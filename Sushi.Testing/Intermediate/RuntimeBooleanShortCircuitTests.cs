using System.Diagnostics;
using FluentAssertions;
using LLVMSharp.Interop;
using NUnit.Framework;
using Sushi.Analysis;
using Sushi.Backends.LLVM;
using Sushi.Backends.Native;
using Sushi.Diagnostics;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.Testing.Intermediate;

[TestFixture]
public sealed class RuntimeBooleanShortCircuitTests
{
    private static readonly Uri testUri = new("file:///TestProject/RuntimeBooleanShortCircuit.sus");

    [TestCase("left and not right", true, false, TestName = "Logical And Should Evaluate Right Operand Only On True")]
    [TestCase("left or not right", false, true, TestName = "Logical Or Should Evaluate Right Operand Only On False")]
    public async Task LowerShould_0(string expression, bool rightWhenTrue, bool shortCircuitValue)
    {
        LoweredFunction lowered = await Lower(expression);

        lowered.Blocks
            .Should()
            .HaveCount(4);

        LoweredConditionalBranchTerminator branch = lowered.EntryBlock.Terminator
            .Should()
            .BeOfType<LoweredConditionalBranchTerminator>()
            .Subject;

        string rightTarget = rightWhenTrue ? branch.TrueTarget : branch.FalseTarget;
        string skipTarget = rightWhenTrue ? branch.FalseTarget : branch.TrueTarget;

        LoweredBasicBlock rightBlock = lowered.Blocks.Single(block => block.Name == rightTarget);
        LoweredBasicBlock skipBlock = lowered.Blocks.Single(block => block.Name == skipTarget);

        rightBlock.Instructions
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<LoweredBooleanNotInstruction>();

        skipBlock.Instructions
            .Should()
            .BeEmpty();

        LoweredBranchTerminator rightBranch = rightBlock.Terminator
            .Should()
            .BeOfType<LoweredBranchTerminator>()
            .Subject;

        LoweredBranchTerminator skipBranch = skipBlock.Terminator
            .Should()
            .BeOfType<LoweredBranchTerminator>()
            .Subject;

        rightBranch.Target
            .Should()
            .Be(skipBranch.Target);

        LoweredBasicBlock mergeBlock = lowered.Blocks.Single(block => block.Name == rightBranch.Target);

        LoweredBooleanPhiInstruction phi = mergeBlock.Instructions
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<LoweredBooleanPhiInstruction>()
            .Subject;

        phi.Incoming
            .Should()
            .HaveCount(2);

        phi.Incoming
            .Select(incoming => incoming.Predecessor)
            .Should()
            .BeEquivalentTo([skipBlock.Name, rightBlock.Name]);

        LoweredPhiIncoming skippedValue = phi.Incoming.Single(incoming => incoming.Predecessor == skipBlock.Name);

        skippedValue.Value
            .Should()
            .BeOfType<LoweredBooleanConstant>()
            .Which
            .Value
            .Should()
            .Be(shortCircuitValue);

        LoweredReturnTerminator result = mergeBlock.Terminator
            .Should()
            .BeOfType<LoweredReturnTerminator>()
            .Subject;

        result.Value
            .Should()
            .BeSameAs(phi.Destination);
    }

    [TestCase(TestName = "Nested Logical Expressions Should Merge Values From Their Actual Predecessors")]
    public async Task LowerShould_1()
    {
        LoweredFunction lowered = await Lower("left and (right or not left)");

        lowered.Blocks
            .Should()
            .HaveCount(7);

        LoweredBooleanPhiInstruction[] phis =
        [
            .. lowered.Blocks.SelectMany(block => block.Instructions).OfType<LoweredBooleanPhiInstruction>()
        ];

        phis
            .Should()
            .HaveCount(2);

        LoweredTemporaryValue[] destinations =
        [
            .. lowered.Blocks.SelectMany(block => block.Instructions).Select(GetDestination)
        ];

        destinations
            .Select(destination => destination.Index)
            .Should()
            .OnlyHaveUniqueItems();

        LoweredConditionalBranchTerminator outerBranch = lowered.EntryBlock.Terminator
            .Should()
            .BeOfType<LoweredConditionalBranchTerminator>()
            .Subject;

        LoweredBasicBlock outerRight = lowered.Blocks.Single(block => block.Name == outerBranch.TrueTarget);

        LoweredConditionalBranchTerminator innerBranch = outerRight.Terminator
            .Should()
            .BeOfType<LoweredConditionalBranchTerminator>()
            .Subject;

        LoweredBasicBlock innerRight = lowered.Blocks.Single(block => block.Name == innerBranch.FalseTarget);

        innerRight.Instructions
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<LoweredBooleanNotInstruction>();

        LoweredBasicBlock finalBlock = lowered.Blocks
            .Should()
            .ContainSingle(block => block.Terminator is LoweredReturnTerminator)
            .Which;

        LoweredBooleanPhiInstruction outerPhi = finalBlock.Instructions
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<LoweredBooleanPhiInstruction>()
            .Subject;

        LoweredPhiIncoming nestedValue = outerPhi.Incoming.Single(incoming => incoming.Value is LoweredTemporaryValue);

        nestedValue.Value
            .Should()
            .BeSameAs(phis.Single(phi => !ReferenceEquals(phi, outerPhi)).Destination);

        LoweredBasicBlock actualPredecessor = lowered.Blocks.Single(block => block.Name == nestedValue.Predecessor);

        actualPredecessor.Instructions
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<LoweredBooleanPhiInstruction>();

        using LLVMModuleArtifact artifact = new LLVMModuleEmitter().Emit("NestedShortCircuit", lowered, CancellationToken.None);

        artifact.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message)
            .Should()
            .BeTrue(message);

        string llvm = artifact.Module.PrintToString();

        llvm
            .Should()
            .Contain("br i1");

        llvm.Split("phi i1", StringSplitOptions.None)
            .Should()
            .HaveCount(3);
    }

    [TestCase(TestName = "Eager Operations Should Consume Short Circuit Results After Control Flow Merges")]
    public async Task LowerShould_2()
    {
        LoweredFunction lowered = await Lower("(left and right) ^ (not left)");

        LoweredBasicBlock mergeBlock = lowered.Blocks
            .Should()
            .ContainSingle(block => block.Terminator is LoweredReturnTerminator)
            .Which;

        mergeBlock.Instructions
            .Should()
            .HaveCount(3);

        mergeBlock.Instructions[0]
            .Should()
            .BeOfType<LoweredBooleanPhiInstruction>();

        mergeBlock.Instructions[1]
            .Should()
            .BeOfType<LoweredBooleanNotInstruction>();

        LoweredBooleanBinaryInstruction eager = mergeBlock.Instructions[2]
            .Should()
            .BeOfType<LoweredBooleanBinaryInstruction>()
            .Subject;

        eager.Operator
            .Should()
            .Be(IRBooleanBinaryOperator.EagerXor);

        using LLVMModuleArtifact artifact = new LLVMModuleEmitter().Emit("LogicalEagerComposition", lowered, CancellationToken.None);

        artifact.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message)
            .Should()
            .BeTrue(message);
    }

    [TestCase("false and (not left)", false, TestName = "False Logical And Should Omit Right Operand")]
    [TestCase("true or (not left)", true, TestName = "True Logical Or Should Omit Right Operand")]
    [TestCase("true and (false or true)", true, TestName = "Constant Logical Expressions Should Fold")]
    [TestCase("false or (true and false)", false, TestName = "Nested Constant Logical Expressions Should Fold")]
    public async Task LowerShould_3(string expression, bool expectedValue)
    {
        LoweredFunction lowered = await Lower(expression);

        lowered.Blocks
            .Should()
            .ContainSingle();

        lowered.EntryBlock.Instructions
            .Should()
            .BeEmpty();

        LoweredReturnTerminator terminator = lowered.EntryBlock.Terminator
            .Should()
            .BeOfType<LoweredReturnTerminator>()
            .Subject;

        terminator.Value
            .Should()
            .BeOfType<LoweredBooleanConstant>()
            .Which
            .Value
            .Should()
            .Be(expectedValue);
    }

    [TestCase("false and missing", TestName = "Short Circuited And Operand Must Still Bind")]
    [TestCase("true or missing", TestName = "Short Circuited Or Operand Must Still Bind")]
    public async Task BindShould_0(string expression)
    {
        string source = $"package Example; namespace Example; public bool check(bool left, bool right) {{ return {expression}; }}";
        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);
        IDiagnosticReporter reporter = new DiagnosticReporter();

        SourceAnalysisResult result = (SourceAnalysisResult)await new SourceAnalyzer().Analyze(snapshot, reporter, CancellationToken.None);

        result.Functions
            .Should()
            .BeEmpty();

        reporter.ReportDiagnostics()
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.UnknownIdentifier));
    }

    [TestCase("left and right", false, false, false, TestName = "Native And False False")]
    [TestCase("left and right", false, true, false, TestName = "Native And False True")]
    [TestCase("left and right", true, false, false, TestName = "Native And True False")]
    [TestCase("left and right", true, true, true, TestName = "Native And True True")]
    [TestCase("left or right", false, false, false, TestName = "Native Or False False")]
    [TestCase("left or right", false, true, true, TestName = "Native Or False True")]
    [TestCase("left or right", true, false, true, TestName = "Native Or True False")]
    [TestCase("left or right", true, true, true, TestName = "Native Or True True")]
    [TestCase("left and (right or not left)", false, true, false, TestName = "Native Nested And Skips Right Expression")]
    [TestCase("left and (right or not left)", true, false, false, TestName = "Native Nested And Evaluates False Inner Result")]
    [TestCase("left and (right or not left)", true, true, true, TestName = "Native Nested And Evaluates True Inner Result")]
    [TestCase("(left or right) & (not left)", false, true, true, TestName = "Native Eager And Consumes Logical Result")]
    [TestCase("(left or right) & (not left)", true, true, false, TestName = "Native Eager And Preserves Left Operand")]
    [TestCase("(left and right) ^ (not left)", true, true, true, TestName = "Native Eager Xor Consumes Merged Result")]
    [TestCase("(left and right) ^ (not left)", false, true, true, TestName = "Native Eager Xor Uses Short Circuit Result")]
    [TestCase("not (left and right)", true, true, false, TestName = "Native Negated Logical And False")]
    [TestCase("not (left and right)", false, true, true, TestName = "Native Negated Logical And True")]
    [TestCase("(left and not right) or (left ^ right)", true, false, true, TestName = "Native Mixed Nested Expression True")]
    [TestCase("(left and not right) or (left ^ right)", true, true, false, TestName = "Native Mixed Nested Expression False")]
    [TestCase("true and (left or right)", false, true, true, TestName = "Native Constant Left And Runtime Right")]
    [TestCase("false or (left and right)", true, false, false, TestName = "Native Constant Left Or Runtime Right")]
    public async Task NativeShould_0(string expression, bool left, bool right, bool expected)
    {
        LoweredFunction lowered = await Lower(expression);

        using LLVMModuleArtifact artifact = new LLVMModuleEmitter().Emit("RuntimeShortCircuitNative", lowered, CancellationToken.None);

        AddTestOnlyCaller(artifact, lowered, left, right);

        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", Guid.NewGuid().ToString("N"));
        string objectPath = Path.Combine(directory, "short-circuit.obj");
        string executablePath = Path.Combine(directory, "ShortCircuit");

        try
        {
            NativeObjectArtifact objectFile = LLVMObjectEmitter.EmitForHost(artifact, objectPath, CancellationToken.None);
            NativeExecutableArtifact executable = await ClangNativeLinker.LinkAsync(objectFile, executablePath, CancellationToken.None);

            using Process process = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executable.FilePath,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start()
                .Should()
                .BeTrue();

            await process.WaitForExitAsync();

            process.ExitCode
                .Should()
                .Be(expected ? 1 : 0);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestCase(TestName = "LLVM Should Reject Branches To Unknown Basic Blocks")]
    public void EmitShould_0()
    {
        LoweredFunction function = new(
            IRAccessibility.Public,
            "test",
            IRType.Bool,
            [
                new LoweredBasicBlock("entry", [], new LoweredBranchTerminator("missing"))
            ]);

        Action action = () =>
        {
            using LLVMModuleArtifact artifact = new LLVMModuleEmitter().Emit("InvalidBranch", function, CancellationToken.None);
        };

        action
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*unknown basic block*");
    }

    [TestCase(TestName = "LLVM Should Reject PHI Instructions After Ordinary Instructions")]
    public void EmitShould_1()
    {
        LoweredTemporaryValue first = new(0, IRType.Bool);
        LoweredTemporaryValue second = new(1, IRType.Bool);

        LoweredFunction function = new(
            IRAccessibility.Public,
            "test",
            IRType.Bool,
            [
                new LoweredBasicBlock("entry",
                [
                    new LoweredBooleanNotInstruction(first, new LoweredBooleanConstant(false)),
                    new LoweredBooleanPhiInstruction(second,
                    [
                        new LoweredPhiIncoming("left", new LoweredBooleanConstant(false)),
                        new LoweredPhiIncoming("right", new LoweredBooleanConstant(true))
                    ])
                ], new LoweredReturnTerminator(second))
            ]);

        Action action = () =>
        {
            using LLVMModuleArtifact artifact = new LLVMModuleEmitter().Emit("MisplacedPhi", function, CancellationToken.None);
        };

        action
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*precede ordinary instructions*");
    }

    [TestCase(TestName = "Boolean PHI Instructions Must Reject Duplicate Predecessors")]
    public void LowerShould_4()
    {
        LoweredTemporaryValue destination = new(0, IRType.Bool);

        Action action = () => _ = new LoweredBooleanPhiInstruction(destination,
        [
            new LoweredPhiIncoming("entry", new LoweredBooleanConstant(false)),
            new LoweredPhiIncoming("entry", new LoweredBooleanConstant(true))
        ]);

        action
            .Should()
            .Throw<ArgumentException>();
    }

    private static LoweredTemporaryValue GetDestination(LoweredInstruction instruction)
        => instruction switch
        {
            LoweredBooleanNotInstruction negation => negation.Destination,
            LoweredBooleanBinaryInstruction binary => binary.Destination,
            LoweredBooleanPhiInstruction phi => phi.Destination,
            _ => throw new NotSupportedException($"Unexpected lowered instruction type \"{instruction.GetType().Name}\".")
        };

    private static void AddTestOnlyCaller(LLVMModuleArtifact artifact, LoweredFunction function, bool left, bool right)
    {
        LLVMModuleRef module = artifact.Module;
        LLVMContextRef context = module.Context;
        LLVMValueRef callee = module.GetNamedFunction(function.Name);

        LLVMTypeRef functionType = LLVMTypeRef.CreateFunction(context.Int1Type, [context.Int1Type, context.Int1Type]);
        LLVMTypeRef mainType = LLVMTypeRef.CreateFunction(context.Int32Type, [context.Int32Type, context.CreatePointerType(0)]);
        LLVMValueRef main = module.AddFunction("main", mainType);
        LLVMBasicBlockRef entry = main.AppendBasicBlock("entry");

        using LLVMBuilderRef builder = context.CreateBuilder();

        builder.PositionAtEnd(entry);

        LLVMValueRef[] arguments =
        [
            LLVMValueRef.CreateConstInt(context.Int1Type, left ? 1UL : 0UL),
            LLVMValueRef.CreateConstInt(context.Int1Type, right ? 1UL : 0UL)
        ];

        LLVMValueRef result = builder.BuildCall2(functionType, callee, arguments, "sushi.test.result");

        builder.BuildRet(builder.BuildZExt(result, context.Int32Type, "sushi.test.exitcode"));

        module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message)
            .Should()
            .BeTrue(message);
    }

    private static async Task<LoweredFunction> Lower(string expression)
    {
        string source = $"package Example; namespace Example; public bool check(bool left, bool right) {{ return {expression}; }}";
        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);
        IDiagnosticReporter reporter = new DiagnosticReporter();

        SourceAnalysisResult result = (SourceAnalysisResult)await new SourceAnalyzer().Analyze(snapshot, reporter, CancellationToken.None);

        reporter.ReportDiagnostics()
            .Should()
            .BeEmpty();

        BoundFunction function = result.Functions
            .Should()
            .ContainSingle()
            .Which;

        IRFunction structured = BoundFunctionIRConverter.Convert(function, CancellationToken.None);

        return IRFunctionLowerer.Lower(structured, CancellationToken.None);
    }
}
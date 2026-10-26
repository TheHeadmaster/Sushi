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
public sealed class RuntimeBooleanOperationTests
{
    private static readonly Uri testUri = new("file:///TestProject/RuntimeBooleanOperations.sus");

    [TestCase("public bool invert(bool value) { return not value; }", 1, "xor i1", TestName = "Lowerer Should Emit Runtime Negation")]
    [TestCase("public bool combine(bool left, bool right) { return left & right; }", 1, "and i1", TestName = "Lowerer Should Emit Runtime Eager And")]
    [TestCase("public bool combine(bool left, bool right) { return left | right; }", 1, "or i1", TestName = "Lowerer Should Emit Runtime Eager Or")]
    [TestCase("public bool combine(bool left, bool right) { return left ^ right; }", 1, "xor i1", TestName = "Lowerer Should Emit Runtime Eager Xor")]
    [TestCase("public bool combine(bool left, bool right) { return not (left ^ right); }", 2, "xor i1", TestName = "Lowerer Should Preserve Nested Runtime Operations")]
    [TestCase("public bool combine(bool left, bool right) { return (left & true) ^ (not right); }", 3, "xor i1", TestName = "Lowerer Should Preserve Evaluation Order")]
    [TestCase("public bool invert(bool value) { return false & (not value); }", 2, "and i1", TestName = "Lowerer Should Evaluate Both Eager Operands")]
    public async Task LowerShould_0(string declaration, int expectedInstructionCount, string expectedOpcode)
    {
        BoundFunction bound = await Bind(declaration);
        IRFunction structured = BoundFunctionIRConverter.Convert(bound, CancellationToken.None);
        LoweredFunction lowered = IRFunctionLowerer.Lower(structured, CancellationToken.None);

        lowered.EntryBlock.Instructions
            .Should()
            .HaveCount(expectedInstructionCount);

        LoweredReturnTerminator terminator = lowered.EntryBlock.Terminator
            .Should()
            .BeOfType<LoweredReturnTerminator>()
            .Subject;

        LoweredTemporaryValue result = terminator.Value
            .Should()
            .BeOfType<LoweredTemporaryValue>()
            .Subject;

        result.Index.Should().Be(expectedInstructionCount - 1);
        result.Type.Should().Be(IRType.Bool);

        for (int i = 0; i < expectedInstructionCount; i++)
        {
            LoweredTemporaryValue destination = GetDestination(lowered.EntryBlock.Instructions[i]);

            destination.Index
                .Should()
                .Be(i);

            destination.Type
                .Should()
                .Be(IRType.Bool);
        }

        using LLVMModuleArtifact artifact = new LLVMModuleEmitter().Emit("RuntimeBooleanOperations", lowered, CancellationToken.None);

        artifact.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message)
            .Should()
            .BeTrue(message);

        artifact.Module.PrintToString()
            .Should()
            .Contain(expectedOpcode);
    }

    [TestCase("public bool check(bool value) { return false and (not value); }", false, TestName = "Lowerer Should Skip False Logical And Right Operand")]
    [TestCase("public bool check(bool value) { return true or (not value); }", true, TestName = "Lowerer Should Skip True Logical Or Right Operand")]
    [TestCase("public bool check(bool value) { return not (true & false); }", true, TestName = "Lowerer Should Fold Constant Boolean Negation")]
    [TestCase("public bool check(bool value) { return true ^ true; }", false, TestName = "Lowerer Should Fold Constant Eager Xor")]
    public async Task LowerShould_1(string declaration, bool expectedValue)
    {
        BoundFunction bound = await Bind(declaration);
        LoweredFunction lowered = IRFunctionLowerer.Lower(BoundFunctionIRConverter.Convert(bound, CancellationToken.None), CancellationToken.None);

        lowered.EntryBlock.Instructions
            .Should()
            .BeEmpty();

        LoweredReturnTerminator terminator = lowered.EntryBlock.Terminator
            .Should()
            .BeOfType<LoweredReturnTerminator>()
            .Subject;

        LoweredBooleanConstant result = terminator.Value
            .Should()
            .BeOfType<LoweredBooleanConstant>()
            .Subject;

        result.Value.Should().Be(expectedValue);
    }

    [TestCase("public bool check(bool value) { return true and value; }", TestName = "Lowerer Should Preserve Right Operand Of Constant True Logical And")]
    [TestCase("public bool check(bool value) { return false or value; }", TestName = "Lowerer Should Preserve Right Operand Of Constant False Logical Or")]
    public async Task LowerShould_2(string declaration)
    {
        BoundFunction bound = await Bind(declaration);
        LoweredFunction lowered = IRFunctionLowerer.Lower(BoundFunctionIRConverter.Convert(bound, CancellationToken.None), CancellationToken.None);

        lowered.EntryBlock.Instructions
            .Should()
            .BeEmpty();

        LoweredReturnTerminator terminator = lowered.EntryBlock.Terminator
            .Should()
            .BeOfType<LoweredReturnTerminator>()
            .Subject;

        LoweredParameterValue result = terminator.Value
            .Should()
            .BeOfType<LoweredParameterValue>()
            .Subject;

        result.Index.Should().Be(0);
        result.Type.Should().Be(IRType.Bool);
    }

    [TestCase("public bool invert(bool value) { return not value; }", false, false, 1, TestName = "Native Runtime Negation Should Invert False")]
    [TestCase("public bool invert(bool value) { return not value; }", true, false, 0, TestName = "Native Runtime Negation Should Invert True")]
    [TestCase("public bool combine(bool left, bool right) { return left & right; }", true, true, 1, TestName = "Native Eager And Should Return True")]
    [TestCase("public bool combine(bool left, bool right) { return left & right; }", true, false, 0, TestName = "Native Eager And Should Return False")]
    [TestCase("public bool combine(bool left, bool right) { return left | right; }", false, true, 1, TestName = "Native Eager Or Should Return True")]
    [TestCase("public bool combine(bool left, bool right) { return left | right; }", false, false, 0, TestName = "Native Eager Or Should Return False")]
    [TestCase("public bool combine(bool left, bool right) { return left ^ right; }", true, false, 1, TestName = "Native Eager Xor Should Return True")]
    [TestCase("public bool combine(bool left, bool right) { return left ^ right; }", true, true, 0, TestName = "Native Eager Xor Should Return False")]
    [TestCase("public bool combine(bool left, bool right) { return not (left ^ right); }", true, false, 0, TestName = "Native Nested Operations Should Return False")]
    [TestCase("public bool combine(bool left, bool right) { return not (left ^ right); }", true, true, 1, TestName = "Native Nested Operations Should Return True")]
    [TestCase("public bool combine(bool left, bool right) { return (left & true) ^ (not right); }", true, true, 1, TestName = "Native Multi Instruction Expression Should Return True")]
    [TestCase("public bool combine(bool left, bool right) { return (left & true) ^ (not right); }", true, false, 0, TestName = "Native Multi Instruction Expression Should Return False")]
    [TestCase("public bool invert(bool value) { return value ^ true; }", false, false, 1, TestName = "Native Runtime Operand With Constant Should Return True")]
    [TestCase("public bool invert(bool value) { return value & false; }", true, false, 0, TestName = "Native Eager And With Constant False Should Return False")]
    public async Task NativeShould_0(string declaration, bool firstArgument, bool secondArgument, int expectedExitCode)
    {
        BoundFunction bound = await Bind(declaration);
        LoweredFunction lowered = IRFunctionLowerer.Lower(BoundFunctionIRConverter.Convert(bound, CancellationToken.None), CancellationToken.None);

        using LLVMModuleArtifact artifact = new LLVMModuleEmitter().Emit("RuntimeBooleanNative", lowered, CancellationToken.None);

        AddTestOnlyCaller(artifact, lowered, firstArgument, secondArgument);

        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", Guid.NewGuid().ToString("N"));
        string objectPath = Path.Combine(directory, "boolean-runtime.obj");
        string executablePath = Path.Combine(directory, "BooleanRuntime");

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

            process.ExitCode.Should().Be(expectedExitCode);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestCase(TestName = "Lowered Boolean Binary Instruction Should Reject Short Circuit Operators")]
    public void LowerShould_3()
    {
        LoweredTemporaryValue destination = new(0, IRType.Bool);
        LoweredBooleanConstant left = new(true);
        LoweredBooleanConstant right = new(false);

        Action action = () => _ = new LoweredBooleanBinaryInstruction(destination, IRBooleanBinaryOperator.LogicalAnd, left, right);

        action
            .Should()
            .Throw<ArgumentOutOfRangeException>();
    }

    [TestCase(TestName = "Lowered Boolean Negation Should Reject Integer Operands")]
    public void LowerShould_4()
    {
        LoweredTemporaryValue destination = new(0, IRType.Bool);
        LoweredIntegerConstant operand = new(1);

        Action action = () => _ = new LoweredBooleanNotInstruction(destination, operand);

        action
            .Should()
            .Throw<ArgumentException>();
    }

    [TestCase(TestName = "LLVM Should Reject Temporary Use Before Definition")]
    public void EmitShould_0()
    {
        LoweredFunction function = new(
            IRAccessibility.Public,
            "test",
            IRType.Bool,
            [
                new LoweredBasicBlock(
                    "entry",
                    [],
                    new LoweredReturnTerminator(new LoweredTemporaryValue(0, IRType.Bool)))
            ]);

        Action action = () =>
        {
            using LLVMModuleArtifact artifact = new LLVMModuleEmitter().Emit("UndefinedTemporary", function, CancellationToken.None);
        };

        action
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*before being defined*");
    }

    [TestCase(TestName = "LLVM Should Reject Duplicate Temporary Definitions")]
    public void EmitShould_1()
    {
        LoweredTemporaryValue destination = new(0, IRType.Bool);

        LoweredFunction function = new(
            IRAccessibility.Public,
            "test",
            IRType.Bool,
            [
                new LoweredBasicBlock(
                    "entry",
                    [
                        new LoweredBooleanNotInstruction(destination, new LoweredBooleanConstant(false)),
                        new LoweredBooleanNotInstruction(destination, new LoweredBooleanConstant(true))
                    ],
                    new LoweredReturnTerminator(destination))
            ]);

        Action action = () =>
        {
            using LLVMModuleArtifact artifact = new LLVMModuleEmitter().Emit("DuplicateTemporary", function, CancellationToken.None);
        };

        action
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*defined more than once*");
    }

    private static LoweredTemporaryValue GetDestination(LoweredInstruction instruction)
        => instruction switch
        {
            LoweredBooleanNotInstruction negation => negation.Destination,
            LoweredBooleanBinaryInstruction binary => binary.Destination,
            _ => throw new NotSupportedException($"Unexpected instruction type \"{instruction.GetType().Name}\".")
        };

    private static void AddTestOnlyCaller(LLVMModuleArtifact artifact, LoweredFunction function, bool firstArgument, bool secondArgument)
    {
        LLVMModuleRef module = artifact.Module;
        LLVMContextRef context = module.Context;
        LLVMValueRef callee = module.GetNamedFunction(function.Name);

        LLVMTypeRef[] parameters =
        [
            .. function.Parameters.Select(parameter => parameter.Type is IRType.Bool ? context.Int1Type : throw new NotSupportedException())
        ];

        LLVMTypeRef calleeType = LLVMTypeRef.CreateFunction(context.Int1Type, parameters);
        LLVMTypeRef mainType = LLVMTypeRef.CreateFunction(context.Int32Type, [context.Int32Type, context.CreatePointerType(0)]);
        LLVMValueRef main = module.AddFunction("main", mainType);
        LLVMBasicBlockRef entry = main.AppendBasicBlock("entry");

        using LLVMBuilderRef builder = context.CreateBuilder();

        builder.PositionAtEnd(entry);

        LLVMValueRef[] arguments = function.Parameters.Count switch
        {
            1 => [LLVMValueRef.CreateConstInt(context.Int1Type, firstArgument ? 1UL : 0UL)],
            2 => [LLVMValueRef.CreateConstInt(context.Int1Type, firstArgument ? 1UL : 0UL), LLVMValueRef.CreateConstInt(context.Int1Type, secondArgument ? 1UL : 0UL)],
            _ => throw new NotSupportedException("The integration test supports one or two Boolean arguments.")
        };

        LLVMValueRef result = builder.BuildCall2(calleeType, callee, arguments, "sushi.test.result");

        builder.BuildRet(builder.BuildZExt(result, context.Int32Type, "sushi.test.exitcode"));

        module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message).Should().BeTrue(message);
    }

    private static async Task<BoundFunction> Bind(string declaration)
    {
        string source = $"package Example; namespace Example; {declaration}";
        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);
        IDiagnosticReporter reporter = new DiagnosticReporter();

        SourceAnalysisResult result = (SourceAnalysisResult)await new SourceAnalyzer().Analyze(snapshot, reporter, CancellationToken.None);

        reporter
            .ReportDiagnostics()
            .Should()
            .BeEmpty();

        return result.Functions
            .Should()
            .ContainSingle()
            .Which;
    }
}
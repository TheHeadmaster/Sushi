using System.Diagnostics;
using FluentAssertions;
using LLVMSharp.Interop;
using NUnit.Framework;
using Sushi.Analysis;
using Sushi.Backends;
using Sushi.Backends.LLVM;
using Sushi.Backends.Native;
using Sushi.Diagnostics;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.Testing.Intermediate;

[TestFixture]
public sealed class RuntimeIntegerArithmeticTests
{
    private static readonly Uri testUri = new("file:///TestProject/RuntimeIntegerArithmetic.sus");

    [TestCase("+left", false, TestName = "Unary Plus Should Preserve Its Operand Without An Instruction")]
    [TestCase("-left", true, TestName = "Unary Negation Should Produce A Checked Instruction")]
    public async Task LowerShould_0(string expression, bool expectedInstruction)
    {
        LoweredFunction lowered = await Lower(expression);

        lowered.Blocks
            .Should()
            .ContainSingle();

        lowered.EntryBlock.Instructions
            .Should()
            .HaveCount(expectedInstruction ? 1 : 0);

        LoweredReturnTerminator terminator = lowered.EntryBlock.Terminator
            .Should()
            .BeOfType<LoweredReturnTerminator>()
            .Subject;

        if (expectedInstruction)
        {
            LoweredIntegerNegateInstruction instruction = lowered.EntryBlock.Instructions[0]
                .Should()
                .BeOfType<LoweredIntegerNegateInstruction>()
                .Subject;

            instruction.Destination.Type
                .Should()
                .Be(IRType.Int32);

            instruction.Operand
                .Should()
                .BeOfType<LoweredParameterValue>();

            terminator.Value
                .Should()
                .BeSameAs(instruction.Destination);
        }
        else
        {
            terminator.Value
                .Should()
                .BeOfType<LoweredParameterValue>();
        }
    }

    [TestCase("left + right", IRIntegerBinaryOperator.Add)]
    [TestCase("left - right", IRIntegerBinaryOperator.Subtract)]
    [TestCase("left * right", IRIntegerBinaryOperator.Multiply)]
    [TestCase("left / right", IRIntegerBinaryOperator.Divide)]
    [TestCase("left % right", IRIntegerBinaryOperator.Remainder)]
    public async Task LowerShould_1(string expression, IRIntegerBinaryOperator operation)
    {
        LoweredFunction lowered = await Lower(expression);

        LoweredIntegerArithmeticInstruction instruction = lowered.EntryBlock.Instructions
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<LoweredIntegerArithmeticInstruction>()
            .Subject;

        instruction.Operator
            .Should()
            .Be(operation);

        instruction.Left
            .Should()
            .BeOfType<LoweredParameterValue>();

        instruction.Right
            .Should()
            .BeOfType<LoweredParameterValue>();

        instruction.Destination.Type
            .Should()
            .Be(IRType.Int32);

        LoweredReturnTerminator terminator = lowered.EntryBlock.Terminator
            .Should()
            .BeOfType<LoweredReturnTerminator>()
            .Subject;

        terminator.Value
            .Should()
            .BeSameAs(instruction.Destination);

        using IModuleArtifact artifact = new LLVMModuleEmitter().Emit("CheckedIntegerArithmetic", lowered, CancellationToken.None);

        LLVMModuleArtifact llvmArtifact = artifact
            .Should()
            .BeOfType<LLVMModuleArtifact>()
            .Subject;

        llvmArtifact.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message)
            .Should()
            .BeTrue(message);
    }

    [TestCase(TestName = "Nested Arithmetic Should Preserve Operation Order And Temporary Identity")]
    public async Task LowerShould_2()
    {
        LoweredFunction lowered = await Lower("-(left * right + 5)");

        lowered.EntryBlock.Instructions
            .Should()
            .HaveCount(3);

        LoweredIntegerArithmeticInstruction multiply = lowered.EntryBlock.Instructions[0]
            .Should()
            .BeOfType<LoweredIntegerArithmeticInstruction>()
            .Subject;

        LoweredIntegerArithmeticInstruction add = lowered.EntryBlock.Instructions[1]
            .Should()
            .BeOfType<LoweredIntegerArithmeticInstruction>()
            .Subject;

        LoweredIntegerNegateInstruction negate = lowered.EntryBlock.Instructions[2]
            .Should()
            .BeOfType<LoweredIntegerNegateInstruction>()
            .Subject;

        multiply.Operator
            .Should()
            .Be(IRIntegerBinaryOperator.Multiply);

        add.Operator
            .Should()
            .Be(IRIntegerBinaryOperator.Add);

        multiply.Destination.Index
            .Should()
            .Be(0);

        add.Destination.Index
            .Should()
            .Be(1);

        negate.Destination.Index
            .Should()
            .Be(2);

        add.Left
            .Should()
            .BeSameAs(multiply.Destination);

        negate.Operand
            .Should()
            .BeSameAs(add.Destination);

        using IModuleArtifact artifact = new LLVMModuleEmitter().Emit("NestedIntegerArithmetic", lowered, CancellationToken.None);

        LLVMModuleArtifact llvmArtifact = artifact
            .Should()
            .BeOfType<LLVMModuleArtifact>()
            .Subject;

        llvmArtifact.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message)
            .Should()
            .BeTrue(message);
    }

    [TestCase("left + right", 17, 25, 42, TestName = "Native Addition Should Produce Exact Result")]
    [TestCase("left + right", int.MaxValue, -1, int.MaxValue - 1, TestName = "Native Addition Should Handle Maximum Boundary")]
    [TestCase("left - right", -20, -22, 2, TestName = "Native Subtraction Should Handle Negative Operands")]
    [TestCase("left - right", int.MinValue, 0, int.MinValue, TestName = "Native Subtraction Should Preserve Minimum Boundary")]
    [TestCase("left * right", -12, -7, 84, TestName = "Native Multiplication Should Handle Two Negative Operands")]
    [TestCase("left * right", int.MinValue, 1, int.MinValue, TestName = "Native Multiplication Should Preserve Minimum Boundary")]
    [TestCase("left / right", 7, 3, 2, TestName = "Native Division Should Truncate Positive Quotient")]
    [TestCase("left / right", -7, 3, -2, TestName = "Native Division Should Truncate Negative Quotient Toward Zero")]
    [TestCase("left / right", 7, -3, -2, TestName = "Native Division Should Preserve Negative Divisor")]
    [TestCase("left / right", -7, -3, 2, TestName = "Native Division Should Handle Two Negative Operands")]
    [TestCase("left % right", 7, 3, 1, TestName = "Native Remainder Should Preserve Positive Dividend")]
    [TestCase("left % right", -7, 3, -1, TestName = "Native Remainder Should Preserve Negative Dividend Sign")]
    [TestCase("left % right", 7, -3, 1, TestName = "Native Remainder Should Handle Negative Divisor")]
    [TestCase("left % right", int.MinValue, -1, 0, TestName = "Native Minimum Integer Remainder Negative One Must Succeed")]
    [TestCase("-left", 42, 0, -42, TestName = "Native Negation Should Produce Negative Result")]
    [TestCase("-left", -42, 0, 42, TestName = "Native Negation Should Produce Positive Result")]
    [TestCase("+left", -42, 0, -42, TestName = "Native Unary Plus Should Preserve Value")]
    [TestCase("-(left * right + 5)", 3, 4, -17, TestName = "Native Nested Arithmetic Should Execute In Order")]
    [TestCase("left + (2147483647 + 1 - 1)", 0, 0, int.MaxValue, TestName = "Native Should Preserve Exact Constant Subexpression")]
    [TestCase("left / (right - 1)", 16, 3, 8, TestName = "Native Should Support Runtime Divisor Expressions")]
    [TestCase("left % (right - 1)", int.MinValue, 0, 0, TestName = "Native Should Allow Remainder When Mathematical Quotient Exceeds Int32")]
    public async Task NativeShould_0(string expression, int left, int right, int expected)
    {
        LoweredFunction lowered = await Lower(expression);

        using IModuleArtifact artifact = new LLVMModuleEmitter().Emit("RuntimeArithmeticNative", lowered, CancellationToken.None);

        LLVMModuleArtifact llvmArtifact = artifact
            .Should()
            .BeOfType<LLVMModuleArtifact>()
            .Subject;

        AddTestOnlyCaller(llvmArtifact, lowered, left, right, expected);

        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", Guid.NewGuid().ToString("N"));
        string objectPath = Path.Combine(directory, "runtime-arithmetic.obj");
        string executablePath = Path.Combine(directory, "RuntimeArithmetic");

        try
        {
            NativeObjectArtifact objectFile = new LLVMObjectEmitter().EmitForHost(artifact, objectPath, CancellationToken.None);
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
                .Be(0);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestCase("-left", TestName = "Runtime Negation Must Contain A Range Guard")]
    [TestCase("left + right", TestName = "Runtime Addition Must Contain A Range Guard")]
    [TestCase("left - right", TestName = "Runtime Subtraction Must Contain A Range Guard")]
    [TestCase("left * right", TestName = "Runtime Multiplication Must Contain A Range Guard")]
    [TestCase("left / right", TestName = "Runtime Division Must Guard Divisor And Quotient")]
    [TestCase("left % right", TestName = "Runtime Remainder Must Guard Zero Divisor")]
    public async Task EmitShould_0(string expression)
    {
        LoweredFunction lowered = await Lower(expression);

        using IModuleArtifact artifact = new LLVMModuleEmitter().Emit("RuntimeArithmeticGuards", lowered, CancellationToken.None);

        LLVMModuleArtifact llvmArtifact = artifact
            .Should()
            .BeOfType<LLVMModuleArtifact>()
            .Subject;

        string llvm = llvmArtifact.Module.PrintToString();

        llvm
            .Should()
            .Contain("br i1");

        llvm
            .Should()
            .Contain("call void @llvm.trap()");

        llvm
            .Should()
            .Contain("unreachable");

        llvmArtifact.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message)
            .Should()
            .BeTrue(message);
    }

    [TestCase("left / right", TestName = "Division Must Check Zero Before Executing Signed Division")]
    [TestCase("left % right", TestName = "Remainder Must Check Zero Before Executing Signed Remainder")]
    public async Task EmitShould_1(string expression)
    {
        LoweredFunction lowered = await Lower(expression);

        using IModuleArtifact artifact = new LLVMModuleEmitter().Emit("SafeDivision", lowered, CancellationToken.None);

        LLVMModuleArtifact llvmArtifact = artifact
            .Should()
            .BeOfType<LLVMModuleArtifact>()
            .Subject;

        string llvm = llvmArtifact.Module.PrintToString();

        llvm
            .Should()
            .Contain("icmp eq i64");

        llvm
            .Should()
            .Contain("sushi.arithmetic.continue");
    }

    [TestCase(TestName = "Arithmetic Guard Splitting Must Preserve PHI Predecessor Identity")]
    public void EmitShould_2()
    {
        LoweredTemporaryValue arithmeticResult = new(0, IRType.Int32);
        LoweredTemporaryValue booleanResult = new(1, IRType.Bool);
        LoweredTemporaryValue phiResult = new(2, IRType.Bool);

        LoweredFunction lowered = new(
            IRAccessibility.Public,
            "merge",
            IRType.Bool,
            [
                new IRParameter(0, "condition", IRType.Bool),
                new IRParameter(1, "value", IRType.Int32)
            ],
            [
                new LoweredBasicBlock("entry", [],
                    new LoweredConditionalBranchTerminator(new LoweredParameterValue(0, IRType.Bool), "calculate", "skip")),
                new LoweredBasicBlock("calculate",
                [
                    new LoweredIntegerArithmeticInstruction(arithmeticResult, IRIntegerBinaryOperator.Add, new LoweredParameterValue(1, IRType.Int32), new LoweredIntegerConstant(1)),
                    new LoweredBooleanNotInstruction(booleanResult, new LoweredBooleanConstant(false))
                ], new LoweredBranchTerminator("merge.result")),
                new LoweredBasicBlock("skip", [], new LoweredBranchTerminator("merge.result")),
                new LoweredBasicBlock("merge.result",
                [
                    new LoweredBooleanPhiInstruction(phiResult,
                    [
                        new LoweredPhiIncoming("calculate", booleanResult),
                        new LoweredPhiIncoming("skip", new LoweredBooleanConstant(false))
                    ])
                ], new LoweredReturnTerminator(phiResult))
            ]);

        using IModuleArtifact artifact = new LLVMModuleEmitter().Emit("GuardedPhi", lowered, CancellationToken.None);

        LLVMModuleArtifact llvmArtifact = artifact
            .Should()
            .BeOfType<LLVMModuleArtifact>()
            .Subject;

        llvmArtifact.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message)
            .Should()
            .BeTrue(message);

        llvmArtifact.Module.PrintToString()
            .Should()
            .Contain("phi i1");
    }

    [TestCase(TestName = "Integer Arithmetic Instruction Must Reject Bitwise Operators")]
    public void LowerShould_3()
    {
        LoweredTemporaryValue destination = new(0, IRType.Int32);
        LoweredIntegerConstant left = new(1);
        LoweredIntegerConstant right = new(2);

        Action action = () => _ = new LoweredIntegerArithmeticInstruction(destination, IRIntegerBinaryOperator.BitwiseAnd, left, right);

        action
            .Should()
            .Throw<ArgumentOutOfRangeException>();
    }

    [TestCase(TestName = "Integer Negation Instruction Must Reject Boolean Operands")]
    public void LowerShould_4()
    {
        LoweredTemporaryValue destination = new(0, IRType.Int32);
        LoweredBooleanConstant operand = new(true);

        Action action = () => _ = new LoweredIntegerNegateInstruction(destination, operand);

        action
            .Should()
            .Throw<ArgumentException>();
    }

    private static void AddTestOnlyCaller(LLVMModuleArtifact artifact, LoweredFunction function, int left, int right, int expected)
    {
        LLVMModuleRef module = artifact.Module;
        LLVMContextRef context = module.Context;
        LLVMValueRef callee = module.GetNamedFunction(function.Name);

        LLVMTypeRef calleeType = LLVMTypeRef.CreateFunction(context.Int32Type, [context.Int32Type, context.Int32Type]);
        LLVMTypeRef mainType = LLVMTypeRef.CreateFunction(context.Int32Type, [context.Int32Type, context.CreatePointerType(0)]);
        LLVMValueRef main = module.AddFunction("main", mainType);
        LLVMBasicBlockRef entry = main.AppendBasicBlock("entry");

        using LLVMBuilderRef builder = context.CreateBuilder();

        builder.PositionAtEnd(entry);

        LLVMValueRef[] arguments =
        [
            LLVMValueRef.CreateConstInt(context.Int32Type, unchecked((ulong)(uint)left)),
            LLVMValueRef.CreateConstInt(context.Int32Type, unchecked((ulong)(uint)right))
        ];

        LLVMValueRef result = builder.BuildCall2(calleeType, callee, arguments, "sushi.test.result");
        LLVMValueRef expectedValue = LLVMValueRef.CreateConstInt(context.Int32Type, unchecked((ulong)(uint)expected));
        LLVMValueRef matches = builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, result, expectedValue, "sushi.test.matches");
        LLVMValueRef zero = LLVMValueRef.CreateConstInt(context.Int32Type, 0UL);
        LLVMValueRef one = LLVMValueRef.CreateConstInt(context.Int32Type, 1UL);

        builder.BuildRet(builder.BuildSelect(matches, zero, one, "sushi.test.exitcode"));

        module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message)
            .Should()
            .BeTrue(message);
    }

    private static async Task<LoweredFunction> Lower(string expression)
    {
        string source = $"package Example; namespace Example; public int32 evaluate(int32 left, int32 right) {{ return {expression}; }}";
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

        return IRFunctionLowerer.Lower(BoundFunctionIRConverter.Convert(function, CancellationToken.None), CancellationToken.None);
    }
}
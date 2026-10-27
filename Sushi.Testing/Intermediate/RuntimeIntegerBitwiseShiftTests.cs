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
public sealed class RuntimeIntegerBitwiseShiftTests
{
    private static readonly Uri testUri = new("file:///TestProject/RuntimeIntegerBitwiseShift.sus");

    [TestCase(TestName = "Integer Complement Should Lower Without Arithmetic Checks")]
    public async Task LowerShould_0()
    {
        LoweredFunction lowered = await Lower("~left");

        LoweredIntegerComplementInstruction instruction = lowered.EntryBlock.Instructions
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<LoweredIntegerComplementInstruction>()
            .Subject;

        instruction.Operand
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

        using IModuleArtifact artifact = new LLVMModuleEmitter().Emit("IntegerComplement", lowered, CancellationToken.None);

        LLVMModuleArtifact llvmArtifact = artifact
            .Should()
            .BeOfType<LLVMModuleArtifact>()
            .Subject;

        string llvm = llvmArtifact.Module.PrintToString();

        llvm
            .Should()
            .Contain("xor i32");

        llvm
            .Should()
            .NotContain("@llvm.trap");

        llvmArtifact.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message)
            .Should()
            .BeTrue(message);
    }

    [TestCase("left & right", IRIntegerBinaryOperator.BitwiseAnd, "and i32", TestName = "Integer Bitwise And Should Lower")]
    [TestCase("left | right", IRIntegerBinaryOperator.BitwiseOr, "or i32", TestName = "Integer Bitwise Or Should Lower")]
    [TestCase("left ^ right", IRIntegerBinaryOperator.BitwiseXor, "xor i32", TestName = "Integer Bitwise Xor Should Lower")]
    public async Task LowerShould_1(string expression, IRIntegerBinaryOperator operation, string opcode)
    {
        LoweredFunction lowered = await Lower(expression);

        LoweredIntegerBitwiseInstruction instruction = lowered.EntryBlock.Instructions
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<LoweredIntegerBitwiseInstruction>()
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

        using IModuleArtifact artifact = new LLVMModuleEmitter().Emit("IntegerBitwise", lowered, CancellationToken.None);

        LLVMModuleArtifact llvmArtifact = artifact
            .Should()
            .BeOfType<LLVMModuleArtifact>()
            .Subject;

        string llvm = llvmArtifact.Module.PrintToString();

        llvm
            .Should()
            .Contain(opcode);

        llvm
            .Should()
            .NotContain("@llvm.trap");

        llvmArtifact.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message)
            .Should()
            .BeTrue(message);
    }

    [TestCase("left << right", IRIntegerBinaryOperator.ShiftLeft, "shl i64", true, TestName = "Left Shift Should Use Checked Wide Integer Result")]
    [TestCase("left >> right", IRIntegerBinaryOperator.ShiftRight, "ashr i32", false, TestName = "Right Shift Should Preserve Signed Semantics")]
    public async Task LowerShould_2(string expression, IRIntegerBinaryOperator operation, string opcode, bool checkedResult)
    {
        LoweredFunction lowered = await Lower(expression);

        LoweredIntegerShiftInstruction instruction = lowered.EntryBlock.Instructions
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<LoweredIntegerShiftInstruction>()
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

        using IModuleArtifact artifact = new LLVMModuleEmitter().Emit("IntegerShift", lowered, CancellationToken.None);

        LLVMModuleArtifact llvmArtifact = artifact
            .Should()
            .BeOfType<LLVMModuleArtifact>()
            .Subject;

        string llvm = llvmArtifact.Module.PrintToString();

        llvm
            .Should()
            .Contain(opcode);

        llvm
            .Should()
            .Contain("icmp slt i32");

        llvm
            .Should()
            .Contain("icmp sge i32");

        llvm
            .Should()
            .Contain("sushi.shift.failure");

        llvm
            .Should()
            .Contain("sushi.shift.continue");

        llvm
            .Should()
            .Contain("call void @llvm.trap()");

        llvm.Contains("sushi.arithmetic.failure", StringComparison.Ordinal)
            .Should()
            .Be(checkedResult);

        llvmArtifact.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message)
            .Should()
            .BeTrue(message);
    }

    [TestCase(TestName = "Nested Operations Should Preserve Order And Temporary Identity")]
    public async Task LowerShould_3()
    {
        LoweredFunction lowered = await Lower("~((left << 1) ^ right)");

        lowered.EntryBlock.Instructions
            .Should()
            .HaveCount(3);

        LoweredIntegerShiftInstruction shift = lowered.EntryBlock.Instructions[0]
            .Should()
            .BeOfType<LoweredIntegerShiftInstruction>()
            .Subject;

        LoweredIntegerBitwiseInstruction xor = lowered.EntryBlock.Instructions[1]
            .Should()
            .BeOfType<LoweredIntegerBitwiseInstruction>()
            .Subject;

        LoweredIntegerComplementInstruction complement = lowered.EntryBlock.Instructions[2]
            .Should()
            .BeOfType<LoweredIntegerComplementInstruction>()
            .Subject;

        shift.Destination.Index
            .Should()
            .Be(0);

        xor.Destination.Index
            .Should()
            .Be(1);

        complement.Destination.Index
            .Should()
            .Be(2);

        xor.Left
            .Should()
            .BeSameAs(shift.Destination);

        complement.Operand
            .Should()
            .BeSameAs(xor.Destination);

        using IModuleArtifact artifact = new LLVMModuleEmitter().Emit("NestedIntegerBitwise", lowered, CancellationToken.None);

        LLVMModuleArtifact llvmArtifact = artifact
            .Should()
            .BeOfType<LLVMModuleArtifact>()
            .Subject;

        llvmArtifact.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message)
            .Should()
            .BeTrue(message);
    }

    [TestCase("left << 32", ErrorType.InvalidIntegerShiftCount, TestName = "Binder Should Reject Constant Shift Count Equal To Width")]
    [TestCase("left >> -1", ErrorType.InvalidIntegerShiftCount, TestName = "Binder Should Reject Negative Constant Shift Count")]
    [TestCase("left << 2147483648", ErrorType.InvalidIntegerShiftCount, TestName = "Binder Should Reject Exact Count Beyond Int32")]
    [TestCase("left & 2147483648", ErrorType.IntegerConstantOutOfRange, TestName = "Binder Should Reject Bitwise Constant Outside Int32")]
    [TestCase("left ^ 2147483648", ErrorType.IntegerConstantOutOfRange, TestName = "Binder Should Reject Xor Constant Outside Int32")]
    public async Task BindShould_0(string expression, ErrorType expectedDiagnostic)
    {
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(expression);

        result.Functions
            .Should()
            .BeEmpty();

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(expectedDiagnostic));
    }

    [TestCase("left << (2147483648 & 1)", 0, TestName = "Shift Count Should Retain Untyped Exact Bitwise Evaluation")]
    [TestCase("left << (2147483647 + 1 - 1)", 2147483647, TestName = "Out Of Range Exact Count Should Be Diagnosed")]
    public async Task BindShould_1(string expression, int expectedCount)
    {
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(expression);

        if (expectedCount >= 32)
        {
            result.Functions
                .Should()
                .BeEmpty();

            diagnostics
                .Should()
                .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.InvalidIntegerShiftCount));

            return;
        }

        diagnostics
            .Should()
            .BeEmpty();

        BoundFunction function = result.Functions
            .Should()
            .ContainSingle()
            .Which;

        BoundReturnStatement statement = function.Body.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<BoundReturnStatement>()
            .Subject;

        BoundIntegerBinaryExpression binary = statement.Expression
            .Should()
            .BeOfType<BoundIntegerBinaryExpression>()
            .Subject;

        binary.Right
            .Should()
            .BeOfType<BoundIntegerLiteralExpression>()
            .Which
            .Value
            .Should()
            .Be(expectedCount);
    }

    [TestCase("~left", 0, 0, -1, TestName = "Native Complement Zero")]
    [TestCase("~left", 5, 0, -6, TestName = "Native Complement Positive")]
    [TestCase("~left", -1, 0, 0, TestName = "Native Complement Negative One")]
    [TestCase("~left", int.MinValue, 0, int.MaxValue, TestName = "Native Complement Minimum Integer")]
    [TestCase("left & right", -5, 3, 3, TestName = "Native And Negative Operand")]
    [TestCase("left & right", -1, 5, 5, TestName = "Native And Negative One")]
    [TestCase("left & right", int.MaxValue, int.MinValue, 0, TestName = "Native And Disjoint Sign Bits")]
    [TestCase("left | right", -5, 3, -5, TestName = "Native Or Negative Operand")]
    [TestCase("left | right", 5, 8, 13, TestName = "Native Or Positive Operands")]
    [TestCase("left ^ right", -5, 3, -8, TestName = "Native Xor Negative Operand")]
    [TestCase("left ^ right", int.MaxValue, int.MinValue, -1, TestName = "Native Xor All Bits")]
    [TestCase("left << right", 1, 30, 1073741824, TestName = "Native Left Shift Positive Boundary")]
    [TestCase("left << right", -1, 31, int.MinValue, TestName = "Native Left Shift Negative One By Maximum Count")]
    [TestCase("left << right", -64, 1, -128, TestName = "Native Left Shift Negative Integer")]
    [TestCase("left << right", -2, 30, int.MinValue, TestName = "Native Left Shift To Signed Minimum")]
    [TestCase("left << right", 0, 31, 0, TestName = "Native Left Shift Zero By Maximum Count")]
    [TestCase("left << right", 42, 0, 42, TestName = "Native Left Shift By Zero")]
    [TestCase("left >> right", -7, 1, -4, TestName = "Native Right Shift Negative Odd Integer")]
    [TestCase("left >> right", -8, 1, -4, TestName = "Native Right Shift Negative Even Integer")]
    [TestCase("left >> right", int.MinValue, 31, -1, TestName = "Native Right Shift Minimum Integer By Maximum Count")]
    [TestCase("left >> right", int.MaxValue, 31, 0, TestName = "Native Right Shift Maximum Integer By Maximum Count")]
    [TestCase("left >> right", -42, 0, -42, TestName = "Native Right Shift By Zero")]
    [TestCase("~(left & right)", -5, 3, -4, TestName = "Native Complement Nested And")]
    [TestCase("left | (~right)", 5, 3, -3, TestName = "Native Or Nested Complement")]
    [TestCase("(left << 1) ^ right", 5, 3, 9, TestName = "Native Shift And Xor Composition")]
    [TestCase("(left >> 1) + right", -7, 5, 1, TestName = "Native Shift And Arithmetic Composition")]
    [TestCase("(left & right) + 1", -5, 3, 4, TestName = "Native Bitwise And Arithmetic Composition")]
    [TestCase("left << (right + 1)", -1, 30, int.MinValue, TestName = "Native Computed Runtime Shift Count")]
    [TestCase("left << (2147483648 & 1)", 9, 0, 9, TestName = "Native Exact Constant Shift Count")]
    [TestCase("left ^ (2147483648 - 2147483647)", 5, 0, 4, TestName = "Native Exact Constant Bitwise Operand")]
    [TestCase("left & -1", 5, 0, 5, TestName = "Native Signed Bitwise Mask")]
    public async Task NativeShould_0(string expression, int left, int right, int expected)
    {
        LoweredFunction lowered = await Lower(expression);

        using IModuleArtifact artifact = new LLVMModuleEmitter().Emit("RuntimeBitwiseShiftNative", lowered, CancellationToken.None);

        LLVMModuleArtifact llvmArtifact = artifact
            .Should()
            .BeOfType<LLVMModuleArtifact>()
            .Subject;

        AddTestOnlyCaller(llvmArtifact, lowered, left, right, expected);

        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", Guid.NewGuid().ToString("N"));
        string objectPath = Path.Combine(directory, "runtime-bitwise-shift.obj");
        string executablePath = Path.Combine(directory, "RuntimeBitwiseShift");

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

    [TestCase(TestName = "Guarded Shifts Must Preserve PHI Predecessor Identity")]
    public void EmitShould_0()
    {
        LoweredTemporaryValue shifted = new(0, IRType.Int32);
        LoweredTemporaryValue evaluated = new(1, IRType.Bool);
        LoweredTemporaryValue merged = new(2, IRType.Bool);

        LoweredFunction lowered = new(
            IRAccessibility.Public,
            "merge",
            IRType.Bool,
            [
                new IRParameter(0, "condition", IRType.Bool),
                new IRParameter(1, "value", IRType.Int32),
                new IRParameter(2, "count", IRType.Int32)
            ],
            [
                new LoweredBasicBlock("entry", [],
                    new LoweredConditionalBranchTerminator(new LoweredParameterValue(0, IRType.Bool), "calculate", "skip")),
                new LoweredBasicBlock("calculate",
                [
                    new LoweredIntegerShiftInstruction(shifted, IRIntegerBinaryOperator.ShiftLeft,
                        new LoweredParameterValue(1, IRType.Int32), new LoweredParameterValue(2, IRType.Int32)),
                    new LoweredBooleanNotInstruction(evaluated, new LoweredBooleanConstant(false))
                ], new LoweredBranchTerminator("merge.result")),
                new LoweredBasicBlock("skip", [], new LoweredBranchTerminator("merge.result")),
                new LoweredBasicBlock("merge.result",
                [
                    new LoweredBooleanPhiInstruction(merged,
                    [
                        new LoweredPhiIncoming("calculate", evaluated),
                        new LoweredPhiIncoming("skip", new LoweredBooleanConstant(false))
                    ])
                ], new LoweredReturnTerminator(merged))
            ]);

        using IModuleArtifact artifact = new LLVMModuleEmitter().Emit("GuardedShiftPhi", lowered, CancellationToken.None);

        LLVMModuleArtifact llvmArtifact = artifact
            .Should()
            .BeOfType<LLVMModuleArtifact>()
            .Subject;

        llvmArtifact.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message)
            .Should()
            .BeTrue(message);

        string llvm = llvmArtifact.Module.PrintToString();

        llvm
            .Should()
            .Contain("phi i1");

        llvm
            .Should()
            .Contain("sushi.shift.continue");

        llvm
            .Should()
            .Contain("sushi.arithmetic.continue");
    }

    [TestCase(TestName = "Bitwise Instruction Must Reject Arithmetic Operator")]
    public void LowerShould_4()
    {
        LoweredTemporaryValue destination = new(0, IRType.Int32);
        LoweredIntegerConstant left = new(1);
        LoweredIntegerConstant right = new(2);

        Action action = () => _ = new LoweredIntegerBitwiseInstruction(destination, IRIntegerBinaryOperator.Add, left, right);

        action
            .Should()
            .Throw<ArgumentOutOfRangeException>();
    }

    [TestCase(TestName = "Shift Instruction Must Reject Bitwise Operator")]
    public void LowerShould_5()
    {
        LoweredTemporaryValue destination = new(0, IRType.Int32);
        LoweredIntegerConstant left = new(1);
        LoweredIntegerConstant right = new(2);

        Action action = () => _ = new LoweredIntegerShiftInstruction(destination, IRIntegerBinaryOperator.BitwiseXor, left, right);

        action
            .Should()
            .Throw<ArgumentOutOfRangeException>();
    }

    [TestCase(TestName = "Complement Instruction Must Reject Boolean Operand")]
    public void LowerShould_6()
    {
        LoweredTemporaryValue destination = new(0, IRType.Int32);
        LoweredBooleanConstant operand = new(true);

        Action action = () => _ = new LoweredIntegerComplementInstruction(destination, operand);

        action
            .Should()
            .Throw<ArgumentException>();
    }

    [TestCase(TestName = "Shift Instruction Must Reject Boolean Count")]
    public void LowerShould_7()
    {
        LoweredTemporaryValue destination = new(0, IRType.Int32);
        LoweredIntegerConstant left = new(1);
        LoweredBooleanConstant right = new(false);

        Action action = () => _ = new LoweredIntegerShiftInstruction(destination, IRIntegerBinaryOperator.ShiftLeft, left, right);

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
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(expression);

        diagnostics
            .Should()
            .BeEmpty();

        BoundFunction function = result.Functions
            .Should()
            .ContainSingle()
            .Which;

        return IRFunctionLowerer.Lower(BoundFunctionIRConverter.Convert(function, CancellationToken.None), CancellationToken.None);
    }

    private static async Task<(SourceAnalysisResult Result, IReadOnlyList<SushiDiagnostic> Diagnostics)> Analyze(string expression)
    {
        string source = $"package Example; namespace Example; public int32 evaluate(int32 left, int32 right) {{ return {expression}; }}";
        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);
        IDiagnosticReporter reporter = new DiagnosticReporter();

        SourceAnalysisResult result = (SourceAnalysisResult)await new SourceAnalyzer().Analyze(snapshot, reporter, CancellationToken.None);

        return (result, reporter.ReportDiagnostics());
    }
}
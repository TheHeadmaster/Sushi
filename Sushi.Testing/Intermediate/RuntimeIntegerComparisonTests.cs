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
public sealed class RuntimeIntegerComparisonTests
{
    private static readonly Uri testUri = new("file:///TestProject/RuntimeIntegerComparison.sus");

    [TestCase("value == other", IRIntegerComparisonOperator.Equal, TestName = "Equality Should Lower To A Boolean Comparison")]
    [TestCase("value != other", IRIntegerComparisonOperator.NotEqual, TestName = "Inequality Should Lower To A Boolean Comparison")]
    [TestCase("value < other", IRIntegerComparisonOperator.LessThan, TestName = "Less Than Should Lower To A Boolean Comparison")]
    [TestCase("value <= other", IRIntegerComparisonOperator.LessThanOrEqual, TestName = "Less Than Or Equal Should Lower To A Boolean Comparison")]
    [TestCase("value > other", IRIntegerComparisonOperator.GreaterThan, TestName = "Greater Than Should Lower To A Boolean Comparison")]
    [TestCase("value >= other", IRIntegerComparisonOperator.GreaterThanOrEqual, TestName = "Greater Than Or Equal Should Lower To A Boolean Comparison")]
    public async Task LowerShould_0(string expression, IRIntegerComparisonOperator operation)
    {
        LoweredFunction lowered = await Lower(expression);

        LoweredIntegerComparisonInstruction comparison = lowered.EntryBlock.Instructions
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<LoweredIntegerComparisonInstruction>()
            .Subject;

        comparison.Operator
            .Should()
            .Be(operation);

        comparison.Destination.Type
            .Should()
            .Be(IRType.Bool);

        comparison.Left
            .Should()
            .BeOfType<LoweredParameterValue>();

        comparison.Right
            .Should()
            .BeOfType<LoweredParameterValue>();

        LoweredReturnTerminator result = lowered.EntryBlock.Terminator
            .Should()
            .BeOfType<LoweredReturnTerminator>()
            .Subject;

        result.Value
            .Should()
            .BeSameAs(comparison.Destination);
    }

    [TestCase("value < 2147483648", true, TestName = "Exact Constant Above Int32 Should Preserve Runtime Evaluation")]
    [TestCase("2147483648 < value", false, TestName = "Exact Constant On Left Should Preserve Comparison Direction")]
    [TestCase("-2147483649 < value", true, TestName = "Exact Constant Below Int32 Should Preserve Runtime Evaluation")]
    [TestCase("value == (1 << 100)", false, TestName = "Large Exact Constant Should Not Narrow To Int32")]
    [TestCase("(1 << 100) != value", true, TestName = "Large Exact Constant Inequality Should Not Narrow")]
    public async Task BindShould_0(string expression, bool expected)
    {
        BoundFunction function = await Bind(expression);

        BoundReturnStatement statement = function.Body.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<BoundReturnStatement>()
            .Subject;

        BoundIntegerRangeComparisonExpression range = statement.Expression
            .Should()
            .BeOfType<BoundIntegerRangeComparisonExpression>()
            .Subject;

        range.Result
            .Should()
            .Be(expected);

        range.Operand.Type
            .Should()
            .Be(BoundType.Int32);

        IRFunction structured = BoundFunctionIRConverter.Convert(function, CancellationToken.None);

        IRReturnStatement structuredReturn = structured.Body.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<IRReturnStatement>()
            .Subject;

        IRIntegerRangeComparisonExpression structuredRange = structuredReturn.Expression
            .Should()
            .BeOfType<IRIntegerRangeComparisonExpression>()
            .Subject;

        structuredRange.Result
            .Should()
            .Be(expected);
    }

    [TestCase(TestName = "Concrete Comparison Should Retain Distinct Integer And Boolean Types")]
    public async Task BindShould_1()
    {
        BoundFunction function = await Bind("(value + 1) >= other");

        BoundReturnStatement statement = function.Body.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<BoundReturnStatement>()
            .Subject;

        BoundIntegerComparisonExpression comparison = statement.Expression
            .Should()
            .BeOfType<BoundIntegerComparisonExpression>()
            .Subject;

        comparison.Type
            .Should()
            .Be(BoundType.Bool);

        comparison.Operator
            .Should()
            .Be(IntegerComparisonOperator.GreaterThanOrEqual);

        comparison.Left
            .Should()
            .BeOfType<BoundIntegerBinaryExpression>();

        comparison.Right
            .Should()
            .BeOfType<BoundParameterReferenceExpression>();

        IRFunction structured = BoundFunctionIRConverter.Convert(function, CancellationToken.None);

        IRReturnStatement structuredReturn = structured.Body.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<IRReturnStatement>()
            .Subject;

        IRIntegerComparisonExpression structuredComparison = structuredReturn.Expression
            .Should()
            .BeOfType<IRIntegerComparisonExpression>()
            .Subject;

        structuredComparison.Type
            .Should()
            .Be(IRType.Bool);

        structuredComparison.Left.Type
            .Should()
            .Be(IRType.Int32);

        structuredComparison.Right.Type
            .Should()
            .Be(IRType.Int32);
    }

    [TestCase(TestName = "Out Of Range Constant Must Not Eliminate Runtime Arithmetic Guards")]
    public async Task LowerShould_1()
    {
        LoweredFunction lowered = await Lower("(value + 1) < 2147483648");

        lowered.EntryBlock.Instructions
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<LoweredIntegerArithmeticInstruction>();

        LoweredReturnTerminator terminator = lowered.EntryBlock.Terminator
            .Should()
            .BeOfType<LoweredReturnTerminator>()
            .Subject;

        LoweredBooleanConstant result = terminator.Value
            .Should()
            .BeOfType<LoweredBooleanConstant>()
            .Subject;

        result.Value
            .Should()
            .BeTrue();

        using IModuleArtifact artifact = new LLVMModuleEmitter().Emit("RangeEvaluation", lowered, CancellationToken.None);

        LLVMModuleArtifact llvmArtifact = artifact
            .Should()
            .BeOfType<LLVMModuleArtifact>()
            .Subject;

        string llvm = llvmArtifact.Module.PrintToString();

        llvm
            .Should()
            .Contain("sushi.arithmetic.failure");

        llvm
            .Should()
            .Contain("sushi.arithmetic.continue");

        llvm
            .Should()
            .Contain("@llvm.trap");

        llvmArtifact.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message)
            .Should()
            .BeTrue(message);
    }

    [TestCase("value == other", "icmp eq i32", TestName = "LLVM Should Emit Integer Equality")]
    [TestCase("value != other", "icmp ne i32", TestName = "LLVM Should Emit Integer Inequality")]
    [TestCase("value < other", "icmp slt i32", TestName = "LLVM Should Emit Signed Less Than")]
    [TestCase("value <= other", "icmp sle i32", TestName = "LLVM Should Emit Signed Less Than Or Equal")]
    [TestCase("value > other", "icmp sgt i32", TestName = "LLVM Should Emit Signed Greater Than")]
    [TestCase("value >= other", "icmp sge i32", TestName = "LLVM Should Emit Signed Greater Than Or Equal")]
    public async Task EmitShould_0(string expression, string opcode)
    {
        LoweredFunction lowered = await Lower(expression);

        using IModuleArtifact artifact = new LLVMModuleEmitter().Emit("RuntimeComparison", lowered, CancellationToken.None);

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

    [TestCase(TestName = "Mixed Arithmetic Comparison And Logical Short Circuit Must Preserve SSA")]
    public async Task EmitShould_1()
    {
        LoweredFunction lowered = await Lower("enabled and ((value * 2 + 5) >= 100)");

        lowered.Blocks
            .Should()
            .HaveCount(4);

        lowered.Blocks.SelectMany(block => block.Instructions)
            .Should()
            .ContainSingle(instruction => instruction is LoweredIntegerComparisonInstruction);

        lowered.Blocks.SelectMany(block => block.Instructions)
            .Should()
            .ContainSingle(instruction => instruction is LoweredBooleanPhiInstruction);

        using IModuleArtifact artifact = new LLVMModuleEmitter().Emit("MixedBooleanInteger", lowered, CancellationToken.None);

        LLVMModuleArtifact llvmArtifact = artifact
            .Should()
            .BeOfType<LLVMModuleArtifact>()
            .Subject;

        string llvm = llvmArtifact.Module.PrintToString();

        llvm
            .Should()
            .Contain("icmp sge i32");

        llvm
            .Should()
            .Contain("phi i1");

        llvm
            .Should()
            .Contain("sushi.arithmetic.failure");

        llvmArtifact.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message)
            .Should()
            .BeTrue(message);
    }

    [TestCase(TestName = "Range Comparison Inside Logical Expression Must Retain Arithmetic Failure Control Flow")]
    public async Task EmitShould_2()
    {
        LoweredFunction lowered = await Lower("enabled and ((value + 1) < 2147483648)");

        using IModuleArtifact artifact = new LLVMModuleEmitter().Emit("GuardedRangeComparison", lowered, CancellationToken.None);

        LLVMModuleArtifact llvmArtifact = artifact
            .Should()
            .BeOfType<LLVMModuleArtifact>()
            .Subject;

        string llvm = llvmArtifact.Module.PrintToString();

        llvm
            .Should()
            .Contain("sushi.arithmetic.failure");

        llvm
            .Should()
            .Contain("sushi.arithmetic.continue");

        llvm
            .Should()
            .Contain("phi i1");

        llvmArtifact.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message)
            .Should()
            .BeTrue(message);
    }

    [TestCase("enabled and ((value / 0) == 1)", ErrorType.IntegerDivisionByZero, TestName = "Short Circuit Must Not Suppress Static Division Diagnostics")]
    [TestCase("enabled or ((value << 32) > 0)", ErrorType.InvalidIntegerShiftCount, TestName = "Short Circuit Must Not Suppress Static Shift Diagnostics")]
    [TestCase("value < (1 / 0)", ErrorType.IntegerDivisionByZero, TestName = "Exact Comparison Operand Must Retain Division Diagnostics")]
    [TestCase("value < missing", ErrorType.UnknownIdentifier, TestName = "Runtime Comparison Must Reject Unknown Identifiers")]
    [TestCase("enabled < value", ErrorType.UnsupportedMVPExpression, TestName = "Integer Comparison Must Reject Boolean Operands")]
    public async Task BindShould_2(string expression, ErrorType expectedDiagnostic)
    {
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(expression);

        result.Functions
            .Should()
            .BeEmpty();

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(expectedDiagnostic));
    }

    [TestCase("value < other < 10", TestName = "Runtime Relational Chains Must Remain Non Associative")]
    [TestCase("value == other != 0", TestName = "Runtime Equality Chains Must Remain Non Associative")]
    public async Task BindShould_3(string expression)
    {
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(expression);

        result.Functions
            .Should()
            .BeEmpty();

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.NonAssociativeComparison));
    }

    [TestCase("value == other", false, 7, 7, true, TestName = "Native Runtime Equality True")]
    [TestCase("value == other", false, 7, 8, false, TestName = "Native Runtime Equality False")]
    [TestCase("value != other", false, 7, 8, true, TestName = "Native Runtime Inequality True")]
    [TestCase("value != other", false, 7, 7, false, TestName = "Native Runtime Inequality False")]
    [TestCase("value < other", false, -1, 0, true, TestName = "Native Signed Less Than Negative To Zero")]
    [TestCase("value < other", false, 1, -1, false, TestName = "Native Signed Less Than Positive To Negative")]
    [TestCase("value <= other", false, -5, -5, true, TestName = "Native Less Than Or Equal Boundary")]
    [TestCase("value <= other", false, 5, 4, false, TestName = "Native Less Than Or Equal False")]
    [TestCase("value > other", false, 0, -1, true, TestName = "Native Signed Greater Than Zero To Negative")]
    [TestCase("value > other", false, -5, 0, false, TestName = "Native Signed Greater Than False")]
    [TestCase("value >= other", false, 5, 5, true, TestName = "Native Greater Than Or Equal Boundary")]
    [TestCase("value >= other", false, -6, -5, false, TestName = "Native Greater Than Or Equal False")]
    [TestCase("value < other", false, int.MinValue, int.MaxValue, true, TestName = "Native Minimum Less Than Maximum")]
    [TestCase("value > other", false, int.MaxValue, int.MinValue, true, TestName = "Native Maximum Greater Than Minimum")]
    [TestCase("value == other", false, int.MinValue, int.MinValue, true, TestName = "Native Minimum Integer Equality")]
    [TestCase("value != other", false, int.MaxValue, int.MinValue, true, TestName = "Native Extreme Integer Inequality")]
    [TestCase("value < 2147483648", false, int.MaxValue, 0, true, TestName = "Native Exact Constant Above Int32")]
    [TestCase("2147483648 < value", false, int.MaxValue, 0, false, TestName = "Native Reversed Exact Constant Above Int32")]
    [TestCase("-2147483649 < value", false, int.MinValue, 0, true, TestName = "Native Exact Constant Below Int32")]
    [TestCase("value == 2147483648", false, int.MaxValue, 0, false, TestName = "Native Equality With Out Of Range Exact Constant")]
    [TestCase("(1 << 100) != value", false, int.MaxValue, 0, true, TestName = "Native Exact Large Constant Inequality")]
    [TestCase("value > -(1 << 100)", false, int.MinValue, 0, true, TestName = "Native Exact Large Negative Constant")]
    [TestCase("value >= (2147483648 - 1)", false, int.MaxValue, 0, true, TestName = "Native Exact In Range Boundary")]
    [TestCase("value >= (2147483648 - 1)", false, 20, 0, false, TestName = "Native Exact In Range Comparison False")]
    [TestCase("value < (2147483648 & 1)", false, -1, 0, true, TestName = "Native Exact Untyped Bitwise Comparison")]
    [TestCase("(value + 1) < 2147483648", false, 12, 0, true, TestName = "Native Checked Arithmetic Before Known Comparison")]
    [TestCase("value + 1 >= other", false, 40, 41, true, TestName = "Native Runtime Arithmetic And Comparison")]
    [TestCase("value + 1 >= other", false, 39, 41, false, TestName = "Native Runtime Arithmetic Comparison False")]
    [TestCase("value < (other + 2)", false, 8, 7, true, TestName = "Native Comparison With Runtime Right Arithmetic")]
    [TestCase("(value << 1) <= other", false, -4, -8, true, TestName = "Native Shift Composition With Signed Comparison")]
    [TestCase("(value & other) == 3", false, 7, 3, true, TestName = "Native Bitwise Composition With Comparison")]
    [TestCase("not (value < other)", false, 2, 1, true, TestName = "Native Negated Comparison")]
    [TestCase("not (value < other)", false, 1, 2, false, TestName = "Native Negated Comparison False")]
    [TestCase("(value < other) & enabled", true, 1, 2, true, TestName = "Native Eager Boolean And With Comparison")]
    [TestCase("(value < other) | enabled", true, 10, 2, true, TestName = "Native Eager Boolean Or With Comparison")]
    [TestCase("(value < other) ^ enabled", true, 1, 2, false, TestName = "Native Eager Boolean Xor With Comparison")]
    [TestCase("value < other and other < 10", false, 2, 5, true, TestName = "Native Nested Comparison With Logical And")]
    [TestCase("value < other and other < 10", false, 2, 12, false, TestName = "Native Nested Comparison With Logical And False")]
    [TestCase("(value > other) or (other == 0)", false, 5, 0, true, TestName = "Native Comparison With Logical Or")]
    [TestCase("(value > other) or (other == 0)", false, 1, 2, false, TestName = "Native Comparison With Logical Or False")]
    [TestCase("enabled and ((value * 2 + 5) >= 100)", true, 48, 0, true, TestName = "Native Final Vertical Compilation Example True")]
    [TestCase("enabled and ((value * 2 + 5) >= 100)", true, 47, 0, false, TestName = "Native Final Vertical Compilation Example False")]
    [TestCase("enabled and ((value * 2 + 5) >= 100)", false, int.MaxValue, 0, false, TestName = "Native Logical And Must Skip Overflow")]
    [TestCase("enabled or ((value + 1) > 0)", true, int.MaxValue, 0, true, TestName = "Native Logical Or Must Skip Overflow")]
    [TestCase("enabled and ((value / (other - other)) >= 0)", false, 100, 5, false, TestName = "Native Logical And Must Skip Division Failure")]
    [TestCase("enabled and ((value + 1) < 2147483648)", false, int.MaxValue, 0, false, TestName = "Native Short Circuit Must Skip Range Operand Overflow")]
    [TestCase("enabled or ((value + 1) < 2147483648)", true, int.MaxValue, 0, true, TestName = "Native Logical Or Must Skip Range Operand Overflow")]
    [TestCase("1 << 100 > 0", false, 0, 0, true, TestName = "Native Constant Comparison Must Remain Exact")]
    public async Task NativeShould_0(string expression, bool enabled, int value, int other, bool expected)
    {
        LoweredFunction lowered = await Lower(expression);

        using IModuleArtifact artifact = new LLVMModuleEmitter().Emit("RuntimeIntegerComparisonNative", lowered, CancellationToken.None);

        LLVMModuleArtifact llvmArtifact = artifact
            .Should()
            .BeOfType<LLVMModuleArtifact>()
            .Subject;

        AddTestOnlyCaller(llvmArtifact, lowered, enabled, value, other, expected);

        await ExecuteNative(llvmArtifact);
    }

    [TestCase(TestName = "Integer Comparison Instruction Must Reject Boolean Operand")]
    public void LowerShould_2()
    {
        LoweredTemporaryValue destination = new(0, IRType.Bool);

        Action action = () => _ = new LoweredIntegerComparisonInstruction(
            destination,
            IRIntegerComparisonOperator.Equal,
            new LoweredBooleanConstant(true),
            new LoweredIntegerConstant(1));

        action
            .Should()
            .Throw<ArgumentException>();
    }

    [TestCase(TestName = "Integer Comparison Instruction Must Reject Integer Destination")]
    public void LowerShould_3()
    {
        LoweredTemporaryValue destination = new(0, IRType.Int32);

        Action action = () => _ = new LoweredIntegerComparisonInstruction(
            destination,
            IRIntegerComparisonOperator.LessThan,
            new LoweredIntegerConstant(1),
            new LoweredIntegerConstant(2));

        action
            .Should()
            .Throw<ArgumentException>();
    }

    [TestCase(TestName = "Integer Comparison Instruction Must Reject Undefined Operator")]
    public void LowerShould_4()
    {
        LoweredTemporaryValue destination = new(0, IRType.Bool);

        Action action = () => _ = new LoweredIntegerComparisonInstruction(
            destination,
            (IRIntegerComparisonOperator)999,
            new LoweredIntegerConstant(1),
            new LoweredIntegerConstant(2));

        action
            .Should()
            .Throw<ArgumentOutOfRangeException>();
    }

    private static void AddTestOnlyCaller(LLVMModuleArtifact artifact, LoweredFunction function, bool enabled, int value, int other, bool expected)
    {
        LLVMModuleRef module = artifact.Module;
        LLVMContextRef context = module.Context;
        LLVMValueRef callee = module.GetNamedFunction(function.Name);

        LLVMTypeRef calleeType = LLVMTypeRef.CreateFunction(context.Int1Type, [context.Int1Type, context.Int32Type, context.Int32Type]);
        LLVMTypeRef mainType = LLVMTypeRef.CreateFunction(context.Int32Type, [context.Int32Type, context.CreatePointerType(0)]);
        LLVMValueRef main = module.AddFunction("main", mainType);
        LLVMBasicBlockRef entry = main.AppendBasicBlock("entry");

        using LLVMBuilderRef builder = context.CreateBuilder();

        builder.PositionAtEnd(entry);

        LLVMValueRef[] arguments =
        [
            LLVMValueRef.CreateConstInt(context.Int1Type, enabled ? 1UL : 0UL),
            LLVMValueRef.CreateConstInt(context.Int32Type, unchecked((ulong)(uint)value)),
            LLVMValueRef.CreateConstInt(context.Int32Type, unchecked((ulong)(uint)other))
        ];

        LLVMValueRef result = builder.BuildCall2(calleeType, callee, arguments, "sushi.test.result");
        LLVMValueRef expectedValue = LLVMValueRef.CreateConstInt(context.Int1Type, expected ? 1UL : 0UL);
        LLVMValueRef matches = builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, result, expectedValue, "sushi.test.matches");
        LLVMValueRef zero = LLVMValueRef.CreateConstInt(context.Int32Type, 0UL);
        LLVMValueRef one = LLVMValueRef.CreateConstInt(context.Int32Type, 1UL);

        builder.BuildRet(builder.BuildSelect(matches, zero, one, "sushi.test.exitcode"));

        module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message)
            .Should()
            .BeTrue(message);
    }

    private static async Task ExecuteNative(LLVMModuleArtifact artifact)
    {
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", Guid.NewGuid().ToString("N"));
        string objectPath = Path.Combine(directory, "runtime-integer-comparison.obj");
        string executablePath = Path.Combine(directory, "RuntimeIntegerComparison");

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

    private static async Task<BoundFunction> Bind(string expression)
    {
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(expression);

        diagnostics
            .Should()
            .BeEmpty();

        return result.Functions
            .Should()
            .ContainSingle()
            .Which;
    }

    private static async Task<LoweredFunction> Lower(string expression)
    {
        BoundFunction function = await Bind(expression);
        IRFunction structured = BoundFunctionIRConverter.Convert(function, CancellationToken.None);

        return IRFunctionLowerer.Lower(structured, CancellationToken.None);
    }

    private static async Task<(SourceAnalysisResult Result, IReadOnlyList<SushiDiagnostic> Diagnostics)> Analyze(string expression)
    {
        string source = $"package Example; namespace Example; public bool check(bool enabled, int32 value, int32 other) {{ return {expression}; }}";
        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);
        IDiagnosticReporter reporter = new DiagnosticReporter();

        SourceAnalysisResult result = (SourceAnalysisResult)await new SourceAnalyzer().Analyze(snapshot, reporter, CancellationToken.None);

        return (result, reporter.ReportDiagnostics());
    }
}
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
public sealed class RuntimeIntegerFoundationTests
{
    private static readonly Uri testUri = new("file:///TestProject/RuntimeIntegerFoundation.sus");

    [TestCase("public int32 echo(int32 value) { return value; }", 0, "value", TestName = "Binder Should Resolve First Integer Parameter")]
    [TestCase("public int32 second(int32 first, int32 second) { return second; }", 1, "second", TestName = "Binder Should Resolve Second Integer Parameter")]
    [TestCase("public int32 select(bool enabled, int32 first, int32 second) { return second; }", 2, "second", TestName = "Binder Should Preserve Integer Position In Mixed Signature")]
    [TestCase("public int32 echo(int32 @default) { return @default; }", 0, "default", TestName = "Binder Should Resolve Escaped Integer Identifier")]
    public async Task BindShould_0(string declaration, int expectedIndex, string expectedName)
    {
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(declaration);

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

        BoundParameterReferenceExpression reference = statement.Expression
            .Should()
            .BeOfType<BoundParameterReferenceExpression>()
            .Subject;

        reference.Parameter
            .Should()
            .BeSameAs(function.Parameters[expectedIndex]);

        reference.Parameter.Name
            .Should()
            .Be(expectedName);

        reference.Parameter.Index
            .Should()
            .Be(expectedIndex);

        reference.Type
            .Should()
            .Be(BoundType.Int32);

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase("public int32 echo(int32 value) { return missing; }", ErrorType.UnknownIdentifier, TestName = "Binder Should Diagnose Unknown Integer Reference")]
    [TestCase("public int32 echo(bool flag) { return flag; }", ErrorType.UnsupportedMVPExpression, TestName = "Binder Should Reject Boolean As Integer")]
    [TestCase("public int32 echo(int32 value) { return value + true; }", ErrorType.UnsupportedMVPExpression, TestName = "Binder Should Reject Boolean Arithmetic Operand")]
    [TestCase("public int32 echo(int32 value) { return value + 2147483648; }", ErrorType.IntegerConstantOutOfRange, TestName = "Binder Should Reject Out Of Range Contextual Constant")]
    [TestCase("public int32 echo(int32 value) { return value / (2 - 2); }", ErrorType.IntegerDivisionByZero, TestName = "Binder Should Diagnose Static Zero Divisor")]
    [TestCase("public int32 echo(int32 value) { return value << 32; }", ErrorType.InvalidIntegerShiftCount, TestName = "Binder Should Diagnose Out Of Range Shift Count")]
    [TestCase("public int32 echo(int32 value) { return value >> -1; }", ErrorType.InvalidIntegerShiftCount, TestName = "Binder Should Diagnose Negative Shift Count")]
    [TestCase("public int32 echo(int32 value, int32 value) { return value; }", ErrorType.DuplicateParameter, TestName = "Binder Should Reject Duplicate Integer Parameter")]
    [TestCase("public int32 echo(int32 value, bool value) { return value; }", ErrorType.DuplicateParameter, TestName = "Binder Should Reject Duplicate Mixed Parameter")]
    public async Task BindShould_1(string declaration, ErrorType expectedDiagnostic)
    {
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(declaration);

        result.Functions
            .Should()
            .BeEmpty();

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(expectedDiagnostic));
    }

    [TestCase("+value", IntegerUnaryOperator.Plus, IRIntegerUnaryOperator.Plus, TestName = "Unary Plus Should Reach Structured IR")]
    [TestCase("-value", IntegerUnaryOperator.Negate, IRIntegerUnaryOperator.Negate, TestName = "Unary Negation Should Reach Structured IR")]
    [TestCase("~value", IntegerUnaryOperator.Complement, IRIntegerUnaryOperator.Complement, TestName = "Bitwise Complement Should Reach Structured IR")]
    public async Task ConvertShould_0(string expression, IntegerUnaryOperator expectedBoundOperator, IRIntegerUnaryOperator expectedIROperator)
    {
        BoundFunction bound = await Bind($"public int32 calculate(int32 value) {{ return {expression}; }}");

        BoundReturnStatement boundReturn = bound.Body.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<BoundReturnStatement>()
            .Subject;

        BoundIntegerUnaryExpression boundUnary = boundReturn.Expression
            .Should()
            .BeOfType<BoundIntegerUnaryExpression>()
            .Subject;

        boundUnary.Operator
            .Should()
            .Be(expectedBoundOperator);

        boundUnary.Operand
            .Should()
            .BeOfType<BoundParameterReferenceExpression>();

        IRFunction structured = BoundFunctionIRConverter.Convert(bound, CancellationToken.None);

        IRReturnStatement irReturn = structured.Body.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<IRReturnStatement>()
            .Subject;

        IRIntegerUnaryExpression irUnary = irReturn.Expression
            .Should()
            .BeOfType<IRIntegerUnaryExpression>()
            .Subject;

        irUnary.Operator
            .Should()
            .Be(expectedIROperator);

        irUnary.Operand
            .Should()
            .BeOfType<IRParameterReferenceExpression>();
    }

    [TestCase("value + 1", IntegerBinaryOperator.Add, IRIntegerBinaryOperator.Add)]
    [TestCase("value - 1", IntegerBinaryOperator.Subtract, IRIntegerBinaryOperator.Subtract)]
    [TestCase("value * 2", IntegerBinaryOperator.Multiply, IRIntegerBinaryOperator.Multiply)]
    [TestCase("value / 2", IntegerBinaryOperator.Divide, IRIntegerBinaryOperator.Divide)]
    [TestCase("value % 2", IntegerBinaryOperator.Remainder, IRIntegerBinaryOperator.Remainder)]
    [TestCase("value & 3", IntegerBinaryOperator.BitwiseAnd, IRIntegerBinaryOperator.BitwiseAnd)]
    [TestCase("value | 3", IntegerBinaryOperator.BitwiseOr, IRIntegerBinaryOperator.BitwiseOr)]
    [TestCase("value ^ 3", IntegerBinaryOperator.BitwiseXor, IRIntegerBinaryOperator.BitwiseXor)]
    [TestCase("value << 1", IntegerBinaryOperator.ShiftLeft, IRIntegerBinaryOperator.ShiftLeft)]
    [TestCase("value >> 1", IntegerBinaryOperator.ShiftRight, IRIntegerBinaryOperator.ShiftRight)]
    public async Task ConvertShould_1(string expression, IntegerBinaryOperator expectedBoundOperator, IRIntegerBinaryOperator expectedIROperator)
    {
        BoundFunction bound = await Bind($"public int32 calculate(int32 value) {{ return {expression}; }}");

        BoundReturnStatement boundReturn = bound.Body.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<BoundReturnStatement>()
            .Subject;

        BoundIntegerBinaryExpression boundBinary = boundReturn.Expression
            .Should()
            .BeOfType<BoundIntegerBinaryExpression>()
            .Subject;

        boundBinary.Operator
            .Should()
            .Be(expectedBoundOperator);

        boundBinary.Left
            .Should()
            .BeOfType<BoundParameterReferenceExpression>();

        boundBinary.Right
            .Should()
            .BeOfType<BoundIntegerLiteralExpression>();

        IRFunction structured = BoundFunctionIRConverter.Convert(bound, CancellationToken.None);

        IRReturnStatement irReturn = structured.Body.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<IRReturnStatement>()
            .Subject;

        IRIntegerBinaryExpression irBinary = irReturn.Expression
            .Should()
            .BeOfType<IRIntegerBinaryExpression>()
            .Subject;

        irBinary.Operator
            .Should()
            .Be(expectedIROperator);

        irBinary.Left
            .Should()
            .BeOfType<IRParameterReferenceExpression>();

        irBinary.Right
            .Should()
            .BeOfType<IRIntegerConstant>();
    }

    [TestCase("value + (2147483647 + 1 - 1)", 2147483647, TestName = "Runtime Arithmetic Should Preserve Exact Constant Subexpression")]
    [TestCase("value + (2147483648 - 2147483647)", 1, TestName = "Runtime Arithmetic Should Accept Exact Intermediate Outside Int32")]
    [TestCase("value << (2147483648 & 1)", 0, TestName = "Runtime Shift Count Should Preserve Untyped Exact Evaluation")]
    [TestCase("value << (2147483648 & 3)", 0, TestName = "Runtime Shift Count Should Not Prematurely Type Untyped Bitwise Operands")]
    public async Task BindShould_2(string expression, int expectedConstant)
    {
        BoundFunction bound = await Bind($"public int32 calculate(int32 value) {{ return {expression}; }}");

        BoundReturnStatement result = bound.Body.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<BoundReturnStatement>()
            .Subject;

        BoundIntegerBinaryExpression binary = result.Expression
            .Should()
            .BeOfType<BoundIntegerBinaryExpression>()
            .Subject;

        BoundIntegerLiteralExpression constant = binary.Right
            .Should()
            .BeOfType<BoundIntegerLiteralExpression>()
            .Subject;

        constant.Value
            .Should()
            .Be(expectedConstant);
    }

    [TestCase(TestName = "Mixed Parameter Types Should Survive Every Compilation Stage")]
    public async Task ConvertShould_2()
    {
        BoundFunction bound = await Bind("public int32 choose(bool enabled, int32 first, int32 second) { return second; }");

        bound.Parameters
            .Select(parameter => parameter.Type)
            .Should()
            .Equal(BoundType.Bool, BoundType.Int32, BoundType.Int32);

        IRFunction structured = BoundFunctionIRConverter.Convert(bound, CancellationToken.None);

        structured.Parameters
            .Select(parameter => parameter.Type)
            .Should()
            .Equal(IRType.Bool, IRType.Int32, IRType.Int32);

        IRReturnStatement irReturn = structured.Body.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<IRReturnStatement>()
            .Subject;

        IRParameterReferenceExpression reference = irReturn.Expression
            .Should()
            .BeOfType<IRParameterReferenceExpression>()
            .Subject;

        reference.Index
            .Should()
            .Be(2);

        reference.Type
            .Should()
            .Be(IRType.Int32);

        LoweredFunction lowered = IRFunctionLowerer.Lower(structured, CancellationToken.None);

        lowered.Parameters
            .Select(parameter => parameter.Type)
            .Should()
            .Equal(IRType.Bool, IRType.Int32, IRType.Int32);

        LoweredReturnTerminator terminator = lowered.EntryBlock.Terminator
            .Should()
            .BeOfType<LoweredReturnTerminator>()
            .Subject;

        LoweredParameterValue value = terminator.Value
            .Should()
            .BeOfType<LoweredParameterValue>()
            .Subject;

        value.Index
            .Should()
            .Be(2);

        value.Type
            .Should()
            .Be(IRType.Int32);

        using LLVMModuleArtifact artifact = new LLVMModuleEmitter().Emit("MixedIntegerParameters", lowered, CancellationToken.None);

        artifact.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message)
            .Should()
            .BeTrue(message);

        string llvm = artifact.Module.PrintToString();

        llvm
            .Should()
            .Contain("define i32 @choose(i1 %enabled, i32 %first, i32 %second)");

        llvm
            .Should()
            .Contain("ret i32 %second");
    }

    [TestCase("~value", typeof(LoweredIntegerComplementInstruction), TestName = "Runtime Complement Should Lower To Bitwise Complement")]
    [TestCase("value & 1", typeof(LoweredIntegerBitwiseInstruction), TestName = "Runtime Bitwise And Should Lower")]
    [TestCase("value | 1", typeof(LoweredIntegerBitwiseInstruction), TestName = "Runtime Bitwise Or Should Lower")]
    [TestCase("value ^ 1", typeof(LoweredIntegerBitwiseInstruction), TestName = "Runtime Bitwise Xor Should Lower")]
    [TestCase("value << 1", typeof(LoweredIntegerShiftInstruction), TestName = "Runtime Left Shift Should Lower")]
    [TestCase("value >> 1", typeof(LoweredIntegerShiftInstruction), TestName = "Runtime Right Shift Should Lower")]
    public async Task LowerShould_0(string expression, Type expectedInstructionType)
    {
        BoundFunction bound = await Bind($"public int32 calculate(int32 value) {{ return {expression}; }}");
        IRFunction structured = BoundFunctionIRConverter.Convert(bound, CancellationToken.None);
        LoweredFunction lowered = IRFunctionLowerer.Lower(structured, CancellationToken.None);

        lowered.EntryBlock.Instructions
            .Should()
            .ContainSingle();

        lowered.EntryBlock.Instructions[0].GetType()
            .Should()
            .Be(expectedInstructionType);
    }

    [TestCase("public int32 echo(int32 value) { return value; }", 42, 0, false, 42, TestName = "Native Should Return Direct Integer Parameter")]
    [TestCase("public int32 echo(int32 value) { return value; }", 0, 0, false, 0, TestName = "Native Should Return Zero Integer Parameter")]
    [TestCase("public int32 echo(int32 value) { return value; }", 255, 0, false, 255, TestName = "Native Should Return Upper Exit Code")]
    [TestCase("public int32 second(int32 first, int32 second) { return second; }", 17, 93, false, 93, TestName = "Native Should Return Second Integer Parameter")]
    [TestCase("public int32 choose(bool enabled, int32 first, int32 second) { return second; }", 17, 93, true, 93, TestName = "Native Should Preserve Mixed Parameter ABI")]
    public async Task NativeShould_0(string declaration, int first, int second, bool enabled, int expectedExitCode)
    {
        BoundFunction bound = await Bind(declaration);
        LoweredFunction lowered = IRFunctionLowerer.Lower(BoundFunctionIRConverter.Convert(bound, CancellationToken.None), CancellationToken.None);

        using LLVMModuleArtifact artifact = new LLVMModuleEmitter().Emit("RuntimeIntegerNative", lowered, CancellationToken.None);

        AddTestOnlyCaller(artifact, lowered, first, second, enabled);

        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", Guid.NewGuid().ToString("N"));
        string objectPath = Path.Combine(directory, "runtime-integer.obj");
        string executablePath = Path.Combine(directory, "RuntimeInteger");

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
                .Be(expectedExitCode);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestCase(TestName = "Integer Binary IR Should Reject Boolean Operand")]
    public void ConvertShould_3()
    {
        IRIntegerConstant left = new(1);
        IRBooleanConstant right = new(true);

        Action action = () => _ = new IRIntegerBinaryExpression(IRIntegerBinaryOperator.Add, left, right);

        action
            .Should()
            .Throw<ArgumentException>();
    }

    [TestCase(TestName = "Integer Unary IR Should Reject Boolean Operand")]
    public void ConvertShould_4()
    {
        IRBooleanConstant operand = new(true);

        Action action = () => _ = new IRIntegerUnaryExpression(IRIntegerUnaryOperator.Negate, operand);

        action
            .Should()
            .Throw<ArgumentException>();
    }

    private static void AddTestOnlyCaller(LLVMModuleArtifact artifact, LoweredFunction function, int first, int second, bool enabled)
    {
        LLVMModuleRef module = artifact.Module;
        LLVMContextRef context = module.Context;
        LLVMValueRef callee = module.GetNamedFunction(function.Name);

        LLVMTypeRef[] parameterTypes =
        [
            .. function.Parameters.Select(parameter => parameter.Type switch
            {
                IRType.Bool => context.Int1Type,
                IRType.Int32 => context.Int32Type,
                _ => throw new NotSupportedException()
            })
        ];

        LLVMValueRef[] arguments =
        [
            .. function.Parameters.Select(parameter => parameter.Name switch
            {
                "enabled" => LLVMValueRef.CreateConstInt(context.Int1Type, enabled ? 1UL : 0UL),
                "value" or "first" => LLVMValueRef.CreateConstInt(context.Int32Type, unchecked((ulong)(uint)first)),
                "second" => LLVMValueRef.CreateConstInt(context.Int32Type, unchecked((ulong)(uint)second)),
                _ => throw new NotSupportedException($"Unexpected integration-test parameter \"{parameter.Name}\".")
            })
        ];

        LLVMTypeRef calleeType = LLVMTypeRef.CreateFunction(context.Int32Type, parameterTypes);
        LLVMTypeRef mainType = LLVMTypeRef.CreateFunction(context.Int32Type, [context.Int32Type, context.CreatePointerType(0)]);
        LLVMValueRef main = module.AddFunction("main", mainType);
        LLVMBasicBlockRef entry = main.AppendBasicBlock("entry");

        using LLVMBuilderRef builder = context.CreateBuilder();

        builder.PositionAtEnd(entry);

        LLVMValueRef result = builder.BuildCall2(calleeType, callee, arguments, "sushi.test.result");

        builder.BuildRet(result);

        module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message)
            .Should()
            .BeTrue(message);
    }

    private static async Task<BoundFunction> Bind(string declaration)
    {
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(declaration);

        diagnostics
            .Should()
            .BeEmpty();

        return result.Functions
            .Should()
            .ContainSingle()
            .Which;
    }

    private static async Task<(SourceAnalysisResult Result, IReadOnlyList<SushiDiagnostic> Diagnostics)> Analyze(string declaration)
    {
        string source = $"package Example; namespace Example; {declaration}";
        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);
        IDiagnosticReporter reporter = new DiagnosticReporter();

        SourceAnalysisResult result = (SourceAnalysisResult)await new SourceAnalyzer().Analyze(snapshot, reporter, CancellationToken.None);

        return (result, reporter.ReportDiagnostics());
    }
}
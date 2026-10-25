using FluentAssertions;
using LLVMSharp.Interop;
using NUnit.Framework;
using Sushi.Analysis;
using Sushi.Backends.LLVM;
using Sushi.Diagnostics;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.Testing.Semantics;

[TestFixture]
public class IntegerConstantExpressionBinderTests
{
    private static readonly Uri testUri = new("file:///TestProject/ConstantArithmetic.sus");

    [TestCase("42", 42, TestName = "Binder Should Preserve Existing Integer Literal Support")]
    [TestCase("6 * 7", 42, TestName = "Binder Should Evaluate Multiplication")]
    [TestCase("(6 + 1) * 3 - 2", 19, TestName = "Binder Should Evaluate Nested Arithmetic")]
    [TestCase("20 / 3", 6, TestName = "Binder Should Truncate Integer Division")]
    [TestCase("-7 / 3", -2, TestName = "Binder Should Truncate Negative Division Toward Zero")]
    [TestCase("-7 % 3", -1, TestName = "Binder Should Preserve Dividend Sign For Remainder")]
    [TestCase("7 % -3", 1, TestName = "Binder Should Evaluate Remainder With Negative Divisor")]
    [TestCase("-2147483648", int.MinValue, TestName = "Binder Should Accept Minimum Int32 Constant")]
    [TestCase("2147483647", int.MaxValue, TestName = "Binder Should Accept Maximum Int32 Constant")]
    [TestCase("2147483647 + 1 - 1", int.MaxValue, TestName = "Binder Should Retain Exact Intermediate Values")]
    [TestCase("-(20 - 62)", 42, TestName = "Binder Should Evaluate Parenthesized Unary Negation")]
    [TestCase("+42", 42, TestName = "Binder Should Evaluate Unary Plus")]
    [TestCase("x#2A", 42, TestName = "Binder Should Preserve Radix Integer Literal Support")]
    [TestCase("2 * (3 + 4)", 14, TestName = "Binder Should Respect Explicit Parentheses")]
    public async Task BindShould_0(string expression, int expectedValue)
    {
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalyzeExpression(expression);

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

        BoundIntegerLiteralExpression boundExpression = statement.Expression
            .Should()
            .BeOfType<BoundIntegerLiteralExpression>()
            .Subject;

        boundExpression.Type
            .Should()
            .Be(BoundType.Int32);

        boundExpression.Value
            .Should()
            .Be(expectedValue);

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase("2147483647 + 1", TestName = "Binder Should Reject Positive Int32 Overflow")]
    [TestCase("-2147483649", TestName = "Binder Should Reject Negative Int32 Overflow")]
    [TestCase("2147483648", TestName = "Binder Should Preserve Existing Out Of Range Literal Diagnostic")]
    [TestCase("-(-2147483648)", TestName = "Binder Should Reject Negation Outside Int32 Range")]
    [TestCase("100000 * 100000", TestName = "Binder Should Reject Out Of Range Multiplication")]
    public async Task BindShould_1(string expression)
    {
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalyzeExpression(expression);

        result.Functions
            .Should()
            .BeEmpty();

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.IntegerConstantOutOfRange));
    }

    [TestCase("1 / 0", TestName = "Binder Should Diagnose Division By Zero")]
    [TestCase("1 % 0", TestName = "Binder Should Diagnose Remainder By Zero")]
    [TestCase("1 / (2 - 2)", TestName = "Binder Should Diagnose Computed Zero Divisor")]
    public async Task BindShould_2(string expression)
    {
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalyzeExpression(expression);

        result.Functions
            .Should()
            .BeEmpty();

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.IntegerDivisionByZero));
    }

    [TestCase("true", TestName = "Binder Should Retain Unsupported Boolean Literal Boundary")]
    [TestCase("not false", TestName = "Binder Should Retain Unsupported Logical Negation Boundary")]
    [TestCase("1 << 2", TestName = "Binder Should Retain Unsupported Shift Boundary")]
    [TestCase("1 & 2", TestName = "Binder Should Retain Unsupported Bitwise Boundary")]
    [TestCase("1 == 2", TestName = "Binder Should Retain Unsupported Comparison Boundary")]
    [TestCase("1 + true", TestName = "Binder Should Reject Unsupported Operand Within Arithmetic")]
    [TestCase("~1", TestName = "Binder Should Retain Unsupported Complement Boundary")]
    public async Task BindShould_3(string expression)
    {
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalyzeExpression(expression);

        result.Functions
            .Should()
            .BeEmpty();

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.UnsupportedMVPExpression));
    }

    [TestCase(TestName = "Binder Should Not Add Unsupported Expression Diagnostic For Recovered Missing Operand")]
    public async Task BindShould_4()
    {
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalyzeExpression("1 +");

        result.Functions
            .Should()
            .BeEmpty();

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));
    }

    [TestCase(TestName = "Exact Arithmetic Should Reach Structured IR And LLVM As Concrete Int32")]
    public async Task BindShould_5()
    {
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalyzeExpression("(6 + 1) * 3 - 2");

        BoundFunction boundFunction = result.Functions
            .Should()
            .ContainSingle()
            .Which;

        IRFunction structured = BoundFunctionIRConverter.Convert(boundFunction, CancellationToken.None);

        IRReturnStatement structuredReturn = structured.Body.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<IRReturnStatement>()
            .Subject;

        IRIntegerConstant constant = structuredReturn.Expression
            .Should()
            .BeOfType<IRIntegerConstant>()
            .Subject;

        constant.Value
            .Should()
            .Be(19);

        LoweredFunction lowered = IRFunctionLowerer.Lower(structured, CancellationToken.None);

        LoweredReturnTerminator terminator = lowered.EntryBlock.Terminator
            .Should()
            .BeOfType<LoweredReturnTerminator>()
            .Subject;

        LoweredIntegerConstant loweredConstant = terminator.Value
            .Should()
            .BeOfType<LoweredIntegerConstant>()
            .Subject;

        loweredConstant.Value
            .Should()
            .Be(19);

        using LLVMModuleArtifact artifact = new LLVMModuleEmitter().Emit("ConstantArithmetic", lowered, CancellationToken.None);

        artifact.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string verificationMessage)
            .Should()
            .BeTrue(verificationMessage);

        artifact.Module.PrintToString()
            .Should()
            .Contain("ret i32 19");

        diagnostics
            .Should()
            .BeEmpty();
    }

    private static async Task<(SourceAnalysisResult Result, IReadOnlyList<SushiDiagnostic> Diagnostics)> AnalyzeExpression(string expression)
    {
        string source = $$"""
            package Example;
            namespace Example;

            public int32 launch() {
                return {{expression}};
            }
            """;

        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);
        IDiagnosticReporter reporter = new DiagnosticReporter();

        SourceAnalysisResult result = (SourceAnalysisResult)await new SourceAnalyzer().Analyze(snapshot, reporter, CancellationToken.None);

        return (result, reporter.ReportDiagnostics());
    }
}
using FluentAssertions;
using NUnit.Framework;
using Sushi.Analysis;
using Sushi.Diagnostics;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.Testing.Semantics;

[TestFixture]
public class IntegerBitwiseShiftConstantTests
{
    private static readonly Uri testUri = new("file:///TestProject/BitwiseShiftConstants.sus");

    [TestCase("5 & 3", 1, TestName = "Binder Should Evaluate Integer Bitwise And")]
    [TestCase("5 | 8", 13, TestName = "Binder Should Evaluate Integer Bitwise Or")]
    [TestCase("7 ^ 3", 4, TestName = "Binder Should Evaluate Integer Bitwise Xor")]
    [TestCase("-1 & 5", 5, TestName = "Binder Should Apply Signed Bitwise And")]
    [TestCase("-1 | 5", -1, TestName = "Binder Should Apply Signed Bitwise Or")]
    [TestCase("-5 ^ 3", -8, TestName = "Binder Should Apply Signed Bitwise Xor")]
    [TestCase("~0", -1, TestName = "Binder Should Complement Zero")]
    [TestCase("~(-1)", 0, TestName = "Binder Should Complement Negative One")]
    [TestCase("~~5", 5, TestName = "Binder Should Apply Nested Complement")]
    [TestCase("~2147483647", int.MinValue, TestName = "Binder Should Complement Maximum Int32")]
    [TestCase("~(-2147483648)", int.MaxValue, TestName = "Binder Should Complement Minimum Int32")]
    [TestCase("1 << 3", 8, TestName = "Binder Should Evaluate Left Shift")]
    [TestCase("16 >> 2", 4, TestName = "Binder Should Evaluate Right Shift")]
    [TestCase("-7 >> 1", -4, TestName = "Binder Should Use Arithmetic Right Shift")]
    [TestCase("-1 >> 31", -1, TestName = "Binder Should Preserve Sign On Maximum Right Shift")]
    [TestCase("-1 << 31", int.MinValue, TestName = "Binder Should Permit Representable Negative Left Shift")]
    [TestCase("0 << 31", 0, TestName = "Binder Should Permit Maximum Valid Shift Count")]
    [TestCase("1 << (2 + 1)", 8, TestName = "Binder Should Evaluate Arithmetic Shift Count")]
    [TestCase("8 >> (1 << 1)", 2, TestName = "Binder Should Evaluate Shift Expression Used As Count")]
    [TestCase("1 << ((1 << 31) >> 30)", 4, TestName = "Binder Should Evaluate Nested Untyped Shift Count Exactly")]
    [TestCase("1 << 2 & 3", 0, TestName = "Binder Should Respect Shift Before Bitwise And")]
    [TestCase("1 | 2 ^ 3 & 1", 3, TestName = "Binder Should Respect Bitwise Precedence")]
    [TestCase("8 >> 1 + 1", 2, TestName = "Binder Should Respect Addition Before Shift")]
    [TestCase("(2147483647 + 1 - 1) & 7", 7, TestName = "Binder Should Preserve Exact Arithmetic Before Bitwise Typing")]
    public async Task BindShould_0(string expression, int expectedValue)
    {
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalyzeExpression(expression);

        BoundFunction function = result.Functions
            .Should()
            .ContainSingle()
            .Which;

        function.ReturnType
            .Should()
            .Be(BoundType.Int32);

        BoundReturnStatement statement = function.Body.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<BoundReturnStatement>()
            .Subject;

        BoundIntegerLiteralExpression constant = statement.Expression
            .Should()
            .BeOfType<BoundIntegerLiteralExpression>()
            .Subject;

        constant.Value
            .Should()
            .Be(expectedValue);

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase("1 << 32", TestName = "Binder Should Reject Shift Count Equal To Width")]
    [TestCase("0 << 32", TestName = "Binder Should Reject Invalid Count Even When Left Value Is Zero")]
    [TestCase("1 >> -1", TestName = "Binder Should Reject Negative Right Shift Count")]
    [TestCase("1 << (1 << 5)", TestName = "Binder Should Reject Computed Count Equal To Width")]
    [TestCase("1 << (1 << 31)", TestName = "Binder Should Reject Oversized Untyped Shift Count")]
    [TestCase("1 << 2147483648", TestName = "Binder Should Reject Large Untyped Count Without Narrowing")]
    [TestCase("1 >> (~0)", TestName = "Binder Should Reject Negative Count From Exact Complement")]
    public async Task BindShould_1(string expression)
    {
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalyzeExpression(expression);

        result.Functions
            .Should()
            .BeEmpty();

        diagnostics
            .Should()
            .ContainSingle(item => item.IsType(ErrorType.InvalidIntegerShiftCount));
    }

    [TestCase("1 << 31", TestName = "Binder Should Reject Positive Left Shift Overflow")]
    [TestCase("-2 << 31", TestName = "Binder Should Reject Negative Left Shift Overflow")]
    [TestCase("(1 << 30) << 1", TestName = "Binder Should Reject Nested Left Shift Overflow")]
    [TestCase("2147483648 & 0", TestName = "Binder Should Not Mask An Out Of Range Operand")]
    [TestCase("1 ^ 2147483648", TestName = "Binder Should Reject Out Of Range Xor Operand")]
    [TestCase("~2147483648", TestName = "Binder Should Reject Out Of Range Complement Operand")]
    [TestCase("(2147483647 + 1) & 0", TestName = "Binder Should Reject Out Of Range Constant Bitwise Operand")]
    public async Task BindShould_2(string expression)
    {
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalyzeExpression(expression);

        result.Functions
            .Should()
            .BeEmpty();

        diagnostics
            .Should()
            .ContainSingle(item => item.IsType(ErrorType.IntegerConstantOutOfRange));
    }

    [TestCase(TestName = "Binder Should Report Invalid Shift Count At Count Expression")]
    public async Task BindShould_3()
    {
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalyzeExpression("1 << 32");

        result.Functions
            .Should()
            .BeEmpty();

        SushiDiagnostic diagnostic = diagnostics
            .Should()
            .ContainSingle()
            .Which;

        diagnostic.IsType(ErrorType.InvalidIntegerShiftCount)
            .Should()
            .BeTrue();

        SourceSpan span = diagnostic.Span;
        string spelling = System.Text.Encoding.UTF8.GetString(span.Snapshot.Bytes.Span[span.Start..span.End]);

        spelling
            .Should()
            .Be("32");
    }

    [TestCase("1 <<", TestName = "Binder Should Preserve Missing Shift Count Diagnostic")]
    [TestCase("1 &", TestName = "Binder Should Preserve Missing Bitwise Operand Diagnostic")]
    [TestCase("~", TestName = "Binder Should Preserve Missing Complement Operand Diagnostic")]
    public async Task BindShould_4(string expression)
    {
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalyzeExpression(expression);

        result.Functions
            .Should()
            .BeEmpty();

        diagnostics
            .Should()
            .ContainSingle(item => item.IsType(ErrorType.ExpectedSyntax));
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
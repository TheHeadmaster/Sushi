using FluentAssertions;
using NUnit.Framework;
using Sushi.Analysis;
using Sushi.Diagnostics;
using Sushi.Semantics;
using Sushi.Source;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;

namespace Sushi.Testing.Semantics;

[TestFixture]
public class BooleanBinaryConstantTests
{
    private static readonly Uri testUri = new("file:///TestProject/BooleanBinaryConstants.sus");

    [TestCase("true and true", true, TestName = "Binder Should Evaluate True Logical And")]
    [TestCase("true and false", false, TestName = "Binder Should Evaluate False Logical And")]
    [TestCase("false and true", false, TestName = "Binder Should Evaluate Logical And With False Left Operand")]
    [TestCase("false and false", false, TestName = "Binder Should Evaluate False Logical And Operands")]
    [TestCase("true or true", true, TestName = "Binder Should Evaluate True Logical Or")]
    [TestCase("true or false", true, TestName = "Binder Should Evaluate Logical Or With True Left Operand")]
    [TestCase("false or true", true, TestName = "Binder Should Evaluate Logical Or With True Right Operand")]
    [TestCase("false or false", false, TestName = "Binder Should Evaluate False Logical Or")]
    [TestCase("true & false", false, TestName = "Binder Should Evaluate Eager Boolean And")]
    [TestCase("true & true", true, TestName = "Binder Should Evaluate Eager Boolean And With True Operands")]
    [TestCase("false | true", true, TestName = "Binder Should Evaluate Eager Boolean Or")]
    [TestCase("false | false", false, TestName = "Binder Should Evaluate Eager Boolean Or With False Operands")]
    [TestCase("true ^ false", true, TestName = "Binder Should Evaluate Eager Boolean Xor")]
    [TestCase("true ^ true", false, TestName = "Binder Should Evaluate Eager Boolean Xor With Equal Operands")]
    [TestCase("false ^ false", false, TestName = "Binder Should Evaluate Eager Boolean Xor With False Operands")]
    [TestCase("not false and true", true, TestName = "Binder Should Respect Negation Before Logical And")]
    [TestCase("false or true and false", false, TestName = "Binder Should Respect Logical And Before Or")]
    [TestCase("true or false and false", true, TestName = "Binder Should Respect Logical Operator Precedence")]
    [TestCase("true | false and false", false, TestName = "Binder Should Respect Eager Or Before Logical And")]
    [TestCase("false and true | true", false, TestName = "Binder Should Respect Eager Or Before Logical And With False Left Operand")]
    [TestCase("true | false ^ true & false", true, TestName = "Binder Should Respect Eager Boolean Bitwise Precedence")]
    [TestCase("false | true ^ true", false, TestName = "Binder Should Respect Eager Xor Before Or")]
    [TestCase("(true or false) and true", true, TestName = "Binder Should Respect Parenthesized Logical Expression")]
    [TestCase("not (true and false)", true, TestName = "Binder Should Negate Logical Conjunction")]
    [TestCase("not (true ^ false)", false, TestName = "Binder Should Negate Eager Xor")]
    [TestCase("1 < 2 and 3 > 2", true, TestName = "Binder Should Combine Integer Comparisons With Logical And")]
    [TestCase("1 > 2 or 3 > 2", true, TestName = "Binder Should Combine Integer Comparisons With Logical Or")]
    [TestCase("(1 << 100) > 0 and true", true, TestName = "Binder Should Combine Exact Untyped Comparison With Boolean Logic")]
    [TestCase("(10 > 5 and 3 < 7) ^ false", true, TestName = "Binder Should Evaluate Mixed Boolean Operations")]
    public async Task BindShould_0(string expression, bool expectedValue)
    {
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalyzeExpression(expression);

        BoundFunction function = result.Functions
            .Should()
            .ContainSingle()
            .Which;

        function.ReturnType
            .Should()
            .Be(BoundType.Bool);

        BoundReturnStatement statement = function.Body.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<BoundReturnStatement>()
            .Subject;

        BoundBooleanConstantExpression constant = statement.Expression
            .Should()
            .BeOfType<BoundBooleanConstantExpression>()
            .Subject;

        constant.Type
            .Should()
            .Be(BoundType.Bool);

        constant.Value
            .Should()
            .Be(expectedValue);

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase("false and 1", TestName = "Binder Should Reject Integer Right Operand Of Logical And")]
    [TestCase("true or 1", TestName = "Binder Should Reject Integer Right Operand Of Logical Or")]
    [TestCase("1 and true", TestName = "Binder Should Reject Integer Left Operand Of Logical And")]
    [TestCase("true & 1", TestName = "Binder Should Reject Integer Operand Of Eager And")]
    [TestCase("false | 1", TestName = "Binder Should Reject Integer Operand Of Eager Or")]
    [TestCase("true ^ 1", TestName = "Binder Should Reject Integer Operand Of Eager Xor")]
    [TestCase("not (true & 1)", TestName = "Binder Should Reject Nested Integer Operand Of Boolean Operation")]
    public async Task BindShould_1(string expression)
    {
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalyzeExpression(expression);

        result.Functions
            .Should()
            .BeEmpty();

        diagnostics
            .Should()
            .ContainSingle(item => item.IsType(ErrorType.UnsupportedMVPExpression));
    }

    [TestCase("false and (1 / 0 == 0)", ErrorType.IntegerDivisionByZero, TestName = "Binder Should Diagnose Static Arithmetic Error In Short Circuited And Operand")]
    [TestCase("true or (1 << -1 == 0)", ErrorType.InvalidIntegerShiftCount, TestName = "Binder Should Diagnose Invalid Shift In Short Circuited Or Operand")]
    [TestCase("false & (1 / 0 == 0)", ErrorType.IntegerDivisionByZero, TestName = "Binder Should Diagnose Arithmetic Error In Eager And Operand")]
    [TestCase("true | (1 / 0 == 0)", ErrorType.IntegerDivisionByZero, TestName = "Binder Should Diagnose Arithmetic Error In Eager Or Operand")]
    public async Task BindShould_2(string expression, ErrorType expectedError)
    {
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalyzeExpression(expression);

        result.Functions
            .Should()
            .BeEmpty();

        diagnostics
            .Should()
            .ContainSingle(item => item.IsType(expectedError));
    }

    [TestCase("false and (1 < 2 < 3)", TestName = "Binder Should Preserve Comparison Chain Diagnostic In Logical And")]
    [TestCase("true or (1 == 2 != 3)", TestName = "Binder Should Preserve Comparison Chain Diagnostic In Logical Or")]
    public async Task BindShould_3(string expression)
    {
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalyzeExpression(expression);

        result.Functions
            .Should()
            .BeEmpty();

        diagnostics
            .Should()
            .ContainSingle(item => item.IsType(ErrorType.NonAssociativeComparison));
    }

    [TestCase("true and", TestName = "Binder Should Preserve Missing Logical And Operand Diagnostic")]
    [TestCase("false or", TestName = "Binder Should Preserve Missing Logical Or Operand Diagnostic")]
    [TestCase("true &", TestName = "Binder Should Preserve Missing Eager And Operand Diagnostic")]
    [TestCase("false |", TestName = "Binder Should Preserve Missing Eager Or Operand Diagnostic")]
    [TestCase("true ^", TestName = "Binder Should Preserve Missing Eager Xor Operand Diagnostic")]
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

            public bool launch() {
                return {{expression}};
            }
            """;

        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);
        IDiagnosticReporter reporter = new DiagnosticReporter();

        SourceAnalysisResult result = (SourceAnalysisResult)await new SourceAnalyzer().Analyze(snapshot, reporter, CancellationToken.None);

        return (result, reporter.ReportDiagnostics());
    }
}
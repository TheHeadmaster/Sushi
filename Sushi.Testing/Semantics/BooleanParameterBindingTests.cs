using FluentAssertions;
using NUnit.Framework;
using Sushi.Analysis;
using Sushi.Diagnostics;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.Testing.Semantics;

[TestFixture]
public sealed class BooleanParameterBindingTests
{
    private static readonly Uri testUri = new("file:///TestProject/BooleanParameterBinding.sus");

    [TestCase("public bool echo(bool value) { return value; }", 0, "value", TestName = "Binder Should Bind A Runtime Boolean Parameter")]
    [TestCase("public bool second(bool first, bool second) { return second; }", 1, "second", TestName = "Binder Should Preserve The Second Boolean Parameter Index")]
    [TestCase("public bool echo(bool @default) { return @default; }", 0, "default", TestName = "Binder Should Resolve Escaped Boolean Parameter Identity")]
    [TestCase("public bool echo(int32 number, bool value) { return value; }", 1, "value", TestName = "Binder Should Resolve Boolean Parameter After Integer Parameter")]
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

        function.Parameters
            .Should()
            .HaveCount(expectedIndex + 1);

        reference.Type
            .Should()
            .Be(BoundType.Bool);

        reference.Parameter.Index
            .Should()
            .Be(expectedIndex);

        reference.Parameter.Name
            .Should()
            .Be(expectedName);

        function.Parameters[expectedIndex]
            .Should()
            .BeSameAs(reference.Parameter);

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase("public bool echo(bool value) { return missing; }", ErrorType.UnknownIdentifier, TestName = "Binder Should Diagnose An Unknown Identifier")]
    [TestCase("public bool echo(bool value, bool value) { return value; }", ErrorType.DuplicateParameter, TestName = "Binder Should Diagnose Duplicate Parameter Names")]
    [TestCase("public bool echo(uint32 value) { return true; }", ErrorType.UnsupportedMVPExpression, TestName = "Binder Should Diagnose An Unsupported Parameter Type")]
    [TestCase("public int32 echo(bool value) { return value; }", ErrorType.UnsupportedMVPExpression, TestName = "Binder Should Reject Boolean Parameter In Integer Result")]
    [TestCase("public bool echo(int32 value) { return value; }", ErrorType.UnsupportedMVPExpression, TestName = "Binder Should Reject Integer Parameter In Boolean Result")]
    [TestCase("public bool echo(int32 value) { return not value; }", ErrorType.UnsupportedMVPExpression, TestName = "Binder Should Reject Boolean Negation Of Integer Parameter")]
    public async Task BindShould_1(string declaration, ErrorType expectedDiagnostic)
    {
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(declaration);

        result.Functions
            .Should()
            .BeEmpty();

        diagnostics
            .Should()
            .ContainSingle(item => item.IsType(expectedDiagnostic));
    }

    [TestCase("public bool echo() { return true; }", TestName = "Binder Should Retain Constant Parameterless Functions")]
    [TestCase("public bool echo(int32 unused) { return true; }", TestName = "Binder Should Allow Integer Parameters In Boolean Functions")]
    public async Task BindShould_2(string declaration)
    {
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(declaration);

        result.Functions
            .Should()
            .ContainSingle();

        diagnostics
            .Should()
            .BeEmpty();
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
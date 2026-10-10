using FluentAssertions;
using NUnit.Framework;
using Sushi.Diagnostics;
using Sushi.Source;

namespace Sushi.Testing.Diagnostics;

[TestFixture]
public class DiagnosticReporterTests
{
    private static readonly SourceSnapshot snapshot = SourceSnapshot.FromText(new Uri("file:///TestProject/Test.sus"), version: null, string.Empty);

    [TestCase(TestName = "Diagnostic Reporter Should Initially Contain No Diagnostics")]
    public void DiagnosticReporterShould_0()
    {
        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();

        diagnosticReporter.HasDiagnostics()
            .Should()
            .BeFalse();

        diagnosticReporter.HasErrors()
            .Should()
            .BeFalse();

        diagnosticReporter.HasWarnings()
            .Should()
            .BeFalse();

        diagnosticReporter.ReportDiagnostics()
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Diagnostic Reporter Should Generate Typed Error")]
    public void DiagnosticReporterShould_1()
    {
        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();
        SourceSpan span = CreateSpan();

        diagnosticReporter.GenerateError(ErrorType.ExpectedSyntax, span);

        diagnosticReporter.HasDiagnostics()
            .Should()
            .BeTrue();

        diagnosticReporter.HasErrors()
            .Should()
            .BeTrue();

        diagnosticReporter.HasWarnings()
            .Should()
            .BeFalse();

        SushiDiagnostic diagnostic = diagnosticReporter.ReportDiagnostics()
            .Should()
            .ContainSingle()
            .Which;

        diagnostic.IsType(ErrorType.ExpectedSyntax)
            .Should()
            .BeTrue();

        diagnostic.Message
            .Should()
            .Be("Syntax expected.");

        diagnostic.Span
            .Should()
            .Be(span);
    }

    [TestCase(TestName = "Diagnostic Reporter Should Generate Typed Warning Without Reporting Error")]
    public void DiagnosticReporterShould_2()
    {
        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();
        SourceSpan span = CreateSpan();

        diagnosticReporter.GenerateWarning(WarningType.TomlSyntaxWarning, span);

        diagnosticReporter.HasDiagnostics()
            .Should()
            .BeTrue();

        diagnosticReporter.HasWarnings()
            .Should()
            .BeTrue();

        diagnosticReporter.HasErrors()
            .Should()
            .BeFalse();

        SushiDiagnostic diagnostic = diagnosticReporter.ReportDiagnostics()
            .Should()
            .ContainSingle()
            .Which;

        diagnostic.IsType(WarningType.TomlSyntaxWarning)
            .Should()
            .BeTrue();

        diagnostic.Message
            .Should()
            .Be("Syntax warning in Toml source.");

        diagnostic.Span
            .Should()
            .Be(span);
    }

    [TestCase(TestName = "Diagnostic Reporter Should Preserve Custom Diagnostic Messages")]
    public void DiagnosticReporterShould_3()
    {
        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();
        SourceSpan span = CreateSpan();

        diagnosticReporter.GenerateErrorWithCustomMessage(ErrorType.ExpectedSyntax, "Expected something extremely specific.", span);
        diagnosticReporter.GenerateWarningWithCustomMessage(WarningType.TomlSyntaxWarning, "Suspicious TOML.", span);

        IReadOnlyList<SushiDiagnostic> diagnostics = diagnosticReporter.ReportDiagnostics();

        diagnostics
            .Should()
            .HaveCount(2);

        diagnostics[0].IsType(ErrorType.ExpectedSyntax)
            .Should()
            .BeTrue();

        diagnostics[0].Message
            .Should()
            .Be("Expected something extremely specific.");

        diagnostics[1].IsType(WarningType.TomlSyntaxWarning)
            .Should()
            .BeTrue();

        diagnostics[1].Message
            .Should()
            .Be("Suspicious TOML.");
    }

    [TestCase(TestName = "Report Diagnostics Should Return Snapshot Of Current Diagnostics")]
    public void ReportDiagnosticsShould_0()
    {
        IDiagnosticReporter diagnosticReporter = new DiagnosticReporter();

        diagnosticReporter.GenerateError(ErrorType.ExpectedSyntax, CreateSpan());

        IReadOnlyList<SushiDiagnostic> firstSnapshot = diagnosticReporter.ReportDiagnostics();

        diagnosticReporter.GenerateWarning(WarningType.TomlSyntaxWarning, CreateSpan());

        IReadOnlyList<SushiDiagnostic> secondSnapshot = diagnosticReporter.ReportDiagnostics();

        firstSnapshot
            .Should()
            .ContainSingle();

        firstSnapshot
            .Should()
            .OnlyContain(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));

        secondSnapshot
            .Should()
            .HaveCount(2);

        secondSnapshot[0].IsType(ErrorType.ExpectedSyntax)
            .Should()
            .BeTrue();

        secondSnapshot[1].IsType(WarningType.TomlSyntaxWarning)
            .Should()
            .BeTrue();
    }

    [TestCase(TestName = "Commit Reporter Should Preserve Diagnostic Order")]
    public void CommitReporterShould_0()
    {
        IDiagnosticReporter parentReporter = new DiagnosticReporter();
        IDiagnosticReporter childReporter = new DiagnosticReporter();

        parentReporter.GenerateError(ErrorType.InvalidUtf8, CreateSpan());

        childReporter.GenerateError(ErrorType.ExpectedSyntax, CreateSpan());
        childReporter.GenerateWarning(WarningType.TomlSyntaxWarning, CreateSpan());

        parentReporter.CommitReporter(childReporter);

        IReadOnlyList<SushiDiagnostic> diagnostics = parentReporter.ReportDiagnostics();

        diagnostics
            .Should()
            .HaveCount(3);

        diagnostics[0].IsType(ErrorType.InvalidUtf8)
            .Should()
            .BeTrue();

        diagnostics[1].IsType(ErrorType.ExpectedSyntax)
            .Should()
            .BeTrue();

        diagnostics[2].IsType(WarningType.TomlSyntaxWarning)
            .Should()
            .BeTrue();
    }

    [TestCase(TestName = "Commit Reporter Should Commit Snapshot Without Linking Reporter State")]
    public void CommitReporterShould_1()
    {
        IDiagnosticReporter parentReporter = new DiagnosticReporter();
        IDiagnosticReporter childReporter = new DiagnosticReporter();

        childReporter.GenerateError(ErrorType.ExpectedSyntax, CreateSpan());

        parentReporter.CommitReporter(childReporter);

        childReporter.GenerateWarning(WarningType.TomlSyntaxWarning, CreateSpan());

        IReadOnlyList<SushiDiagnostic> parentDiagnostics = parentReporter.ReportDiagnostics();
        IReadOnlyList<SushiDiagnostic> childDiagnostics = childReporter.ReportDiagnostics();

        parentDiagnostics
            .Should()
            .ContainSingle();

        parentDiagnostics
            .Should()
            .OnlyContain(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));

        childDiagnostics
            .Should()
            .HaveCount(2);
    }

    private static SourceSpan CreateSpan() => new(snapshot, 0, 0);
}
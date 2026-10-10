using FluentAssertions;
using NUnit.Framework;
using Sushi.Diagnostics;
using Sushi.Source;

namespace Sushi.Testing.Diagnostics;

[TestFixture]
public class SushiDiagnosticTests
{
    private static readonly SourceSnapshot snapshot = SourceSnapshot.FromText(new Uri("file:///TestProject/Test.sus"), version: null, string.Empty);

    [TestCase(ErrorType.InvalidUtf8, 1, "SUSE0001", TestName = "Diagnostic Should Preserve Invalid UTF-8 Error Code")]
    [TestCase(ErrorType.TomlSyntaxError, 100, "SUSE0100", TestName = "Diagnostic Should Preserve TOML Syntax Error Code")]
    [TestCase(ErrorType.TomlMissingRequiredKey, 101, "SUSE0101", TestName = "Diagnostic Should Preserve TOML Missing Required Key Error Code")]
    [TestCase(ErrorType.TomlInvalidValueType, 102, "SUSE0102", TestName = "Diagnostic Should Preserve TOML Invalid Value Type Error Code")]
    [TestCase(ErrorType.TomlEmptyProjectName, 103, "SUSE0103", TestName = "Diagnostic Should Preserve TOML Empty Project Name Error Code")]
    [TestCase(ErrorType.TomlMissingBuildTargets, 104, "SUSE0104", TestName = "Diagnostic Should Preserve TOML Missing Build Targets Error Code")]
    [TestCase(ErrorType.TomlUnknownDefaultBuildTarget, 105, "SUSE0105", TestName = "Diagnostic Should Preserve TOML Unknown Default Build Target Error Code")]
    [TestCase(ErrorType.UnterminatedBlockComment, 201, "SUSE0201", TestName = "Diagnostic Should Preserve Unterminated Block Comment Error Code")]
    [TestCase(ErrorType.MissingRadixDigit, 202, "SUSE0202", TestName = "Diagnostic Should Preserve Missing Radix Digit Error Code")]
    [TestCase(ErrorType.InvalidRadixDigit, 203, "SUSE0203", TestName = "Diagnostic Should Preserve Invalid Radix Digit Error Code")]
    [TestCase(ErrorType.InvalidDigitSeparator, 204, "SUSE0204", TestName = "Diagnostic Should Preserve Invalid Digit Separator Error Code")]
    [TestCase(ErrorType.MissingLexicalSeparation, 205, "SUSE0205", TestName = "Diagnostic Should Preserve Missing Lexical Separation Error Code")]
    [TestCase(ErrorType.RequiredSyntacticAdjacency, 206, "SUSE0206", TestName = "Diagnostic Should Preserve Required Syntactic Adjacency Error Code")]
    [TestCase(ErrorType.ExpectedSyntax, 207, "SUSE0207", TestName = "Diagnostic Should Preserve Expected Syntax Error Code")]
    [TestCase(ErrorType.IntegerConstantOutOfRange, 300, "SUSE0300", TestName = "Diagnostic Should Preserve Integer Constant Out Of Range Error Code")]
    [TestCase(ErrorType.MissingPackageDeclaration, 301, "SUSE0301", TestName = "Diagnostic Should Preserve Missing Package Declaration Error Code")]
    [TestCase(ErrorType.DuplicatePackageDeclaration, 302, "SUSE0302", TestName = "Diagnostic Should Preserve Duplicate Package Declaration Error Code")]
    [TestCase(ErrorType.MissingNamespaceDeclaration, 303, "SUSE0303", TestName = "Diagnostic Should Preserve Missing Namespace Declaration Error Code")]
    [TestCase(ErrorType.DuplicateNamespaceDeclaration, 304, "SUSE0304", TestName = "Diagnostic Should Preserve Duplicate Namespace Declaration Error Code")]
    public void ErrorDiagnosticShould_0(ErrorType type, int expectedCode, string expectedDisplayName)
    {
        SushiDiagnostic diagnostic = new((int)type, "Test diagnostic.", DiagnosticSeverity.Error, CreateSpan());

        ((int)type)
            .Should()
            .Be(expectedCode);

        diagnostic.Code
            .Should()
            .Be(expectedCode);

        diagnostic.DisplayName
            .Should()
            .Be(expectedDisplayName);

        diagnostic.IsType(type)
            .Should()
            .BeTrue();
    }

    [TestCase(WarningType.TomlSyntaxWarning, 1, "SUSW0001", TestName = "Diagnostic Should Preserve TOML Syntax Warning Code")]
    public void WarningDiagnosticShould_0(WarningType type, int expectedCode, string expectedDisplayName)
    {
        SushiDiagnostic diagnostic = new((int)type, "Test diagnostic.", DiagnosticSeverity.Warning, CreateSpan());

        ((int)type)
            .Should()
            .Be(expectedCode);

        diagnostic.Code
            .Should()
            .Be(expectedCode);

        diagnostic.DisplayName
            .Should()
            .Be(expectedDisplayName);

        diagnostic.IsType(type)
            .Should()
            .BeTrue();
    }

    [TestCase(DiagnosticSeverity.Error, "SUSE0042", TestName = "Diagnostic Display Name Should Encode Error Severity")]
    [TestCase(DiagnosticSeverity.Warning, "SUSW0042", TestName = "Diagnostic Display Name Should Encode Warning Severity")]
    [TestCase(DiagnosticSeverity.Information, "SUSI0042", TestName = "Diagnostic Display Name Should Encode Information Severity")]
    [TestCase(DiagnosticSeverity.Hint, "SUSH0042", TestName = "Diagnostic Display Name Should Encode Hint Severity")]
    public void DisplayNameShould_0(DiagnosticSeverity severity, string expectedDisplayName)
    {
        SushiDiagnostic diagnostic = new(42, "Test diagnostic.", severity, CreateSpan());

        diagnostic.DisplayName
            .Should()
            .Be(expectedDisplayName);
    }

    [TestCase(TestName = "Diagnostic Type Should Include Severity In Identity")]
    public void IsTypeShould_0()
    {
        SushiDiagnostic error = new((int)ErrorType.InvalidUtf8, "Error.", DiagnosticSeverity.Error, CreateSpan());
        SushiDiagnostic warning = new((int)WarningType.TomlSyntaxWarning, "Warning.", DiagnosticSeverity.Warning, CreateSpan());

        error.Code
            .Should()
            .Be(warning.Code);

        error.IsType(ErrorType.InvalidUtf8)
            .Should()
            .BeTrue();

        error.IsType(WarningType.TomlSyntaxWarning)
            .Should()
            .BeFalse();

        warning.IsType(WarningType.TomlSyntaxWarning)
            .Should()
            .BeTrue();

        warning.IsType(ErrorType.InvalidUtf8)
            .Should()
            .BeFalse();

        error.DisplayName
            .Should()
            .Be("SUSE0001");

        warning.DisplayName
            .Should()
            .Be("SUSW0001");
    }

    private static SourceSpan CreateSpan() => new(snapshot, 0, 0);
}
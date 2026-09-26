using System.Text;
using FluentAssertions;
using NUnit.Framework;
using Sushi.Configuration;
using Sushi.Configuration.Toml;
using Sushi.Diagnostics;
using Sushi.Source;

namespace Sushi.Testing.Configuration;

[TestFixture]
public class TomlConfigurationParserTests
{
    [TestCase(TestName = "Parse Should Produce No Diagnostics For Valid TOML")]
    public void ParseShould_0()
    {
        string text =
            """
            name = "Sushi Compiler"
            assembly = "Sushi.Compiler"
            language-version = "1.0"

            [build]
            default = "debug"

            [build.targets.debug]
            type = "Sushi.Compiler.Build.DebugTarget"
            """;

        SourceSnapshot snapshot = CreateSnapshot(text);

        TomlConfigurationParser parser = new();

        TomlConfigurationParseResult result = parser.Parse(snapshot, CancellationToken.None);

        result.Snapshot.Should().BeSameAs(snapshot);
        result.Syntax.Should().NotBeNull();
        result.Diagnostics.Should().BeEmpty();
    }

    [TestCase(TestName = "Parse Should Convert Invalid TOML Diagnostics To Sushi Diagnostics")]
    public void ParseShould_1()
    {
        const string text =
            """
            name = "Sushi Compiler"
            broken = @
            """;

        SourceSnapshot snapshot = CreateSnapshot(text);

        TomlConfigurationParser parser = new();

        TomlConfigurationParseResult result = parser.Parse(snapshot, CancellationToken.None);

        result.Diagnostics.Should().NotBeEmpty();

        result.Diagnostics
            .Should()
            .Contain(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

        result.Diagnostics
            .Should()
            .OnlyContain(diagnostic => ReferenceEquals(diagnostic.Span.Snapshot, snapshot));
    }

    [TestCase(TestName = "Parse Should Convert TOML Character Offsets To UTF-8 Byte Offsets")]
    public void ParseShould_2()
    {
        const string asciiText =
            """
            name = "ab"
            broken = @
            """;

        const string unicodeText =
            """
            name = "寿司"
            broken = @
            """;

        SourceSnapshot asciiSnapshot = CreateSnapshot(asciiText);
        SourceSnapshot unicodeSnapshot = CreateSnapshot(unicodeText);

        TomlConfigurationParser parser = new();

        TomlConfigurationParseResult asciiResult = parser.Parse(asciiSnapshot, CancellationToken.None);

        TomlConfigurationParseResult unicodeResult = parser.Parse(unicodeSnapshot, CancellationToken.None);

        SushiDiagnostic asciiDiagnostic = asciiResult.Diagnostics
            .First(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

        SushiDiagnostic unicodeDiagnostic = unicodeResult.Diagnostics
            .First(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

        int expectedByteOffsetDifference = Encoding.UTF8.GetByteCount("寿司") - Encoding.UTF8.GetByteCount("ab");

        unicodeDiagnostic.Span.Start
            .Should()
            .Be(asciiDiagnostic.Span.Start + expectedByteOffsetDifference);

        unicodeDiagnostic.Span.End
            .Should()
            .Be(asciiDiagnostic.Span.End + expectedByteOffsetDifference);
    }

    [TestCase(TestName = "Parse Should Produce Valid Sushi Spans For End Of File Diagnostics")]
    public void ParseShould_3()
    {
        const string text =
            """
            name = "Sushi Compiler"

            [build
            """;

        SourceSnapshot snapshot = CreateSnapshot(text);

        TomlConfigurationParser parser = new();

        TomlConfigurationParseResult result = parser.Parse(snapshot, CancellationToken.None);

        result.Diagnostics.Should().NotBeEmpty();

        result.Diagnostics
            .Should()
            .OnlyContain(diagnostic =>
                diagnostic.Span.Start >= 0
                && diagnostic.Span.End >= diagnostic.Span.Start
                && diagnostic.Span.End <= snapshot.Bytes.Length);
    }

    [TestCase(TestName = "Parse Should Preserve Recoverable Semantic Document After Syntax Error")]
    public void ParseShould_4()
    {
        const string text =
            """
            name = "Sushi Compiler"
            broken = @
            assembly = "Sushi.Compiler"
            language-version = "1.0"

            source.exclude = ["Generated/**"]
            """;

        SourceSnapshot snapshot = CreateSnapshot(text);

        TomlConfigurationParser parser = new();

        TomlConfigurationParseResult result = parser.Parse(snapshot, CancellationToken.None);

        result.Diagnostics
            .Should()
            .Contain(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

        result.Document
            .TryGetProperty("assembly", out TomlConfigurationProperty assemblyProperty)
            .Should()
            .BeTrue();

        assemblyProperty.Value
            .Should()
            .BeOfType<TomlConfigurationString>()
            .Which.Value
            .Should()
            .Be("Sushi.Compiler");

        result.Document
            .TryGetProperty("source", out TomlConfigurationProperty sourceProperty)
            .Should()
            .BeTrue();

        TomlConfigurationTable source =
            sourceProperty.Value
                .Should()
                .BeOfType<TomlConfigurationTable>()
                .Subject;

        source.TryGetProperty("exclude", out TomlConfigurationProperty excludeProperty)
            .Should()
            .BeTrue();

        excludeProperty.Value
            .Should()
            .BeOfType<TomlConfigurationArray>();
    }

    [TestCase(TestName = "Bind Should Not Duplicate Syntax Diagnostic For Invalid Required Value")]
    public void BindShould_18()
    {
        const string text =
            """
            name =
            assembly = "Sushi.Compiler"
            language-version = "1.0"
            """;

        SourceSnapshot snapshot = CreateSnapshot(text);

        TomlConfigurationParser parser = new();

        TomlConfigurationParseResult parseResult = parser.Parse(snapshot, CancellationToken.None);

        parseResult.Diagnostics
            .Should()
            .NotBeEmpty();

        ProjectConfigurationBinder binder = new();

        ProjectConfigurationBindResult bindResult = binder.Bind(snapshot, parseResult.Document, CancellationToken.None);

        bindResult.Project
            .Should()
            .BeNull();

        bindResult.Diagnostics
            .Should()
            .BeEmpty();
    }

    private static SourceSnapshot CreateSnapshot(string text) => new(new Uri("file:///TestProject/Test.susproj"), version: null, text);
}
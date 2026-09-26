using FluentAssertions;
using NUnit.Framework;
using Sushi.Analysis;
using Sushi.Configuration;
using Sushi.Diagnostics;
using Sushi.Source;

namespace Sushi.Testing.Configuration;

[TestFixture]
public class ProjectConfigurationBinderTests
{
    [TestCase(TestName = "Bind Should Produce Project Definition For Valid Project Identity")]
    public void BindShould_0()
    {
        const string text =
            """
            name = "Sushi Compiler"
            assembly = "Sushi.Compiler"
            language-version = "1.0"
            """;

        (SourceSnapshot _, ProjectConfigurationBindResult result) = Bind(text);

        result.Diagnostics
            .Should()
            .BeEmpty();

        result.Project
            .Should()
            .BeEquivalentTo(CreateExpectedProject());
    }

    [TestCase(TestName = "Bind Should Accept Quoted And Escaped Root Keys")]
    public void BindShould_1()
    {
        const string text =
            """
            "na\u006de" = "Sushi Compiler"
            'assembly' = "Sushi.Compiler"
            "language-version" = "1.0"
            """;

        (_, ProjectConfigurationBindResult result) = Bind(text);

        result.Diagnostics
            .Should()
            .BeEmpty();

        result.Project
            .Should()
            .BeEquivalentTo(CreateExpectedProject());
    }

    [TestCase(TestName = "Bind Should Use Decoded String Values")]
    public void BindShould_2()
    {
        const string text =
            """
            name = "Sushi\u0020Compiler"
            assembly = "Sushi\u002eCompiler"
            language-version = "1\u002e0"
            """;

        (_, ProjectConfigurationBindResult result) =
            Bind(text);

        result.Diagnostics.Should().BeEmpty();

        result.Project
            .Should()
            .BeEquivalentTo(CreateExpectedProject());
    }

    [TestCase(TestName = "Bind Should Reject Non String Project Identity Values")]
    public void BindShould_3()
    {
        const string text =
            """
            name = 42
            assembly = true
            language-version = 1.0
            """;

        (_, ProjectConfigurationBindResult result) = Bind(text);

        result.Project
            .Should()
            .BeNull();

        result.Diagnostics
            .Should()
            .HaveCount(3);

        result.Diagnostics
            .Should()
            .OnlyContain(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [TestCase(TestName = "Bind Should Diagnose Missing Required Project Identity Keys")]
    public void BindShould_4()
    {
        const string text =
            """
            name = "Sushi Compiler"
            """;

        (SourceSnapshot snapshot, ProjectConfigurationBindResult result) = Bind(text);

        result.Project.Should().BeNull();

        result.Diagnostics.Should().HaveCount(2);

        result.Diagnostics
            .Should()
            .OnlyContain(diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Error
                && diagnostic.Span.Start == snapshot.Bytes.Length
                && diagnostic.Span.End == snapshot.Bytes.Length);
    }

    [TestCase(TestName = "Bind Should Reject Empty Project Name")]
    public void BindShould_5()
    {
        const string text =
            """
            name = ""
            assembly = "Sushi.Compiler"
            language-version = "1.0"
            """;

        (_, ProjectConfigurationBindResult result) = Bind(text);

        result.Project
            .Should()
            .BeNull();

        result.Diagnostics
            .Should()
            .ContainSingle();

        result.Diagnostics.Single()
            .Message
            .Should()
            .Contain("Project name");
    }

    [TestCase(TestName = "Bind Should Not Treat Dotted Keys As Required Root Keys")]
    public void BindShould_6()
    {
        const string text =
            """
            name.value = "Sushi Compiler"
            assembly.value = "Sushi.Compiler"
            language-version.value = "1.0"
            """;

        (_, ProjectConfigurationBindResult result) = Bind(text);

        result.Project.Should().BeNull();

        result.Diagnostics.Should().HaveCount(3);
    }

    [TestCase(TestName = "Bind Should Ignore Configuration Not Yet Bound By This Slice")]
    public void BindShould_7()
    {
        const string text =
            """
            name = "Sushi Compiler"
            assembly = "Sushi.Compiler"
            language-version = "1.0"

            some-future-key = "ignored for now"

            [build]
            default = "debug"

            [build.targets.debug]
            type = "Sushi.Compiler.Build.DebugTarget"
            """;

        (_, ProjectConfigurationBindResult result) = Bind(text);

        result.Diagnostics.Should().BeEmpty();

        result.Project
            .Should()
            .BeEquivalentTo(CreateExpectedProject());
    }

    [TestCase(TestName = "Bind Should Place Invalid Value Diagnostic On The Value")]
    public void BindShould_8()
    {
        const string text =
            """
            name = 42
            assembly = "Sushi.Compiler"
            language-version = "1.0"
            """;

        (SourceSnapshot snapshot, ProjectConfigurationBindResult result) = Bind(text);

        result.Project.Should().BeNull();
        result.Diagnostics.Should().ContainSingle();

        SushiDiagnostic diagnostic =
            result.Diagnostics.Single();

        int valueStart = System.Text.Encoding.UTF8.GetByteCount(text[..text.IndexOf("42", StringComparison.Ordinal)]);

        diagnostic.Span.Snapshot.Should().BeSameAs(snapshot);
        diagnostic.Span.Start.Should().Be(valueStart);
        diagnostic.Span.Length.Should().Be(2);
    }

    [TestCase(TestName = "Bind Should Use Empty Source Definition When Source Table Is Missing")]
    public void BindShould_9()
    {
        const string text =
            """
            name = "Sushi Compiler"
            assembly = "Sushi.Compiler"
            language-version = "1.0"
            """;

        (_, ProjectConfigurationBindResult result) = Bind(text);

        result.Diagnostics.Should().BeEmpty();

        result.Project
            .Should()
            .BeEquivalentTo(CreateExpectedProject());
    }

    [TestCase(TestName = "Bind Should Bind Source Default Namespace")]
    public void BindShould_10()
    {
        const string text =
            """
            name = "Sushi Compiler"
            assembly = "Sushi.Compiler"
            language-version = "1.0"

            [source]
            default-namespace = "Sushi.Compiler"
            """;

        (_, ProjectConfigurationBindResult result) = Bind(text);

        result.Diagnostics.Should().BeEmpty();

        result.Project
            .Should()
            .BeEquivalentTo(
                CreateExpectedProject(
                    defaultNamespace: "Sushi.Compiler"));
    }

    [TestCase(TestName = "Bind Should Bind Source Exclusions")]
    public void BindShould_11()
    {
        const string text =
            """
            name = "Sushi Compiler"
            assembly = "Sushi.Compiler"
            language-version = "1.0"

            [source]
            exclude = [
                "Experimental/**",
                "Generated/Test.sus",
            ]
            """;

        (_, ProjectConfigurationBindResult result) = Bind(text);

        result.Diagnostics.Should().BeEmpty();

        result.Project
            .Should()
            .BeEquivalentTo(
                CreateExpectedProject(
                    exclude:
                    [
                        "Experimental/**",
                        "Generated/Test.sus"
                    ]));
    }

    [TestCase(TestName = "Bind Should Decode Source String Values")]
    public void BindShould_12()
    {
        const string text =
            """
            name = "Sushi Compiler"
            assembly = "Sushi.Compiler"
            language-version = "1.0"

            ["source"]
            "default-namespace" = "Sushi\u002eCompiler"
            "exclude" = ["Generated\u002f**"]
            """;

        (_, ProjectConfigurationBindResult result) = Bind(text);

        result.Diagnostics.Should().BeEmpty();

        result.Project
            .Should()
            .BeEquivalentTo(
                CreateExpectedProject(
                    defaultNamespace: "Sushi.Compiler",
                    exclude: ["Generated/**"]));
    }

    [TestCase(TestName = "Bind Should Reject Non Array Source Exclude")]
    public void BindShould_13()
    {
        const string text =
            """
            name = "Sushi Compiler"
            assembly = "Sushi.Compiler"
            language-version = "1.0"

            [source]
            exclude = "Generated/**"
            """;

        (_, ProjectConfigurationBindResult result) = Bind(text);

        result.Project.Should().BeNull();

        result.Diagnostics.Should().ContainSingle();

        result.Diagnostics.Single()
            .Message
            .Should()
            .Contain("source.exclude");
    }

    [TestCase(TestName = "Bind Should Diagnose Invalid Source Exclude Element On The Element")]
    public void BindShould_14()
    {
        const string text =
            """
            name = "Sushi Compiler"
            assembly = "Sushi.Compiler"
            language-version = "1.0"

            [source]
            exclude = [
                "Experimental/**",
                42,
                "Generated/**",
            ]
            """;

        (SourceSnapshot snapshot, ProjectConfigurationBindResult result) =
            Bind(text);

        result.Project.Should().BeNull();
        result.Diagnostics.Should().ContainSingle();

        SushiDiagnostic diagnostic =
            result.Diagnostics.Single();

        int valueStart =
            System.Text.Encoding.UTF8.GetByteCount(
                text[..text.IndexOf("42", StringComparison.Ordinal)]);

        diagnostic.Span.Snapshot.Should().BeSameAs(snapshot);
        diagnostic.Span.Start.Should().Be(valueStart);
        diagnostic.Span.Length.Should().Be(2);
    }

    [TestCase(TestName = "Bind Should Not Treat Nested Source Table As Source Configuration")]
    public void BindShould_15()
    {
        const string text =
            """
            name = "Sushi Compiler"
            assembly = "Sushi.Compiler"
            language-version = "1.0"
    
            [source.generated]
            default-namespace = "Wrong.Namespace"
            """;
    
        (_, ProjectConfigurationBindResult result) = Bind(text);
    
        result.Diagnostics.Should().BeEmpty();
    
        result.Project
            .Should()
            .BeEquivalentTo(CreateExpectedProject());
    }

    [TestCase(TestName = "Bind Should Accept Dotted Source Configuration")]
    public void BindShould_16()
    {
        const string text =
            """
            name = "Sushi Compiler"
            assembly = "Sushi.Compiler"
            language-version = "1.0"

            source.default-namespace = "Sushi.Compiler"
            source.exclude = ["Generated/**"]
            """;

        (_, ProjectConfigurationBindResult result) = Bind(text);

        result.Diagnostics.Should().BeEmpty();

        result.Project
            .Should()
            .BeEquivalentTo(
                CreateExpectedProject(
                    defaultNamespace: "Sushi.Compiler",
                    exclude: ["Generated/**"]));
    }

    [TestCase(TestName = "Bind Should Accept Inline Source Configuration")]
    public void BindShould_17()
    {
        const string text =
            """
            name = "Sushi Compiler"
            assembly = "Sushi.Compiler"
            language-version = "1.0"
    
            source = {
                default-namespace = "Sushi.Compiler",
                exclude = ["Generated/**"],
            }
            """;
    
        (_, ProjectConfigurationBindResult result) = Bind(text);
    
        result.Diagnostics.Should().BeEmpty();
    
        result.Project
            .Should()
            .BeEquivalentTo(
                CreateExpectedProject(
                    defaultNamespace: "Sushi.Compiler",
                    exclude: ["Generated/**"]));
    }

    private static (SourceSnapshot Snapshot, ProjectConfigurationBindResult Result) Bind(string text)
    {
        SourceSnapshot snapshot = CreateSnapshot(text);

        TomlConfigurationParser parser = new();

        TomlConfigurationParseResult parseResult = parser.Parse(snapshot, CancellationToken.None);

        parseResult.Diagnostics.Should().BeEmpty("binder tests require syntactically valid TOML");

        ProjectConfigurationBinder binder = new();

        ProjectConfigurationBindResult result = binder.Bind(snapshot, parseResult.Document, CancellationToken.None);

        return (snapshot, result);
    }

    private static SourceSnapshot CreateSnapshot(string text)
    {
        return new SourceSnapshot(
            new Uri("file:///TestProject/Test.susproj"),
            version: null,
            text);
    }

    private static ProjectDefinition CreateExpectedProject(string name = "Sushi Compiler", string assembly = "Sushi.Compiler", string languageVersion = "1.0", string? defaultNamespace = null, IReadOnlyList<string>? exclude = null)
        => new(name, assembly, languageVersion, new ProjectSourceDefinition(defaultNamespace, exclude ?? []));
}
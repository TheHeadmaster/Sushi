
using FluentAssertions;
using NUnit.Framework;
using Sushi.Diagnostics;
using Sushi.Lexing;
using Sushi.Parsing;
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.Testing.Parsing;

/// <summary>
/// Verifies parsing and recovery for Sushi activity declarations and lifecycle methods.
/// </summary>
[TestFixture]
public class ActivityDeclarationParserTests
{
    private static readonly Uri testUri = new("file:///TestProject/Activities.sus");

    [TestCase(TestName = "Parser Should Recognize Root Task Activity With Entry And Stop Methods")]
    public void ParserShould_0()
    {
        const string source = """
            public root activity Application : task
            {
                public entry void Boot()
                {
                }

                public stop void Cleanup()
                {
                }
            }
            """;

        (ParserResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseActivity(source);

        diagnostics
            .Should()
            .BeEmpty();

        ActivityDeclarationSyntax activity = GetActivity(result);

        activity.Type
            .Should()
            .Be(SyntaxType.ActivityDeclaration);

        activity.RootKeyword
            .Should()
            .NotBeNull();

        activity.RootKeyword!.Value.Type
            .Should()
            .Be(SyntaxType.RootKeyword);

        activity.ActivityKeyword.Type
            .Should()
            .Be(SyntaxType.ActivityKeyword);

        activity.Name.Type
            .Should()
            .Be(SyntaxType.IdentifierToken);

        activity.ColonToken.Type
            .Should()
            .Be(SyntaxType.ColonToken);

        activity.LifetimeKeyword.Type
            .Should()
            .Be(SyntaxType.TaskKeyword);

        activity.Members
            .Should()
            .HaveCount(2);

        ActivityMethodDeclarationSyntax entry = activity.Members[0]
            .Should()
            .BeOfType<ActivityMethodDeclarationSyntax>()
            .Subject;

        ActivityMethodDeclarationSyntax stop = activity.Members[1]
            .Should()
            .BeOfType<ActivityMethodDeclarationSyntax>()
            .Subject;

        entry.LifecycleModifier.Type
            .Should()
            .Be(SyntaxType.EntryKeyword);

        stop.LifecycleModifier.Type
            .Should()
            .Be(SyntaxType.StopKeyword);

        entry.ReturnType.Type
            .Should()
            .Be(SyntaxType.VoidKeyword);

        stop.ReturnType.Type
            .Should()
            .Be(SyntaxType.VoidKeyword);

        entry.Name.Span.Snapshot.GetType()
            .Should()
            .Be<SourceSnapshot>();

        entry.Name.Span
            .Should()
            .NotBe(stop.Name.Span);

        entry.Parameters
            .Should()
            .BeEmpty();

        stop.Parameters
            .Should()
            .BeEmpty();

        entry.Span.End
            .Should()
            .BeLessThan(stop.Span.Start);

        activity.Span.Snapshot
            .Should()
            .BeSameAs(result.Tree.Snapshot);

        activity.Span.Start
            .Should()
            .Be(0);

        activity.Span.End
            .Should()
            .Be(source.Length);
    }

    [TestCase(TestName = "Parser Should Recognize Non Root Persistent Activity")]
    public void ParserShould_1()
    {
        const string source = """
            internal activity Worker : persistent
            {
                public entry int32 Execute(int32 count)
                {
                    return count;
                }
            }
            """;

        (ParserResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseActivity(source);

        diagnostics
            .Should()
            .BeEmpty();

        ActivityDeclarationSyntax activity = GetActivity(result);

        activity.RootKeyword
            .Should()
            .BeNull();

        activity.LifetimeKeyword.Type
            .Should()
            .Be(SyntaxType.PersistentKeyword);

        ActivityMethodDeclarationSyntax entry = activity.Members
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<ActivityMethodDeclarationSyntax>()
            .Subject;

        entry.Parameters
            .Should()
            .ContainSingle();

        entry.Body.Statements
            .Should()
            .ContainSingle();
    }

    [TestCase(TestName = "Parser Should Recognize Activity Without Stop Method")]
    public void ParserShould_2()
    {
        const string source = """
            public root activity Application : task
            {
                public entry void Start() {}
            }
            """;

        (ParserResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseActivity(source);

        diagnostics
            .Should()
            .BeEmpty();

        ActivityDeclarationSyntax activity = GetActivity(result);

        activity.Members
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<ActivityMethodDeclarationSyntax>();
    }

    [TestCase(TestName = "Parser Should Preserve Interleaved Function And Activity Declarations")]
    public void ParserShould_3()
    {
        const string source = """
            package Example;
            namespace Example;

            public int32 Before() { return 1; }

            public root activity Application : task
            {
                public entry void Start() {}
            }

            public int32 After() { return 2; }
            """;

        (ParserResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = ParsingHelper.ParseSourceFile(source, testUri);

        diagnostics
            .Should()
            .BeEmpty();

        SourceFileSyntax sourceFile = GetSourceFile(result);

        sourceFile.FunctionDeclarations
            .Should()
            .HaveCount(2);

        sourceFile.ActivityDeclarations
            .Should()
            .ContainSingle();

        sourceFile.Declarations
            .Select(declaration => declaration.Type)
            .Should()
            .Equal(
                SyntaxType.FunctionDeclaration,
                SyntaxType.ActivityDeclaration,
                SyntaxType.FunctionDeclaration);

        sourceFile.UnparsedContentSpan
            .Should()
            .BeNull();
    }

    [TestCase(TestName = "Parser Should Preserve Multiple Activity Declarations")]
    public void ParserShould_4()
    {
        const string source = """
            package Example;
            namespace Example;

            public activity Worker : persistent
            {
                public entry void Start() {}
            }

            public root activity Application : task
            {
                public entry void Start() {}
            }
            """;

        (ParserResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = ParsingHelper.ParseSourceFile(source, testUri);

        diagnostics
            .Should()
            .BeEmpty();

        SourceFileSyntax sourceFile = GetSourceFile(result);

        sourceFile.ActivityDeclarations
            .Should()
            .HaveCount(2);

        sourceFile.ActivityDeclarations[0].RootKeyword
            .Should()
            .BeNull();

        sourceFile.ActivityDeclarations[1].RootKeyword
            .Should()
            .NotBeNull();

        sourceFile.UnparsedContentSpan
            .Should()
            .BeNull();
    }

    [TestCase(TestName = "Parser Should Preserve Unsupported Activity Field And Continue To Stop Method")]
    public void ParserShould_5()
    {
        const string source = """
            public root activity Application : task
            {
                public int32 count;
                public stop void Cleanup() {}
            }
            """;

        (ParserResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseActivity(source);

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));

        ActivityDeclarationSyntax activity = GetActivity(result);

        activity.Members
            .Should()
            .HaveCount(2);

        UnsupportedActivityMemberSyntax unsupported = activity.Members[0]
            .Should()
            .BeOfType<UnsupportedActivityMemberSyntax>()
            .Subject;

        unsupported.Span.Start
            .Should()
            .Be(source.IndexOf("public int32", StringComparison.Ordinal));

        unsupported.Span.End
            .Should()
            .Be(source.IndexOf(';', unsupported.Span.Start) + 1);

        activity.Members[1]
            .Should()
            .BeOfType<ActivityMethodDeclarationSyntax>();
    }

    [TestCase(TestName = "Parser Should Recover Unsupported Member With Balanced Method Body")]
    public void ParserShould_6()
    {
        const string source = """
            public root activity Application : task
            {
                public void Helper() { return 42; }
                public stop void Cleanup() {}
            }
            """;

        (ParserResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseActivity(source);

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));

        ActivityDeclarationSyntax activity = GetActivity(result);

        activity.Members
            .Should()
            .HaveCount(2);

        activity.Members[0]
            .Should()
            .BeOfType<UnsupportedActivityMemberSyntax>();

        activity.Members[1]
            .Should()
            .BeOfType<ActivityMethodDeclarationSyntax>();

        activity.CloseBraceToken.IsMissing
            .Should()
            .BeFalse();
    }

    [TestCase(TestName = "Parser Should Recover Missing Activity Lifetime Separator")]
    public void ParserShould_7()
    {
        const string source = "public root activity Application task {}";

        (ParserResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseActivity(source);

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));

        ActivityDeclarationSyntax activity = GetActivity(result);

        activity.ColonToken.IsMissing
            .Should()
            .BeTrue();

        activity.LifetimeKeyword.Type
            .Should()
            .Be(SyntaxType.TaskKeyword);

        activity.CloseBraceToken.IsMissing
            .Should()
            .BeFalse();
    }

    [TestCase(TestName = "Parser Should Recover Missing Activity Lifetime")]
    public void ParserShould_8()
    {
        const string source = "public root activity Application : {}";

        (ParserResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseActivity(source);

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));

        ActivityDeclarationSyntax activity = GetActivity(result);

        activity.LifetimeKeyword.IsMissing
            .Should()
            .BeTrue();

        activity.OpenBraceToken.IsMissing
            .Should()
            .BeFalse();
    }

    [TestCase(TestName = "Parser Should Recover Missing Activity Closing Brace")]
    public void ParserShould_9()
    {
        const string source = "public root activity Application : task {";

        (ParserResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseActivity(source);

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));

        ActivityDeclarationSyntax activity = GetActivity(result);

        activity.CloseBraceToken.IsMissing
            .Should()
            .BeTrue();

        activity.Span.End
            .Should()
            .Be(source.Length);
    }

    [TestCase(TestName = "Parser Should Recover Missing Activity Identifier")]
    public void ParserShould_10()
    {
        const string source = "public root activity : task {}";

        (ParserResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseActivity(source);

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));

        ActivityDeclarationSyntax activity = GetActivity(result);

        activity.Name.IsMissing
            .Should()
            .BeTrue();

        activity.LifetimeKeyword.IsMissing
            .Should()
            .BeFalse();
    }

    [TestCase(TestName = "Parser Should Recover Missing Activity Keyword")]
    public void ParserShould_11()
    {
        const string source = "public root Application : task {}";

        (ParserResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseActivity(source);

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));

        ActivityDeclarationSyntax activity = GetActivity(result);

        activity.ActivityKeyword.IsMissing
            .Should()
            .BeTrue();

        activity.Name.IsMissing
            .Should()
            .BeFalse();
    }

    [TestCase(TestName = "Parser Should Preserve Unsupported Source After Activity Declaration")]
    public void ParserShould_12()
    {
        const string source = """
            package Example;
            namespace Example;

            public root activity Application : task
            {
                public entry void Start() {}
            }

            using Example.Other;
            """;

        (ParserResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = ParsingHelper.ParseSourceFile(source, testUri);

        diagnostics
            .Should()
            .BeEmpty();

        SourceFileSyntax sourceFile = GetSourceFile(result);

        sourceFile.ActivityDeclarations
            .Should()
            .ContainSingle();

        sourceFile.UnparsedContentSpan
            .Should()
            .NotBeNull();

        sourceFile.UnparsedContentSpan!.Value.Start
            .Should()
            .Be(source.IndexOf("using", StringComparison.Ordinal));
    }

    [TestCase(TestName = "Parser Should Preserve Escaped Activity And Method Identifiers")]
    public void ParserShould_13()
    {
        const string source = """
            public root activity @task : task
            {
                public entry void @stop() {}
            }
            """;

        (ParserResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = ParseActivity(source);

        diagnostics
            .Should()
            .BeEmpty();

        ActivityDeclarationSyntax activity = GetActivity(result);

        activity.Name.Type
            .Should()
            .Be(SyntaxType.EscapedIdentifierToken);

        ActivityMethodDeclarationSyntax entry = activity.Members
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<ActivityMethodDeclarationSyntax>()
            .Subject;

        entry.Name.Type
            .Should()
            .Be(SyntaxType.EscapedIdentifierToken);
    }

    private static (ParserResult Result, IReadOnlyList<SushiDiagnostic> Diagnostics) ParseActivity(string source)
    {
        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);
        IDiagnosticReporter reporter = new DiagnosticReporter();
        LexerResult lexerResult = new SourceLexer().Lex(snapshot, reporter, CancellationToken.None);
        ParserResult result = SourceParser.ParseActivityDeclaration(lexerResult, reporter, CancellationToken.None);

        return (result, reporter.ReportDiagnostics());
    }

    private static ActivityDeclarationSyntax GetActivity(ParserResult result)
        => result.Tree.Root
            .Should()
            .BeOfType<ActivityDeclarationSyntax>()
            .Subject;

    private static SourceFileSyntax GetSourceFile(ParserResult result)
        => result.Tree.Root
            .Should()
            .BeOfType<SourceFileSyntax>()
            .Subject;
}

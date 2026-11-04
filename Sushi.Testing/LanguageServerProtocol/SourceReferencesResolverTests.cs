using System.Text;
using FluentAssertions;
using NUnit.Framework;
using Sushi.Analysis;
using Sushi.Diagnostics;
using Sushi.LanguageServerProtocol;
using Sushi.Lexing;
using Sushi.Parsing;
using Sushi.Parsing.Syntax;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.Testing.LanguageServerProtocol;

/// <summary>
/// Verifies source reference grouping independently of the JSON-RPC transport.
/// </summary>
[TestFixture]
public sealed class SourceReferencesResolverTests
{
    [Test]
    public void ResolverShouldMatchDeclarationIdentityAndRespectDeclarationPreference()
    {
        const string declarationText = "package Example; namespace Example; public int32 Target() { return 1; }";
        const string callerText = "package Example; namespace Example; public int32 Caller() { return Target(); }";
        const string unrelatedText = "package Other; namespace Other; public int32 Target() { return 2; }";
        Uri projectUri = new("file:///tmp/References.susproj");
        (SourceAnalysisResult declarationAnalysis, SourceSemanticOccurrence declaration) = CreateAnalysis(
            "file:///tmp/Math.sus", declarationText, "Target", SourceSemanticSymbolKind.Function, 0, projectUri, null);
        SourceSemanticSymbol target = declaration.Symbol;
        (SourceAnalysisResult callerAnalysis, SourceSemanticOccurrence reference) = CreateAnalysis(
            "file:///tmp/Caller.sus", callerText, "Target", SourceSemanticSymbolKind.Function, 0, projectUri, target.DefinitionSpan);
        (SourceAnalysisResult unrelatedAnalysis, _) = CreateAnalysis(
            "file:///tmp/Other.sus", unrelatedText, "Target", SourceSemanticSymbolKind.Function, 0, new Uri("file:///tmp/Other.susproj"), null);
        SourceAnalysisResult[] analyses = [declarationAnalysis, callerAnalysis, unrelatedAnalysis];

        IReadOnlyList<SourceSpan> withDeclaration = SourceReferencesResolver.Resolve(analyses, callerAnalysis, reference.Span.Start,
            includeDeclaration: true, CancellationToken.None);
        IReadOnlyList<SourceSpan> referencesOnly = SourceReferencesResolver.Resolve(analyses, callerAnalysis, reference.Span.Start,
            includeDeclaration: false, CancellationToken.None);

        withDeclaration.Should().HaveCount(2);
        withDeclaration[0].Snapshot.Uri.LocalPath.Should().EndWith("Caller.sus");
        withDeclaration[1].Snapshot.Uri.LocalPath.Should().EndWith("Math.sus");
        referencesOnly.Should().ContainSingle();
        referencesOnly[0].Should().Be(reference.Span);
    }

    [Test]
    public void ResolverShouldNotMergeLocalsWithTheSameNameInDifferentScopes()
    {
        const string source = "package Example; namespace Example; public int32 Caller() { { int32 item = 1; } { int32 item = 2; } return 1; }";
        Uri projectUri = new("file:///tmp/References.susproj");
        (SourceAnalysisResult analysis, SourceSemanticOccurrence selected) = CreateAnalysis(
            "file:///tmp/Caller.sus", source, "item", SourceSemanticSymbolKind.Local, 0, projectUri, null);
        int secondName = source.LastIndexOf("item", StringComparison.Ordinal);
        SourceSpan secondDefinition = CreateSpan(analysis.Snapshot, secondName, "item");
        SourceSemanticSymbol secondSymbol = selected.Symbol with { Index = 1, DefinitionSpan = secondDefinition };
        SourceSemanticOccurrence secondDeclaration = new(secondDefinition, secondSymbol, IsDeclaration: true);
        SourceAnalysisResult populated = analysis with
        {
            SemanticOccurrences = Array.AsReadOnly([selected, secondDeclaration])
        };

        IReadOnlyList<SourceSpan> matches = SourceReferencesResolver.Resolve([populated], populated, selected.Span.Start,
            includeDeclaration: true, CancellationToken.None);

        matches.Should().ContainSingle();
        matches[0].Should().Be(selected.Span);
    }

    [Test]
    public void ResolverShouldPropagateCancellationBeforeCollectingReferences()
    {
        const string source = "package Example; namespace Example; public int32 Target() { return 1; }";
        (SourceAnalysisResult analysis, SourceSemanticOccurrence occurrence) = CreateAnalysis(
            "file:///tmp/Target.sus", source, "Target", SourceSemanticSymbolKind.Function, 0,
            new Uri("file:///tmp/References.susproj"), null);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Action resolve = () => SourceReferencesResolver.Resolve([analysis], analysis, occurrence.Span.Start,
            includeDeclaration: true, cancellation.Token);

        resolve.Should().Throw<OperationCanceledException>();
    }

    private static (SourceAnalysisResult Analysis, SourceSemanticOccurrence Occurrence) CreateAnalysis(
        string uri,
        string text,
        string token,
        SourceSemanticSymbolKind kind,
        int index,
        Uri projectUri,
        SourceSpan? definitionSpan)
    {
        SourceSnapshot snapshot = SourceSnapshot.FromText(new Uri(uri), 1, text);
        DiagnosticReporter reporter = new();
        LexerResult lexical = new SourceLexer().Lex(snapshot, reporter, CancellationToken.None);
        ParserResult parsed = SourceParser.ParseSourceFile(lexical, reporter, CancellationToken.None);
        SyntaxNode declaration = parsed.Tree.Root;
        int characterOffset = text.IndexOf(token, StringComparison.Ordinal);
        SourceSpan span = CreateSpan(snapshot, characterOffset, token);
        SourceSemanticSymbol symbol = new(declaration, kind, index, token, null, null, null, null,
            DefinitionSpan: definitionSpan ?? span);
        SourceSemanticOccurrence occurrence = new(span, symbol, IsDeclaration: definitionSpan is null);
        SourceAnalysisResult analysis = new(snapshot, parsed.Tree, null, null, [])
        {
            SemanticProjectUri = projectUri,
            SemanticOccurrences = Array.AsReadOnly([occurrence])
        };
        return (analysis, occurrence);
    }

    private static SourceSpan CreateSpan(SourceSnapshot snapshot, int characterOffset, string token)
    {
        int start = Encoding.UTF8.GetByteCount(snapshot.Text.AsSpan(0, characterOffset));
        int end = start + Encoding.UTF8.GetByteCount(token);
        return new SourceSpan(snapshot, start, end);
    }
}

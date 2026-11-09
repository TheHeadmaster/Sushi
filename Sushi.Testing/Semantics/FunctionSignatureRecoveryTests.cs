using FluentAssertions;
using NUnit.Framework;
using Sushi.Analysis;
using Sushi.Diagnostics;
using Sushi.Parsing.Syntax;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.Testing.Semantics;

[TestFixture]
public sealed class FunctionSignatureRecoveryTests
{
    [Test]
    public async Task MissingClosingBodyBraceShouldPreserveTheSignatureButRejectFullBinding()
    {
        const string source = """
            package Example;
            namespace Example;
            public void Start()
            {
            """;

        Uri uri = new(Path.Combine(Path.GetTempPath(), "SignatureRecovery.sus"));
        SourceSnapshot snapshot = SourceSnapshot.FromText(uri, 1, source);
        DiagnosticReporter diagnostics = new();

        SourceAnalysisResult analysis = (SourceAnalysisResult)await new SourceAnalyzer()
            .AnalyzeDeclarations(snapshot, diagnostics, CancellationToken.None);

        analysis.Functions.Should().ContainSingle();
        analysis.Functions[0].Name.Should().Be("Start");
        diagnostics.HasErrors().Should().BeTrue();

        SourceFileSyntax syntax = (SourceFileSyntax)analysis.SyntaxTree.Root;
        FunctionDeclarationSyntax declaration = syntax.FunctionDeclarations.Single();

        declaration.Body.CloseBraceToken.IsSourceBacked.Should().BeFalse();

        DiagnosticReporter fullDiagnostics = new();
        FunctionDeclarationBinder.Bind(declaration, fullDiagnostics, CancellationToken.None)
            .Function.Should().BeNull();
    }
}
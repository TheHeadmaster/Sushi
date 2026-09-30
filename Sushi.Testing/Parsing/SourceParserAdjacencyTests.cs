using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using NUnit.Framework;
using Sushi.Diagnostics;
using Sushi.Lexing;
using Sushi.Lexing.Tokenization;
using Sushi.Parsing;
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.Testing.Parsing;

[TestFixture]
public class SourceParserAdjacencyTests
{
    private static readonly Uri testUri = new("file:///TestProject/Adjacency.sus");

    [TestCase("package Sushi.Text;", 0, TestName = "Parser Should Accept Adjacent Qualified Names")]
    [TestCase("package Sushi .Text;", 1, TestName = "Parser Should Diagnose Trivia Before Dot")]
    [TestCase("package Sushi. Text;", 1, TestName = "Parser Should Diagnose Trivia After Dot")]
    [TestCase("package Sushi . Text;", 2, TestName = "Parser Should Diagnose Each Violated Dot Boundary")]
    [TestCase("package Sushi/*comment*/.Text;", 1, TestName = "Parser Should Diagnose Comment After Dot")]
    [TestCase("package Sushi\r\n.Text;", 1, TestName = "Parser Should Diagnose Line Terminator After Dot")]
    [TestCase("package Sushi $ .Text;", 1, TestName = "Parser Should Recover Across Unknown Before Dot")]
    [TestCase("package Sushi. $ Text;", 1, TestName = "Parser Should Recover Across Unknown After Dot")]
    public void ParserShould_0([NotNull] string source, int expectedDiagnostics)
    {
        ParserResult result = Parse(source);

        PackageDeclarationSyntax declaration = GetDeclaration(result);

        result.Diagnostics.Count(diagnostic => diagnostic.Code == "SUSE007")
            .Should()
            .Be(expectedDiagnostics);

        result.Diagnostics.Count(diagnostic => diagnostic.Code == "SUSE006")
            .Should()
            .Be(0);

        declaration.Name.Segments
            .Should()
            .HaveCount(2)
            .And.OnlyContain(segment => segment.IsSourceBacked);

        declaration.Name.Separators
            .Should()
            .ContainSingle(separator => separator.IsSourceBacked);

        declaration.SemicolonToken.IsMissing
            .Should()
            .BeFalse();
    }

    [TestCase(TestName = "Parser Should Mark The Complete Source Gap On Both Sides Of Dot")]
    public void ParserShould_1()
    {
        const string source = "package Sushi /*left*/ . /*right*/ Text;";

        ParserResult result = Parse(source);

        SushiDiagnostic[] diagnostics = [.. result.Diagnostics.Where(diagnostic => diagnostic.Code == "SUSE007")];

        int leftStart = source.IndexOf(" /*left*/ ", StringComparison.Ordinal);
        int rightStart = source.IndexOf(" /*right*/ ", StringComparison.Ordinal);
        
        diagnostics.Select(diagnostic => (diagnostic.Span.Start, diagnostic.Span.End))
            .Should()
            .Equal(
                (leftStart, leftStart + " /*left*/ ".Length),
                (rightStart, rightStart + " /*right*/ ".Length)
            );

        result.Tree.LexerResult.Tokens
            .Count(token => token.Type is LexTokenType.BlockComment)
            .Should()
            .Be(2);
    }

    [TestCase("package .Text;", 0, 2, 1, TestName = "Parser Should Synthesize Missing Initial Name")]
    [TestCase("package Sushi.;", 1, 2, 1, TestName = "Parser Should Synthesize Missing Terminal Name")]
    [TestCase("package Sushi..Text;", 1, 3, 2, TestName = "Parser Should Synthesize Name Between Consecutive Dots")]
    public void ParserShould_2([NotNull] string source, int missingIndex, int expectedSegments, int expectedSeparators)
    {
        ParserResult result = Parse(source);

        PackageDeclarationSyntax declaration = GetDeclaration(result);

        declaration.Name.Segments
            .Should()
            .HaveCount(expectedSegments);

        declaration.Name.Segments[missingIndex].IsMissing
            .Should()
            .BeTrue();

        declaration.Name.Segments
            .Count(segment => segment.IsMissing)
            .Should()
            .Be(1);

        declaration.Name.Separators
            .Should()
            .HaveCount(expectedSeparators);

        result.Diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.Code == "SUSE006")
            .And.NotContain(diagnostic => diagnostic.Code == "SUSE007");

        declaration.SemicolonToken.IsMissing
            .Should()
            .BeFalse();
    }

    [TestCase(TestName = "Parser Should Include Trailing Dot In Recovered Qualified Name Span")]
    public void ParserShould_3()
    {
        const string source = "package Sushi.;";
        
        ParserResult result = Parse(source);

        PackageDeclarationSyntax declaration = GetDeclaration(result);

        declaration.Name.Span.End
            .Should()
            .Be(source.IndexOf(';', StringComparison.Ordinal));
    }

    [TestCase("package Sushi; .Text;", false, TestName = "Parser Should Stop At Recognized Semicolon Before Later Dot")]
    [TestCase("package Sushi else .Text;", true, TestName = "Parser Should Stop At Recognized Keyword Before Later Dot")]
    public void ParserShould_4(string source, bool semicolonIsMissing)
    {
        ParserResult result = Parse(source);

        PackageDeclarationSyntax declaration = GetDeclaration(result);

        declaration.Name.Segments
            .Should()
            .ContainSingle();

        declaration.Name.Separators
            .Should()
            .BeEmpty();

        declaration.SemicolonToken.IsMissing
            .Should()
            .Be(semicolonIsMissing);
    }

    [TestCase(TestName = "Parser Should Recover Missing Name Between Spaced Dots Without Spurious Adjacency Errors")]
    public void ParserShould_5()
    {
        ParserResult result = Parse("package Sushi. /*gap*/ .Text;");

        PackageDeclarationSyntax declaration = GetDeclaration(result);

        declaration.Name.Segments[1].IsMissing
            .Should()
            .BeTrue();

            result.Diagnostics
                .Should()
                .ContainSingle(diagnostic => diagnostic.Code == "SUSE006")
                .And.NotContain(diagnostic => diagnostic.Code == "SUSE007");
    }

    [TestCase(TestName = "Parser Should Accept Escaped Qualified Name Component")]
    public void ParserShould_6()
    {
        ParserResult result = Parse("package Sushi.@if;");

        PackageDeclarationSyntax declaration = GetDeclaration(result);

        result.Diagnostics
            .Should()
            .BeEmpty();

        declaration.Name.Segments[1].Type
            .Should()
            .Be(SyntaxType.EscapedIdentifierToken);
    }

    [TestCase(TestName = "Parser Should not Consume Unknown Without A Recoverable Dot")]
    public void ParserShould_7()
    {
        ParserResult result = Parse("package Sushi $ Text;");

        PackageDeclarationSyntax declaration = GetDeclaration(result);

        result.Diagnostics
            .Should()
            .ContainSingle();

        declaration.SemicolonToken.IsMissing
            .Should()
            .BeTrue();

        result.Diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.Code == "SUSE006")
            .And.NotContain(diagnostic => diagnostic.Code == "SUSE007");   
    }

    private static PackageDeclarationSyntax GetDeclaration(ParserResult result)
        => result.Tree.Root.Should().BeOfType<PackageDeclarationSyntax>().Subject;
    
    private static ParserResult Parse(string source)
    {
        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);

        LexerResult lexerResult = new SourceLexer().Lex(snapshot, CancellationToken.None);

        lexerResult.Diagnostics
            .Should()
            .BeEmpty();

        return new SourceParser().ParsePackageDeclaration(lexerResult, CancellationToken.None);
    }
}
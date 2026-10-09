using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using NUnit.Framework;
using Sushi.Diagnostics;
using Sushi.Lexing.Tokenization;
using Sushi.Parsing;
using Sushi.Parsing.Syntax;

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
        (ParserResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = ParsingHelper.ParsePackageDeclaration(source, testUri);

        PackageDeclarationSyntax declaration = GetDeclaration(result);

        diagnostics
            .Count(diagnostic => diagnostic.IsType(ErrorType.RequiredSyntacticAdjacency))
            .Should()
            .Be(expectedDiagnostics);

        diagnostics
            .Should()
            .NotContain(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax));

        diagnostics
            .Should()
            .HaveCount(expectedDiagnostics);

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

        (ParserResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = ParsingHelper.ParsePackageDeclaration(source, testUri);

        SushiDiagnostic[] adjacencyDiagnostics = [.. diagnostics.Where(diagnostic => diagnostic.IsType(ErrorType.RequiredSyntacticAdjacency))];

        int leftStart = source.IndexOf(" /*left*/ ", StringComparison.Ordinal);
        int rightStart = source.IndexOf(" /*right*/ ", StringComparison.Ordinal);
        
        adjacencyDiagnostics.Select(diagnostic => (diagnostic.Span.Start, diagnostic.Span.End))
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
        (ParserResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = ParsingHelper.ParsePackageDeclaration(source, testUri);

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

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax))
            .And
            .NotContain(diagnostic => diagnostic.IsType(ErrorType.RequiredSyntacticAdjacency));

        declaration.SemicolonToken.IsMissing
            .Should()
            .BeFalse();
    }

    [TestCase(TestName = "Parser Should Include Trailing Dot In Recovered Qualified Name Span")]
    public void ParserShould_3()
    {
        const string source = "package Sushi.;";
        
        (ParserResult result, IReadOnlyList<SushiDiagnostic> _) = ParsingHelper.ParsePackageDeclaration(source, testUri);

        PackageDeclarationSyntax declaration = GetDeclaration(result);

        declaration.Name.Span.End
            .Should()
            .Be(source.IndexOf(';', StringComparison.Ordinal));
    }

    [TestCase("package Sushi; .Text;", false, TestName = "Parser Should Stop At Recognized Semicolon Before Later Dot")]
    [TestCase("package Sushi else .Text;", true, TestName = "Parser Should Stop At Recognized Keyword Before Later Dot")]
    public void ParserShould_4(string source, bool semicolonIsMissing)
    {
        (ParserResult result, IReadOnlyList<SushiDiagnostic> _) = ParsingHelper.ParsePackageDeclaration(source, testUri);

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
        const string source = "package Sushi. /*gap*/ .Text;";

        (ParserResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = ParsingHelper.ParsePackageDeclaration(source, testUri);

        PackageDeclarationSyntax declaration = GetDeclaration(result);

        declaration.Name.Segments[1].IsMissing
            .Should()
            .BeTrue();

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax))
            .And
            .NotContain(diagnostic => diagnostic.IsType(ErrorType.RequiredSyntacticAdjacency));
    }

    [TestCase(TestName = "Parser Should Accept Escaped Qualified Name Component")]
    public void ParserShould_6()
    {
        const string source = "package Sushi.@if;";
        (ParserResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = ParsingHelper.ParsePackageDeclaration(source, testUri);
        PackageDeclarationSyntax declaration = GetDeclaration(result);

        diagnostics
            .Should()
            .BeEmpty();

        declaration.Name.Segments[1].Type
            .Should()
            .Be(SyntaxType.EscapedIdentifierToken);
    }

    [TestCase(TestName = "Parser Should not Consume Unknown Without A Recoverable Dot")]
    public void ParserShould_7()
    {
        const string source = "package Sushi $ Text;";

        (ParserResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = ParsingHelper.ParsePackageDeclaration(source, testUri);

        PackageDeclarationSyntax declaration = GetDeclaration(result);

        declaration.SemicolonToken.IsMissing
            .Should()
            .BeTrue();

        diagnostics
            .Should()
            .ContainSingle(diagnostic => diagnostic.IsType(ErrorType.ExpectedSyntax))
            .And
            .NotContain(diagnostic => diagnostic.IsType(ErrorType.RequiredSyntacticAdjacency)); 
    }

    private static PackageDeclarationSyntax GetDeclaration(ParserResult result)
        => result.Tree.Root.Should().BeOfType<PackageDeclarationSyntax>().Subject;
}
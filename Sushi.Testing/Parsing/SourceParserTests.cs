using FluentAssertions;
using NUnit.Framework;
using Sushi.Lexing;
using Sushi.Parsing;
using Sushi.Parsing.Syntax;
using Sushi.Source;
using Sushi.Lexing.Tokenization;

namespace Sushi.Testing.Parsing;

[TestFixture]
public class SourceParserTests
{
    private static readonly Uri testUri = new("file:///TestProject/Test.sus");

    [TestCase(TestName = "Parser Should Parse Dotted Package Declaration")]
    public void ParserShould_0()
    {
        ParserResult result = Parse("package Sushi.StandardLibrary.Text;");

        result.Diagnostics
            .Should()
            .BeEmpty();

        PackageDeclarationSyntax declaration = result.Tree.Root
            .Should()
            .BeOfType<PackageDeclarationSyntax>()
            .Subject;

        declaration.PackageKeyword.Type
            .Should()
            .Be(SyntaxType.PackageKeyword);

        declaration.Name.Segments
            .Select(segment => segment.Type)
            .Should()
            .Equal(
                SyntaxType.IdentifierToken,
                SyntaxType.IdentifierToken,
                SyntaxType.IdentifierToken
            );

        declaration.Name.Separators
            .Select(separator => separator.Type)
            .Should()
            .Equal(
                SyntaxType.DotToken,
                SyntaxType.DotToken
            );

        declaration.SemicolonToken.Type
            .Should()
            .Be(SyntaxType.SemicolonToken);

        declaration.PackageKeyword.IsMissing
            .Should()
            .BeFalse();

        declaration.SemicolonToken.IsMissing
            .Should()
            .BeFalse();

        declaration.Span.Snapshot
            .Should()
            .BeSameAs(result.Tree.Snapshot);

        declaration.Span.Start
            .Should()
            .Be(0);

        declaration.Span.End
            .Should()
            .Be("package Sushi.StandardLibrary.Text;".Length);
    }
    
    private static ParserResult Parse(string source)
    {
        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);

        SourceLexer lexer = new();
        LexerResult lexerResult = lexer.Lex(snapshot, CancellationToken.None);

        lexerResult.Diagnostics
            .Should()
            .BeEmpty();

        SourceParser parser = new();

        return parser.ParsePackageDeclaration(lexerResult, CancellationToken.None);
    }
}
using FluentAssertions;
using NUnit.Framework;
using Sushi.Lexing;
using Sushi.Parsing;
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.Testing.Parsing;

[TestFixture]
public class SourceParserFunctionTests
{
    private static readonly Uri testUri = new("file:///TestProject/Function.sus");

    [TestCase(TestName = "Parser Should Parse Minimum Function Declaration")]
    public void ParserShould_0()
    {
        const string source = "public int32 main() { return 42; }";

        ParserResult result = ParseFunction(source);

        result.Diagnostics
            .Should()
            .BeEmpty();

        FunctionDeclarationSyntax declaration = GetFunction(result);

        declaration.Type
            .Should()
            .Be(SyntaxType.FunctionDeclaration);

        declaration.AccessModifier.Type
            .Should()
            .Be(SyntaxType.AccessModifierKeyword);

        declaration.AccessModifier.IsMissing
            .Should()
            .BeFalse();

        declaration.ReturnType.Type
            .Should()
            .Be(SyntaxType.IdentifierToken);

        declaration.ReturnType.IsMissing
            .Should()
            .BeFalse();

        declaration.Name.Type
            .Should()
            .Be(SyntaxType.IdentifierToken);

        declaration.Name.IsMissing
            .Should()
            .BeFalse();

        declaration.OpenParenthesisToken.Type
            .Should()
            .Be(SyntaxType.OpenParenthesisToken);

        declaration.CloseParenthesisToken.Type
            .Should()
            .Be(SyntaxType.CloseParenthesisToken);

        declaration.Body.Type
            .Should()
            .Be(SyntaxType.Block);

        declaration.Body.OpenBraceToken.Type
            .Should()
            .Be(SyntaxType.OpenBraceToken);

        declaration.Body.CloseBraceToken.Type
            .Should()
            .Be(SyntaxType.CloseBraceToken);

        declaration.Body.Statements
            .Should()
            .ContainSingle();

        ReturnStatementSyntax returnStatement = declaration.Body.Statements[0]
            .Should()
            .BeOfType<ReturnStatementSyntax>()
            .Subject;

        returnStatement.ReturnKeyword.Type
            .Should()
            .Be(SyntaxType.ReturnKeyword);

        returnStatement.SemicolonToken.Type
            .Should()
            .Be(SyntaxType.SemicolonToken);
        
        IntegerLiteralExpressionSyntax expression = returnStatement.Expression
            .Should()
            .BeOfType<IntegerLiteralExpressionSyntax>()
            .Subject;

        expression.Type
            .Should()
            .Be(SyntaxType.IntegerLiteralExpression);

        expression.LiteralToken.Type
            .Should()
            .Be(SyntaxType.IntegerLiteralToken);

        expression.LiteralToken.IsMissing
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
            .Be(source.Length);
    }

    private static FunctionDeclarationSyntax GetFunction(ParserResult result)
        => result.Tree.Root
            .Should()
            .BeOfType<FunctionDeclarationSyntax>()
            .Subject;

    private static ParserResult ParseFunction(string source)
    {
        LexerResult lexerResult = Lex(source);

        return new SourceParser().ParseFunctionDeclaration(lexerResult, CancellationToken.None);
    }

    private static SourceFileSyntax GetSourceFile(ParserResult result)
        => result.Tree.Root
            .Should()
            .BeOfType<SourceFileSyntax>()
            .Subject;

    private static ParserResult ParseSourceFile(string source)
    {
        LexerResult lexerResult = Lex(source);

        return new SourceParser().ParseSourceFile(lexerResult, CancellationToken.None);
    }

    private static LexerResult Lex(string source)
    {
        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);

        LexerResult result = new SourceLexer().Lex(snapshot, CancellationToken.None);

        result.Diagnostics
            .Should()
            .BeEmpty();

        return result;
    }
}
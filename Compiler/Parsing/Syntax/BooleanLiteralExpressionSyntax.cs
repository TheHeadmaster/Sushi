using Sushi.Lexing.Tokenization;
using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents one of Sushi's two Boolean literal expressions.
/// </summary>
public sealed class BooleanLiteralExpressionSyntax : ExpressionSyntax
{
    /// <inheritdoc />
    public override SyntaxType Type => SyntaxType.BooleanLiteralExpression;

    /// <inheritdoc />
    public override SourceSpan Span => this.LiteralToken.Span;

    /// <summary>
    /// Gets the source-backed true or false literal.
    /// </summary>
    public SyntaxToken LiteralToken { get; }

    /// <summary>
    /// Creates a Boolean literal expression from its original lexical element.
    /// </summary>
    /// <param name="literalToken">
    /// The Boolean literal, classified as a BooleanLiteralToken by the parser.
    /// </param>
    public BooleanLiteralExpressionSyntax(SyntaxToken literalToken)
    {
        if (literalToken.Type is not SyntaxType.BooleanLiteralToken
            || literalToken.SourceToken is not LexToken sourceToken
            || sourceToken.Type is not LexTokenType.BooleanLiteral)
        {
            throw new ArgumentException("Boolean literal expressions require a source-backed Boolean literal token.", nameof(literalToken));
        }

        ReadOnlySpan<byte> spelling = sourceToken.Span.Snapshot.Bytes.Span[sourceToken.Span.Start..sourceToken.Span.End];

        if (!spelling.SequenceEqual("true"u8) && !spelling.SequenceEqual("false"u8))
        {
            throw new ArgumentException("Boolean literal expressions require the exact spelling true or false.", nameof(literalToken));
        }

        this.LiteralToken = literalToken;
    }
}
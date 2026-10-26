using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents an identifier used as an expression, without performing semantic name resolution.
/// </summary>
public sealed class IdentifierExpressionSyntax : ExpressionSyntax
{
    /// <inheritdoc />
    public override SyntaxType Type => SyntaxType.IdentifierExpression;

    /// <inheritdoc />
    public override SourceSpan Span => this.IdentifierToken.Span;

    /// <summary>
    /// Gets the source-backed or recovered identifier.
    /// </summary>
    public SyntaxToken IdentifierToken { get; }

    /// <summary>
    /// Creates an identifier expression.
    /// </summary>
    /// <param name="identifierToken">
    /// The identifier's syntactic token.
    /// </param>
    public IdentifierExpressionSyntax(SyntaxToken identifierToken)
    {
        if (identifierToken.Type is not SyntaxType.IdentifierToken and not SyntaxType.EscapedIdentifierToken)
        {
            throw new ArgumentException("An identifier expression requires an identifier token.", nameof(identifierToken));
        }

        this.IdentifierToken = identifierToken;
    }
}
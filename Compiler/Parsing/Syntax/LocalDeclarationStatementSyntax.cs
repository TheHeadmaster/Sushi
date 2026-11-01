using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents an explicitly typed local declaration with a required initializer.
/// </summary>
public sealed class LocalDeclarationStatementSyntax : StatementSyntax
{
    /// <inheritdoc />
    public override SyntaxType Type => SyntaxType.LocalDeclarationStatement;

    /// <inheritdoc />
    public override SourceSpan Span => new(this.TypeToken.Span.Snapshot, this.TypeToken.Span.Start, this.SemicolonToken.Span.End);

    /// <summary>
    /// Gets the declared type's source token.
    /// </summary>
    public SyntaxToken TypeToken { get; }

    /// <summary>
    /// Gets the declared local name.
    /// </summary>
    public SyntaxToken NameToken { get; }

    /// <summary>
    /// Gets the required initializer separator.
    /// </summary>
    public SyntaxToken EqualsToken { get; }

    /// <summary>
    /// Gets the expression evaluated before the local becomes visible.
    /// </summary>
    public ExpressionSyntax Initializer { get; }

    /// <summary>
    /// Gets the terminating semicolon.
    /// </summary>
    public SyntaxToken SemicolonToken { get; }

    /// <summary>
    /// Creates a source-ordered local declaration while preserving recovered tokens.
    /// </summary>
    /// <param name="typeToken">
    /// The explicit declared type.
    /// </param>
    /// <param name="nameToken">
    /// The local's name.
    /// </param>
    /// <param name="equalsToken">
    /// The initializer separator, possibly missing.
    /// </param>
    /// <param name="initializer">
    /// The required initializer, possibly containing recovered syntax.
    /// </param>
    /// <param name="semicolonToken">
    /// The statement terminator, possibly missing.
    /// </param>
    public LocalDeclarationStatementSyntax(SyntaxToken typeToken, SyntaxToken nameToken, SyntaxToken equalsToken, ExpressionSyntax initializer, SyntaxToken semicolonToken)
    {
        ArgumentNullException.ThrowIfNull(initializer);

        if (typeToken.Type is not (SyntaxType.IdentifierToken or SyntaxType.EscapedIdentifierToken)
            || nameToken.Type is not (SyntaxType.IdentifierToken or SyntaxType.EscapedIdentifierToken)
            || equalsToken.Type is not SyntaxType.OperatorToken
            || semicolonToken.Type is not SyntaxType.SemicolonToken)
        {
            throw new ArgumentException("Invalid local-declaration token roles.");
        }

        SourceSpan[] spans = [typeToken.Span, nameToken.Span, equalsToken.Span, initializer.Span, semicolonToken.Span];

        for (int i = 1; i < spans.Length; i++)
        {
            if (!ReferenceEquals(spans[i].Snapshot, spans[0].Snapshot) || spans[i].Start < spans[i - 1].End)
            {
                throw new ArgumentException("Local declaration syntax must share a source snapshot and occur in source order.");
            }
        }

        this.TypeToken = typeToken;
        this.NameToken = nameToken;
        this.EqualsToken = equalsToken;
        this.Initializer = initializer;
        this.SemicolonToken = semicolonToken;
    }
}
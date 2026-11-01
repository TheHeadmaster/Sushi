using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents ordinary or compound assignment to a named local as a
/// semicolon-terminated statement. The operator remains source-backed
/// so semantic binding can select its exact built-in operation.
/// </summary>
public sealed class LocalAssignmentStatementSyntax : StatementSyntax
{
    /// <inheritdoc />
    public override SyntaxType Type => SyntaxType.LocalAssignmentStatement;

    /// <inheritdoc />
    public override SourceSpan Span => new(this.NameToken.Span.Snapshot, this.NameToken.Span.Start, this.SemicolonToken.Span.End);

    /// <summary>
    /// Gets the identifier naming the assignment target.
    /// </summary>
    public SyntaxToken NameToken { get; }

    /// <summary>
    /// Gets the ordinary or compound assignment operator.
    /// </summary>
    public SyntaxToken OperatorToken { get; }

    /// <summary>
    /// Gets the expression evaluated to produce the value to write.
    /// </summary>
    public ExpressionSyntax Value { get; }

    /// <summary>
    /// Gets the terminating semicolon.
    /// </summary>
    public SyntaxToken SemicolonToken { get; }

    /// <summary>
    /// Creates an assignment statement while preserving source order
    /// and recovered syntax.
    /// </summary>
    /// <param name="nameToken">
    /// The identifier denoting the writable local.
    /// </param>
    /// <param name="operatorToken">
    /// The source-backed assignment operator.
    /// </param>
    /// <param name="value">
    /// The right-hand expression.
    /// </param>
    /// <param name="semicolonToken">
    /// The statement terminator.
    /// </param>
    public LocalAssignmentStatementSyntax(SyntaxToken nameToken, SyntaxToken operatorToken, ExpressionSyntax value, SyntaxToken semicolonToken)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (nameToken.Type is not (SyntaxType.IdentifierToken or SyntaxType.EscapedIdentifierToken)
            || operatorToken.Type is not SyntaxType.OperatorToken
            || semicolonToken.Type is not SyntaxType.SemicolonToken)
        {
            throw new ArgumentException("Assignment statements require an identifier, assignment operator, and semicolon.");
        }

        SourceSpan[] spans = [nameToken.Span, operatorToken.Span, value.Span, semicolonToken.Span];

        for (int i = 1; i < spans.Length; i++)
        {
            if (!ReferenceEquals(spans[i].Snapshot, spans[0].Snapshot) || spans[i].Start < spans[i - 1].End)
            {
                throw new ArgumentException("Assignment syntax must share a source snapshot and occur in source order.");
            }
        }

        this.NameToken = nameToken;
        this.OperatorToken = operatorToken;
        this.Value = value;
        this.SemicolonToken = semicolonToken;
    }
}
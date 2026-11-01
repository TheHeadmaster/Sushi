using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents assignment to a named local as a semicolon-terminated statement.
/// The target is preserved independently from the expression being assigned.
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
    /// Gets the assignment operator, possibly synthesized during recovery.
    /// </summary>
    public SyntaxToken EqualsToken { get; }

    /// <summary>
    /// Gets the expression evaluated to produce the replacement value.
    /// </summary>
    public ExpressionSyntax Value { get; }

    /// <summary>
    /// Gets the terminating semicolon.
    /// </summary>
    public SyntaxToken SemicolonToken { get; }

    /// <summary>
    /// Creates an assignment statement while preserving source order and recovery information.
    /// </summary>
    /// <param name="nameToken">
    /// The source identifier identifying writable storage.
    /// </param>
    /// <param name="equalsToken">
    /// The ordinary assignment operator.
    /// </param>
    /// <param name="value">
    /// The expression to evaluate before writing to the target.
    /// </param>
    /// <param name="semicolonToken">
    /// The statement terminator.
    /// </param>
    public LocalAssignmentStatementSyntax(SyntaxToken nameToken, SyntaxToken equalsToken, ExpressionSyntax value, SyntaxToken semicolonToken)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (nameToken.Type is not (SyntaxType.IdentifierToken or SyntaxType.EscapedIdentifierToken)
            || equalsToken.Type is not SyntaxType.OperatorToken
            || semicolonToken.Type is not SyntaxType.SemicolonToken)
        {
            throw new ArgumentException("Assignment statements require an identifier, assignment operator, and semicolon.");
        }

        SourceSpan[] spans = [nameToken.Span, equalsToken.Span, value.Span, semicolonToken.Span];

        for (int i = 1; i < spans.Length; i++)
        {
            if (!ReferenceEquals(spans[i].Snapshot, spans[0].Snapshot) || spans[i].Start < spans[i - 1].End)
            {
                throw new ArgumentException("Assignment syntax must share a source snapshot and occur in source order.");
            }
        }

        this.NameToken = nameToken;
        this.EqualsToken = equalsToken;
        this.Value = value;
        this.SemicolonToken = semicolonToken;
    }
}
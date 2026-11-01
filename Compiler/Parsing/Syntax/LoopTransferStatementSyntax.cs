using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents break or continue with one or more repeated, identical keywords.
/// The number of keywords identifies the enclosing loop being targeted.
/// </summary>
public sealed class LoopTransferStatementSyntax : StatementSyntax
{
    /// <inheritdoc />
    public override SyntaxType Type => SyntaxType.LoopTransferStatement;

    /// <inheritdoc />
    public override SourceSpan Span => new(this.Keywords[0].Span.Snapshot, this.Keywords[0].Span.Start, this.SemicolonToken.Span.End);

    /// <summary>
    /// Gets the repeated break or continue keywords in source order.
    /// </summary>
    public IReadOnlyList<SyntaxToken> Keywords { get; }

    /// <summary>
    /// Gets the terminating semicolon.
    /// </summary>
    public SyntaxToken SemicolonToken { get; }

    /// <summary>
    /// Gets whether this statement exits the selected loop rather than
    /// continuing its next iteration.
    /// </summary>
    public bool IsBreak => this.Keywords[0].Type is SyntaxType.BreakKeyword;

    /// <summary>
    /// Gets the number of enclosing loops to traverse.
    /// One selects the innermost loop.
    /// </summary>
    public int Depth => this.Keywords.Count;

    /// <summary>
    /// Creates a loop transfer from a nonempty sequence of identical keywords.
    /// </summary>
    /// <param name="keywords">
    /// The repeated transfer keywords.
    /// </param>
    /// <param name="semicolonToken">
    /// The terminating semicolon, potentially synthesized for recovery.
    /// </param>
    public LoopTransferStatementSyntax(IReadOnlyList<SyntaxToken> keywords, SyntaxToken semicolonToken)
    {
        ArgumentNullException.ThrowIfNull(keywords);

        if (keywords.Count == 0 || keywords[0].Type is not (SyntaxType.BreakKeyword or SyntaxType.ContinueKeyword)
            || semicolonToken.Type is not SyntaxType.SemicolonToken)
        {
            throw new ArgumentException("Loop transfers require at least one break or continue keyword and a semicolon.");
        }

        SyntaxType type = keywords[0].Type;
        SourceSnapshot snapshot = keywords[0].Span.Snapshot;
        int previousEnd = keywords[0].Span.Start;

        foreach (SyntaxToken keyword in keywords)
        {
            if (keyword.Type != type || !ReferenceEquals(keyword.Span.Snapshot, snapshot) || keyword.Span.Start < previousEnd)
            {
                throw new ArgumentException("Loop transfer keywords must be identical, source-ordered, and belong to one snapshot.");
            }

            previousEnd = keyword.Span.End;
        }

        if (!ReferenceEquals(semicolonToken.Span.Snapshot, snapshot) || semicolonToken.Span.Start < previousEnd)
        {
            throw new ArgumentException("The loop transfer terminator must follow its keywords.");
        }

        this.Keywords = [.. keywords];
        this.SemicolonToken = semicolonToken;
    }
}
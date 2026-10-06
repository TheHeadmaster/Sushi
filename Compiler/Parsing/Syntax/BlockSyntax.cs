using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents a braced statement block.
/// </summary>
public sealed class BlockSyntax : SyntaxNode
{
    /// <inheritdoc />
    public override SyntaxType Type => SyntaxType.Block;

    /// <inheritdoc />
    public override SourceSpan Span => new(this.OpenBraceToken.Span.Snapshot, this.OpenBraceToken.Span.Start, this.CloseBraceToken.Span.End);

    /// <summary>
    /// Gets the opening brace.
    /// </summary>
    public SyntaxToken OpenBraceToken { get; }

    /// <summary>
    /// Gets the statements contained by the block.
    /// </summary>
    public IReadOnlyList<StatementSyntax> Statements { get; }

    /// <summary>
    /// Gets the closing brace.
    /// </summary>
    public SyntaxToken CloseBraceToken { get; }

    /// <summary>
    /// Creates a braced statement block.
    /// </summary>
    /// <param name="openBraceToken">
    /// The opening brace.
    /// </param>
    /// <param name="statements">
    /// The statements contained by the block.
    /// </param>
    /// <param name="closeBraceToken">
    /// The closing brace.
    /// </param>
    public BlockSyntax(SyntaxToken openBraceToken, IReadOnlyList<StatementSyntax> statements, SyntaxToken closeBraceToken)
    {
        ArgumentNullException.ThrowIfNull(statements);

        if (openBraceToken.Type is not SyntaxType.OpenBraceToken)
        {
            throw new ArgumentException("Blocks require an opening brace token.", nameof(openBraceToken));
        }

        if (closeBraceToken.Type is not SyntaxType.CloseBraceToken)
        {
            throw new ArgumentException("Blocks require a closing brace token.", nameof(closeBraceToken));
        }

        StatementSyntax[] statementArray = [.. statements];

        SourceSnapshot snapshot = openBraceToken.Span.Snapshot;

        if (statementArray.Any(statement => !ReferenceEquals(statement.Span.Snapshot, snapshot)) || !ReferenceEquals(closeBraceToken.Span.Snapshot, snapshot))
        {
            throw new ArgumentException("All block syntax must originate from the same source snapshot.");
        }

        for (int i = 1; i < statementArray.Length; i++)
        {
            if (statementArray[i - 1].Span.End > statementArray[i].Span.Start)
            {
                throw new ArgumentException("Block statements must occur in source order without overlapping.", nameof(statements));
            }
        }

        this.OpenBraceToken = openBraceToken;
        this.Statements = statementArray;
        this.CloseBraceToken = closeBraceToken;
    }
}
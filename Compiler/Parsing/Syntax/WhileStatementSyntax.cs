using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents a pre-test while loop with a Boolean condition and a required
/// braced body. The condition is reevaluated before every iteration.
/// </summary>
public sealed class WhileStatementSyntax : StatementSyntax
{
    /// <inheritdoc />
    public override SyntaxType Type => SyntaxType.WhileStatement;

    /// <inheritdoc />
    public override SourceSpan Span => new(this.WhileKeyword.Span.Snapshot, this.WhileKeyword.Span.Start, this.Body.Span.End);

    /// <summary>
    /// Gets the keyword introducing the loop.
    /// </summary>
    public SyntaxToken WhileKeyword { get; }

    /// <summary>
    /// Gets the opening parenthesis.
    /// </summary>
    public SyntaxToken OpenParenthesisToken { get; }

    /// <summary>
    /// Gets the expression required to produce a Boolean value.
    /// </summary>
    public ExpressionSyntax Condition { get; }

    /// <summary>
    /// Gets the closing parenthesis.
    /// </summary>
    public SyntaxToken CloseParenthesisToken { get; }

    /// <summary>
    /// Gets the required braced loop body.
    /// </summary>
    public BlockSyntax Body { get; }

    /// <summary>
    /// Creates source-ordered while syntax, preserving recovered punctuation.
    /// </summary>
    /// <param name="whileKeyword">
    /// The loop keyword.
    /// </param>
    /// <param name="openParenthesisToken">
    /// The opening condition parenthesis.
    /// </param>
    /// <param name="condition">
    /// The loop condition.
    /// </param>
    /// <param name="closeParenthesisToken">
    /// The closing condition parenthesis.
    /// </param>
    /// <param name="body">
    /// The braced body.
    /// </param>
    public WhileStatementSyntax(SyntaxToken whileKeyword, SyntaxToken openParenthesisToken, ExpressionSyntax condition, SyntaxToken closeParenthesisToken, BlockSyntax body)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(body);

        if (whileKeyword.Type is not SyntaxType.WhileKeyword
            || openParenthesisToken.Type is not SyntaxType.OpenParenthesisToken
            || closeParenthesisToken.Type is not SyntaxType.CloseParenthesisToken)
        {
            throw new ArgumentException("A while statement requires its keyword and parenthesis tokens.");
        }

        SourceSnapshot snapshot = whileKeyword.Span.Snapshot;
        SourceSpan[] spans = [openParenthesisToken.Span, condition.Span, closeParenthesisToken.Span, body.Span];
        int previousEnd = whileKeyword.Span.End;

        foreach (SourceSpan span in spans)
        {
            if (!ReferenceEquals(span.Snapshot, snapshot) || span.Start < previousEnd)
            {
                throw new ArgumentException("While statement syntax must share a source snapshot and occur in source order.");
            }

            previousEnd = span.End;
        }

        this.WhileKeyword = whileKeyword;
        this.OpenParenthesisToken = openParenthesisToken;
        this.Condition = condition;
        this.CloseParenthesisToken = closeParenthesisToken;
        this.Body = body;
    }
}
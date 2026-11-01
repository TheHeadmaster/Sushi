using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents a post-test loop whose braced body executes before its Boolean
/// condition. The trailing while clause has no statement semicolon.
/// </summary>
public sealed class DoWhileStatementSyntax : StatementSyntax
{
    /// <inheritdoc />
    public override SyntaxType Type => SyntaxType.DoWhileStatement;

    /// <inheritdoc />
    public override SourceSpan Span => new(this.DoKeyword.Span.Snapshot, this.DoKeyword.Span.Start, this.CloseParenthesisToken.Span.End);

    /// <summary>
    /// Gets the keyword beginning the post-test loop.
    /// </summary>
    public SyntaxToken DoKeyword { get; }

    /// <summary>
    /// Gets the required braced loop body.
    /// </summary>
    public BlockSyntax Body { get; }

    /// <summary>
    /// Gets the keyword introducing the post-body condition.
    /// </summary>
    public SyntaxToken WhileKeyword { get; }

    /// <summary>
    /// Gets the opening parenthesis of the trailing condition.
    /// </summary>
    public SyntaxToken OpenParenthesisToken { get; }

    /// <summary>
    /// Gets the expression required to produce a Boolean condition.
    /// </summary>
    public ExpressionSyntax Condition { get; }

    /// <summary>
    /// Gets the closing parenthesis that terminates this statement.
    /// </summary>
    public SyntaxToken CloseParenthesisToken { get; }

    /// <summary>
    /// Creates a source-ordered post-test loop while preserving recovered tokens.
    /// </summary>
    /// <param name="doKeyword">
    /// The opening do keyword.
    /// </param>
    /// <param name="body">
    /// The required braced body.
    /// </param>
    /// <param name="whileKeyword">
    /// The keyword introducing the trailing condition.
    /// </param>
    /// <param name="openParenthesisToken">
    /// The opening parenthesis of that condition.
    /// </param>
    /// <param name="condition">
    /// The expression to evaluate after each completed iteration.
    /// </param>
    /// <param name="closeParenthesisToken">
    /// The closing condition parenthesis.
    /// </param>
    public DoWhileStatementSyntax(SyntaxToken doKeyword, BlockSyntax body, SyntaxToken whileKeyword, SyntaxToken openParenthesisToken, ExpressionSyntax condition, SyntaxToken closeParenthesisToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(condition);

        if (doKeyword.Type is not SyntaxType.DoKeyword
            || whileKeyword.Type is not SyntaxType.WhileKeyword
            || openParenthesisToken.Type is not SyntaxType.OpenParenthesisToken
            || closeParenthesisToken.Type is not SyntaxType.CloseParenthesisToken)
        {
            throw new ArgumentException("A do while statement requires do, while, and condition parenthesis tokens.");
        }

        SourceSnapshot snapshot = doKeyword.Span.Snapshot;
        SourceSpan[] spans = [body.Span, whileKeyword.Span, openParenthesisToken.Span, condition.Span, closeParenthesisToken.Span];
        int previousEnd = doKeyword.Span.End;

        foreach (SourceSpan span in spans)
        {
            if (!ReferenceEquals(span.Snapshot, snapshot) || span.Start < previousEnd)
            {
                throw new ArgumentException("Do while syntax must share a source snapshot and occur in source order.");
            }

            previousEnd = span.End;
        }

        this.DoKeyword = doKeyword;
        this.Body = body;
        this.WhileKeyword = whileKeyword;
        this.OpenParenthesisToken = openParenthesisToken;
        this.Condition = condition;
        this.CloseParenthesisToken = closeParenthesisToken;
    }
}
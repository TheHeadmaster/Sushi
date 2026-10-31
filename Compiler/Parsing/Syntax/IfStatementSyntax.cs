
using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents a braced conditional statement with an optional else branch or else-if continuation.
/// </summary>
public sealed class IfStatementSyntax : StatementSyntax
{
    /// <inheritdoc />
    public override SyntaxType Type => SyntaxType.IfStatement;

    /// <inheritdoc />
    public override SourceSpan Span => new(this.IfKeyword.Span.Snapshot, this.IfKeyword.Span.Start, this.ElseIf?.Span.End ?? this.ElseBlock?.Span.End ?? this.ThenBlock.Span.End);

    /// <summary>
    /// Gets the keyword introducing the conditional statement.
    /// </summary>
    public SyntaxToken IfKeyword { get; }

    /// <summary>
    /// Gets the opening condition parenthesis.
    /// </summary>
    public SyntaxToken OpenParenthesisToken { get; }

    /// <summary>
    /// Gets the expression required to produce a Boolean condition.
    /// </summary>
    public ExpressionSyntax Condition { get; }

    /// <summary>
    /// Gets the closing condition parenthesis.
    /// </summary>
    public SyntaxToken CloseParenthesisToken { get; }

    /// <summary>
    /// Gets the braced body selected when the condition is true.
    /// </summary>
    public BlockSyntax ThenBlock { get; }

    /// <summary>
    /// Gets the optional keyword introducing the alternative branch.
    /// </summary>
    public SyntaxToken? ElseKeyword { get; }

    /// <summary>
    /// Gets the braced final alternative, when one is declared.
    /// </summary>
    public BlockSyntax? ElseBlock { get; }

    /// <summary>
    /// Gets the directly chained conditional alternative, when one is declared.
    /// </summary>
    public IfStatementSyntax? ElseIf { get; }

    /// <summary>
    /// Creates a source-ordered conditional statement, preserving its optional alternative.
    /// </summary>
    /// <param name="ifKeyword">
    /// The opening if keyword.
    /// </param>
    /// <param name="openParenthesisToken">
    /// The opening parenthesis.
    /// </param>
    /// <param name="condition">
    /// The condition expression.
    /// </param>
    /// <param name="closeParenthesisToken">
    /// The closing parenthesis.
    /// </param>
    /// <param name="thenBlock">
    /// The required braced true branch.
    /// </param>
    /// <param name="elseKeyword">
    /// The optional else keyword.
    /// </param>
    /// <param name="elseBlock">
    /// The optional final braced alternative.
    /// </param>
    /// <param name="elseIf">
    /// The optional directly chained conditional alternative.
    /// </param>
    public IfStatementSyntax(SyntaxToken ifKeyword, SyntaxToken openParenthesisToken, ExpressionSyntax condition, SyntaxToken closeParenthesisToken, BlockSyntax thenBlock, SyntaxToken? elseKeyword, BlockSyntax? elseBlock, IfStatementSyntax? elseIf)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(thenBlock);

        if (ifKeyword.Type is not SyntaxType.IfKeyword)
        {
            throw new ArgumentException("Conditional statements require an if keyword.", nameof(ifKeyword));
        }

        if (openParenthesisToken.Type is not SyntaxType.OpenParenthesisToken || closeParenthesisToken.Type is not SyntaxType.CloseParenthesisToken)
        {
            throw new ArgumentException("Conditional statements require parenthesis tokens.");
        }

        if (elseKeyword is SyntaxToken keyword && keyword.Type is not SyntaxType.ElseKeyword)
        {
            throw new ArgumentException("Conditional alternatives require an else keyword.", nameof(elseKeyword));
        }

        if ((elseKeyword is null) != (elseBlock is null && elseIf is null)
            || (elseBlock is not null && elseIf is not null))
        {
            throw new ArgumentException("An if statement may have exactly one alternative when else is present.");
        }

        SourceSnapshot snapshot = ifKeyword.Span.Snapshot;
        SourceSpan[] requiredSpans = [openParenthesisToken.Span, condition.Span, closeParenthesisToken.Span, thenBlock.Span];

        if (requiredSpans.Any(span => !ReferenceEquals(span.Snapshot, snapshot)))
        {
            throw new ArgumentException("Conditional syntax must belong to one source snapshot.");
        }

        int previousEnd = ifKeyword.Span.End;

        foreach (SourceSpan span in requiredSpans)
        {
            if (span.Start < previousEnd)
            {
                throw new ArgumentException("Conditional syntax must occur in source order.");
            }

            previousEnd = span.End;
        }

        if (elseKeyword is SyntaxToken alternativeKeyword)
        {
            if (!ReferenceEquals(alternativeKeyword.Span.Snapshot, snapshot) || alternativeKeyword.Span.Start < previousEnd)
            {
                throw new ArgumentException("The else keyword must follow the true branch.", nameof(elseKeyword));
            }

            SourceSpan alternativeSpan = elseBlock?.Span ?? elseIf!.Span;

            if (!ReferenceEquals(alternativeSpan.Snapshot, snapshot) || alternativeSpan.Start < alternativeKeyword.Span.End)
            {
                throw new ArgumentException("The alternative must follow its else keyword.");
            }
        }

        this.IfKeyword = ifKeyword;
        this.OpenParenthesisToken = openParenthesisToken;
        this.Condition = condition;
        this.CloseParenthesisToken = closeParenthesisToken;
        this.ThenBlock = thenBlock;
        this.ElseKeyword = elseKeyword;
        this.ElseBlock = elseBlock;
        this.ElseIf = elseIf;
    }
}
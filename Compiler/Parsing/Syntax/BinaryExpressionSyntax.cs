using Sushi.Lexing.Tokenization;
using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents a binary arithmetic expression with explicit left and right operands.
/// </summary>
public sealed class BinaryExpressionSyntax : ExpressionSyntax
{
    /// <inheritdoc />
    public override SyntaxType Type => SyntaxType.BinaryExpression;

    /// <inheritdoc />
    public override SourceSpan Span => new(this.Left.Span.Snapshot, this.Left.Span.Start, this.Right.Span.End);

    /// <summary>
    /// Gets the expression to the left of the operator.
    /// </summary>
    public ExpressionSyntax Left { get; }

    /// <summary>
    /// Gets the source-backed arithmetic operator.
    /// </summary>
    public SyntaxToken OperatorToken { get; }

    /// <summary>
    /// Gets the expression to the right of the operator.
    /// </summary>
    public ExpressionSyntax Right { get; }

    /// <summary>
    /// Creates a binary expression, retaining the original source spans and operand grouping.
    /// </summary>
    /// <param name="left">
    /// The left operand.
    /// </param>
    /// <param name="operatorToken">
    /// The binary operator between the operands.
    /// </param>
    /// <param name="right">
    /// The right operand.
    /// </param>
    public BinaryExpressionSyntax(ExpressionSyntax left, SyntaxToken operatorToken, ExpressionSyntax right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (operatorToken.Type is not SyntaxType.OperatorToken
            || operatorToken.SourceToken is not LexToken sourceToken
            || sourceToken.Type is not LexTokenType.Operator)
        {
            throw new ArgumentException("Binary expressions require a source-backed operator token.", nameof(operatorToken));
        }

        SourceSpan leftSpan = left.Span;
        SourceSpan operatorSpan = operatorToken.Span;
        SourceSpan rightSpan = right.Span;

        if (!ReferenceEquals(leftSpan.Snapshot, operatorSpan.Snapshot)
            || !ReferenceEquals(leftSpan.Snapshot, rightSpan.Snapshot))
        {
            throw new ArgumentException("All binary-expression syntax must originate from the same source snapshot.");
        }

        if (leftSpan.End > operatorSpan.Start || operatorSpan.End > rightSpan.Start)
        {
            throw new ArgumentException("Binary-expression operands and operators must occur in source order without overlapping.");
        }

        this.Left = left;
        this.OperatorToken = operatorToken;
        this.Right = right;
    }
}
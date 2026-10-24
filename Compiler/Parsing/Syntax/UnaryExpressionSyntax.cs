using Sushi.Lexing.Tokenization;
using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents a prefix unary expression consisting of an operator and
/// the expression to which it applies.
/// </summary>
public sealed class UnaryExpressionSyntax : ExpressionSyntax
{
    /// <inheritdoc />
    public override SyntaxType Type => SyntaxType.UnaryExpression;

    /// <inheritdoc />
    public override SourceSpan Span => new(this.OperatorToken.Span.Snapshot, this.OperatorToken.Span.Start, this.Operand.Span.End);

    /// <summary>
    /// Gets the source-backed prefix operator.
    /// </summary>
    public SyntaxToken OperatorToken { get; }

    /// <summary>
    /// Gets the operand, including recovered missing syntax when present.
    /// </summary>
    public ExpressionSyntax Operand { get; }

    /// <summary>
    /// Creates a prefix unary expression, preserving its original source coordinates.
    /// </summary>
    /// <param name="operatorToken">
    /// The source-backed unary operator, currently +, -, or ~.
    /// </param>
    /// <param name="operand">
    /// The expression to which the operator applies.
    /// </param>
    public UnaryExpressionSyntax(SyntaxToken operatorToken, ExpressionSyntax operand)
    {
        ArgumentNullException.ThrowIfNull(operand);

        if (operatorToken.Type is not SyntaxType.OperatorToken
            || operatorToken.SourceToken is not LexToken sourceToken
            || sourceToken.Type is not LexTokenType.Operator
            || sourceToken.Span.Length != 1)
        {
            throw new ArgumentException("Unary expressions require a single-byte, source-backed operator token.", nameof(operatorToken));
        }

        byte spelling = sourceToken.Span.Snapshot.Bytes.Span[sourceToken.Span.Start];

        if (spelling is not ((byte)'+' or (byte)'-' or (byte)'~'))
        {
            throw new ArgumentException("The supplied operator is not a supported prefix unary operator.", nameof(operatorToken));
        }

        SourceSpan operatorSpan = operatorToken.Span;
        SourceSpan operandSpan = operand.Span;

        if (!ReferenceEquals(operatorSpan.Snapshot, operandSpan.Snapshot))
        {
            throw new ArgumentException("The unary operator and operand must originate from the same source snapshot.");
        }

        if (operatorSpan.End > operandSpan.Start)
        {
            throw new ArgumentException("A unary operand must follow its prefix operator without overlapping.", nameof(operand));
        }

        this.OperatorToken = operatorToken;
        this.Operand = operand;
    }
}
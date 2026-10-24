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
    /// Gets the source-backed prefix operator, including keyword operators such as not.
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
    /// The source-backed unary operator, currently +, -, ~, or not.
    /// </param>
    /// <param name="operand">
    /// The expression to which the operator applies.
    /// </param>
    public UnaryExpressionSyntax(SyntaxToken operatorToken, ExpressionSyntax operand)
    {
        ArgumentNullException.ThrowIfNull(operand);

        if (operatorToken.SourceToken is not LexToken sourceToken
            || !IsSupportedPrefixOperator(operatorToken.Type, sourceToken))
        {
            throw new ArgumentException("Unary expressions require a supported source-backed prefix operator.", nameof(operatorToken));
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

    private static bool IsSupportedPrefixOperator(SyntaxType type, LexToken token)
    {
        ReadOnlySpan<byte> spelling = token.Span.Snapshot.Bytes.Span[token.Span.Start..token.Span.End];

        return type switch
        {
            SyntaxType.OperatorToken => token.Type is LexTokenType.Operator
                && spelling.Length == 1
                && spelling[0] is (byte)'+' or (byte)'-' or (byte)'~',

            SyntaxType.NotKeyword => token.Type is LexTokenType.Keyword
                && spelling.SequenceEqual("not"u8),

            _ => false
        };
    }
}
using Sushi.Lexing.Tokenization;
using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents a binary expression with explicit left and right operands.
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
    /// Gets the source-backed binary operator.
    /// </summary>
    public SyntaxToken OperatorToken { get; }

    /// <summary>
    /// Gets the expression to the right of the operator.
    /// </summary>
    public ExpressionSyntax Right { get; }

    private static readonly byte[] supportedBinaryOperators = [
        (byte)'+',
        (byte)'-',
        (byte)'*',
        (byte)'/',
        (byte)'%',
        (byte)'<',
        (byte)'>'
    ];

    /// <summary>
    /// Creates a binary expression, retaining the original source spans and operand grouping.
    /// </summary>
    /// <param name="left">
    /// The left operand.
    /// </param>
    /// <param name="operatorToken">
    /// The source-backed binary operator.
    /// </param>
    /// <param name="right">
    /// The right operand.
    /// </param>
    public BinaryExpressionSyntax(ExpressionSyntax left, SyntaxToken operatorToken, ExpressionSyntax right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (operatorToken.SourceToken is not LexToken sourceToken
            || !IsSupportedBinaryOperator(operatorToken.Type, sourceToken))
        {
            throw new ArgumentException("Binary expressions require a supported source-backed binary operator.", nameof(operatorToken));
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

    private static bool IsSupportedBinaryOperator(SyntaxType type, LexToken token)
    {
        ReadOnlySpan<byte> spelling = token.Span.Snapshot.Bytes.Span[token.Span.Start..token.Span.End];

        return type switch
        {
            SyntaxType.OperatorToken => token.Type is LexTokenType.Operator
                && (
                    (spelling.Length == 1 && supportedBinaryOperators.Contains(spelling[0]))
                    || spelling.SequenceEqual("<<"u8)
                    || spelling.SequenceEqual(">>"u8)
                    || spelling.SequenceEqual("=="u8)
                    || spelling.SequenceEqual("!="u8)
                    || spelling.SequenceEqual("<="u8)
                    || spelling.SequenceEqual(">="u8)
                ),

            SyntaxType.AndKeyword => token.Type is LexTokenType.Keyword
                && spelling.SequenceEqual("and"u8),

            SyntaxType.OrKeyword => token.Type is LexTokenType.Keyword
                && spelling.SequenceEqual("or"u8),

            _ => false
        };
    }
}
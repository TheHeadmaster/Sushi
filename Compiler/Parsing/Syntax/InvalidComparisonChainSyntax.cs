using Sushi.Lexing.Tokenization;
using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents an invalid unparenthesized chain of equality or relational comparisons.
/// Retains the offending operator and operand so parsing can continue without
/// assigning valid binary-expression semantics to the chain.
/// </summary>
public sealed class InvalidComparisonChainSyntax : ExpressionSyntax
{
    /// <inheritdoc />
    public override SyntaxType Type => SyntaxType.InvalidComparisonChain;

    /// <inheritdoc />
    public override SourceSpan Span => new(this.Left.Span.Snapshot, this.Left.Span.Start, this.Right.Span.End);

    /// <summary>
    /// Gets the preceding comparison expression or invalid comparison chain.
    /// </summary>
    public ExpressionSyntax Left { get; }

    /// <summary>
    /// Gets the operator that introduced the invalid chain.
    /// </summary>
    public SyntaxToken OperatorToken { get; }

    /// <summary>
    /// Gets the expression following the offending operator.
    /// </summary>
    public ExpressionSyntax Right { get; }

    /// <summary>
    /// Creates a recovery node for a diagnosed non-associative comparison chain.
    /// </summary>
    /// <param name="left">
    /// The preceding comparison expression or chain.
    /// </param>
    /// <param name="operatorToken">
    /// The offending relational or equality operator.
    /// </param>
    /// <param name="right">
    /// The expression following that operator.
    /// </param>
    public InvalidComparisonChainSyntax(ExpressionSyntax left, SyntaxToken operatorToken, ExpressionSyntax right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (operatorToken.Type is not SyntaxType.OperatorToken
            || operatorToken.SourceToken is not LexToken sourceToken
            || sourceToken.Type is not LexTokenType.Operator)
        {
            throw new ArgumentException("An invalid comparison chain requires a source-backed comparison operator.", nameof(operatorToken));
        }

        ReadOnlySpan<byte> spelling = sourceToken.Span.Snapshot.Bytes.Span[sourceToken.Span.Start..sourceToken.Span.End];

        bool isRelational = (spelling.Length == 1 && spelling[0] is (byte)'<' or (byte)'>')
            || spelling.SequenceEqual("<="u8)
            || spelling.SequenceEqual(">="u8);

        bool isEquality = spelling.SequenceEqual("=="u8) || spelling.SequenceEqual("!="u8);

        if (!isRelational && !isEquality)
        {
            throw new ArgumentException("The recovery operator must be a relational or equality comparison.", nameof(operatorToken));
        }

        SourceSpan leftSpan = left.Span;
        SourceSpan operatorSpan = operatorToken.Span;
        SourceSpan rightSpan = right.Span;

        if (!ReferenceEquals(leftSpan.Snapshot, operatorSpan.Snapshot)
            || !ReferenceEquals(leftSpan.Snapshot, rightSpan.Snapshot))
        {
            throw new ArgumentException("All comparison-chain syntax must originate from the same source snapshot.");
        }

        if (leftSpan.End > operatorSpan.Start || operatorSpan.End > rightSpan.Start)
        {
            throw new ArgumentException("Comparison-chain components must occur in source order without overlapping.");
        }

        this.Left = left;
        this.OperatorToken = operatorToken;
        this.Right = right;
    }
}
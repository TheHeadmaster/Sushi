using Sushi.Lexing.Tokenization;
using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents a prefix or postfix integer update expression. Syntax preserves
/// the operand without presuming it denotes a readable and writable location.
/// </summary>
public sealed class IncrementDecrementExpressionSyntax : ExpressionSyntax
{
    /// <inheritdoc />
    public override SyntaxType Type => SyntaxType.IncrementDecrementExpression;

    /// <inheritdoc />
    public override SourceSpan Span
    {
        get
        {
            SourceSpan first = this.IsPrefix ? this.OperatorToken.Span : this.Operand.Span;
            SourceSpan last = this.IsPrefix ? this.Operand.Span : this.OperatorToken.Span;
            return new SourceSpan(first.Snapshot, first.Start, last.End);
        }
    }

    /// <summary>
    /// Gets the source-backed increment or decrement operator.
    /// </summary>
    public SyntaxToken OperatorToken { get; }

    /// <summary>
    /// Gets the expression whose storage location must be updated.
    /// </summary>
    public ExpressionSyntax Operand { get; }

    /// <summary>
    /// Gets whether the updated value is produced rather than the previous value.
    /// </summary>
    public bool IsPrefix { get; }

    /// <summary>
    /// Gets whether the operation increments rather than decrements.
    /// </summary>
    public bool IsIncrement
        => this.OperatorToken.Span.Snapshot.Bytes.Span[this.OperatorToken.Span.Start] == (byte)'+';

    /// <summary>
    /// Creates an update expression with a verified operator and ordered source spans.
    /// </summary>
    /// <param name="operatorToken">
    /// The lexical ++ or -- operator.
    /// </param>
    /// <param name="operand">
    /// The operand, whose storage eligibility is checked during binding.
    /// </param>
    /// <param name="isPrefix">
    /// True when the operator precedes its operand.
    /// </param>
    public IncrementDecrementExpressionSyntax(SyntaxToken operatorToken, ExpressionSyntax operand, bool isPrefix)
    {
        ArgumentNullException.ThrowIfNull(operand);

        if (operatorToken.Type is not SyntaxType.OperatorToken
            || operatorToken.SourceToken is not LexToken sourceToken
            || sourceToken.Type is not LexTokenType.Operator
            || !IsUpdateOperator(operatorToken))
        {
            throw new ArgumentException("Increment and decrement require a source-backed ++ or -- operator.", nameof(operatorToken));
        }

        SourceSpan operatorSpan = operatorToken.Span;
        SourceSpan operandSpan = operand.Span;

        if (!ReferenceEquals(operatorSpan.Snapshot, operandSpan.Snapshot)
            || isPrefix && operatorSpan.End > operandSpan.Start
            || !isPrefix && operandSpan.End > operatorSpan.Start)
        {
            throw new ArgumentException("Update expression components must share a snapshot and occur in source order.");
        }

        this.OperatorToken = operatorToken;
        this.Operand = operand;
        this.IsPrefix = isPrefix;
    }

    private static bool IsUpdateOperator(SyntaxToken token)
    {
        ReadOnlySpan<byte> spelling = token.Span.Snapshot.Bytes.Span[token.Span.Start..token.Span.End];
        return spelling.SequenceEqual("++"u8) || spelling.SequenceEqual("--"u8);
    }
}
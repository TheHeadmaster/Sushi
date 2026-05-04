using System.Diagnostics.CodeAnalysis;
using Sushi.Diagnostics;
using Sushi.Diagnostics.Errors;
using Sushi.Parsing.Nodes.Expressions.Core;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes.Expressions.Infixes;

/// <summary>
/// Represents a binary expression, such as add or multiply.
/// </summary>
/// <param name="token">
/// The operator token of the expression.
/// </param>
/// <param name="left">
/// The left-hand side of the expression.
/// </param>
/// <param name="right">
/// The right-hand side of the expression.
/// </param>
/// <param name="filePath">
/// The path of the file that this node exists in.
/// </param>
public sealed class BinaryExpressionNode([NotNull] Token token, ExpressionNode? left, ExpressionNode? right, [NotNull] string filePath) : ExpressionNode
{
    /// <summary>
    /// The binary operator being used.
    /// </summary>
    public OperatorType Operator { get; set; } = token.Type switch
    {
        TokenType.Dot => OperatorType.Navigation,
        _ => throw new NotImplementedException()
    };

    /// <summary>
    /// The left-hand side of the expression.
    /// </summary>
    public ExpressionNode? Left { get; set; } = left;

    /// <summary>
    /// The right-hand side of the expression.
    /// </summary>
    public ExpressionNode? Right { get; set; } = right;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CompilerMessage> GetMessages()
    {
        if (this.Left is not null)
        {
            await foreach (CompilerMessage message in this.Left.GetMessages())
            {
                yield return message;
            }
        }
        else
        {
            yield return new EmptyBinaryExpressionError(token, "left", filePath);
        }

        if (this.Right is not null)
        {
            await foreach (CompilerMessage message in this.Right.GetMessages())
            {
                yield return message;
            }
        }
        else
        {
            yield return new EmptyBinaryExpressionError(token, "right", filePath);
        }
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<Token> GetTokens()
    {
        if (this.Left is not null)
        {
            await foreach (Token leftToken in this.Left.GetTokens())
            {
                yield return leftToken;
            }
        }

        yield return token;

        if (this.Right is not null)
        {
            await foreach (Token rightToken in this.Right.GetTokens())
            {
                yield return rightToken;
            }
        }
    }
}
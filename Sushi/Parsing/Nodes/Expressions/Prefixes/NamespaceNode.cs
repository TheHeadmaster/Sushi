using Sushi.Diagnostics;
using Sushi.Parsing.Nodes.Expressions.Core;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes.Expressions.Prefixes;

/// <summary>
/// Represents a namespace reference, such as in a namespace declaration or using statement.
/// </summary>
/// <param name="identifier">
/// The identifier of the namespace.
/// </param>
/// <param name="right">
/// The right-hand side of the namespace, usually another namespace.
/// </param>
public sealed class NamespaceNode(IdentifierNode? identifier, ExpressionNode? right) : ExpressionNode
{
    /// <summary>
    /// The right-hand side of the namespace, usually another namespace.
    /// </summary>
    public ExpressionNode? Right { get; set; } = right;

    /// <summary>
    /// The identifier of the namespace.
    /// </summary>
    public IdentifierNode? Name { get; set; } = identifier;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CompilerMessage> GetMessages()
    {
        if (this.Right is not null)
        {
            await foreach (CompilerMessage message in this.Right.GetMessages())
            {
                yield return message;
            }
        }

        if (this.Name is not null)
        {
            await foreach (CompilerMessage message in this.Name.GetMessages())
            {
                yield return message;
            }
        }
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<Token> GetTokens()
    {
        if (this.Right is not null)
        {
            await foreach (Token token in this.Right.GetTokens())
            {
                yield return token;
            }
        }

        if (this.Name is not null)
        {
            await foreach (Token token in this.Name.GetTokens())
            {
                yield return token;
            }
        }
    }
}
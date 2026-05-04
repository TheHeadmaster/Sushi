using System.Diagnostics.CodeAnalysis;
using Sushi.Diagnostics;
using Sushi.Diagnostics.Errors;
using Sushi.Parsing.Nodes.Expressions.Core;
using Sushi.Parsing.Nodes.Expressions.Prefixes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes.TopLevelStatements;

/// <summary>
/// Represents a namespace declaration.
/// </summary>
/// <param name="namespaceToken">
/// The starting token of the namespace declaration.
/// </param>
/// <param name="expression">
/// The expression body of the namespace.
/// </param>
/// <param name="terminatorToken">
/// The terminator token that is expected to be at the end of the node.
/// Not all statements require a terminator, such as sub-statements.
/// </param>
/// <param name="filePath">
/// The path of the file that this node exists in.
/// </param>
public sealed class NamespaceDeclarationNode([NotNull] Token namespaceToken, ExpressionNode? expression, Token? terminatorToken, string filePath) : StatementNode(terminatorToken)
{
    /// <summary>
    /// The expression body of the namespace.
    /// </summary>
    public ExpressionNode? Expression { get; set; } = expression;

    /// <summary>
    /// Builds a namespace chain from this using node's namespace expression.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/> that returns a <see cref="List{T}"/> of <see cref="string"/> objects.
    /// </returns>
    public async Task<List<string>> BuildNamespace()
    {
        ExpressionNode? currentNode = this.Expression;

        List<string> namespaceChain = [];

        while (true)
        {
            if (currentNode is null)
            {
                break;
            }

            currentNode = await ConsumeNamespaceOrIdentifier(currentNode, namespaceChain);
        }

        return namespaceChain;
    }

    /// <summary>
    /// Consumes a namespace or identifier node and adds the namespace part to the chain.
    /// </summary>
    /// <param name="node">
    /// The node to consume.
    /// </param>
    /// <param name="namespaceChain">
    /// The namespace chain to modify.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/> that returns the next node in the chain.
    /// </returns>
    private static Task<ExpressionNode?> ConsumeNamespaceOrIdentifier([NotNull] ExpressionNode node, [NotNull] List<string> namespaceChain)
    {
        ExpressionNode? nextNode = null;

        if (node is NamespaceNode namespaceNode && namespaceNode.Name is not null)
        {
            namespaceChain.Add(namespaceNode.Name.Name);
            nextNode = namespaceNode.Right;
        }
        else if (node is IdentifierNode identifier)
        {
            namespaceChain.Add(identifier.Name);
            nextNode = null;
        }

        return Task.FromResult(nextNode);
    }
    /// <inheritdoc />
    public override async IAsyncEnumerable<CompilerMessage> GetMessages()
    {
        if (this.Expression is not null)
        {
            await foreach (CompilerMessage message in this.Expression.GetMessages())
            {
                yield return message;
            }
        }
        else
        {
            yield return new InvalidNamespaceDeclarationError(namespaceToken, filePath);
        }
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<Token> GetTokens()
    {
        yield return namespaceToken;

        if (this.Expression is not null)
        {
            await foreach (Token token in this.Expression.GetTokens())
            {
                yield return token;
            }
        }

        if (this.TerminatorToken is not null)
        {
            yield return this.TerminatorToken;
        }
    }
}
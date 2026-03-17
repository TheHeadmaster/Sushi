using System.Diagnostics.CodeAnalysis;
using Sushi.Compilation;
using Sushi.Tokenization;
using Sushi.Verification;

namespace Sushi.Parsing.Nodes;

public class UsingNode([NotNull] Token usingToken, ExpressionNode? identifier) : StatementNode
{
    public ExpressionNode Identifier { get; set; } = identifier;

    public List<string> ResolvedNamespaces { get; set; } = [];

    public override Token GetStartToken() => usingToken;

    public override async Task Verify(VerificationContext context)
    {
        await this.Identifier.Verify(context);
    }

    /// <summary>
    /// Builds a namespace chain from this using node's namespace expression.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/> that returns a <see cref="List{T}"/> of <see cref="string"/> objects.
    /// </returns>
    public async Task<List<string>> BuildNamespace()
    {
        ExpressionNode? currentNode = this.Identifier;

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
    public override async Task Compile([NotNull] Compiler compiler)
    {
        foreach (string namespaceString in this.ResolvedNamespaces)
        {
            foreach (string path in await compiler.Reference.GetNamespaceFilePaths(namespaceString))
            {
                await compiler.WriteLine($"#include \"{Path.ChangeExtension(path, ".h")}\"");
            }
        }
    }

    /// <inheritdoc />
    public override async Task CompileHeader([NotNull] Compiler compiler)
    {
        foreach (string namespaceString in this.ResolvedNamespaces)
        {
            foreach (string path in await compiler.Reference.GetNamespaceFilePaths(namespaceString))
            {
                await compiler.WriteHeaderLine($"#include \"{Path.ChangeExtension(path, ".h")}\"");
            }
        }
    }
}

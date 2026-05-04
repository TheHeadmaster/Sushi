using System.Diagnostics.CodeAnalysis;
using Sushi.Diagnostics;
using Sushi.Diagnostics.Errors;
using Sushi.Parsing.Nodes.Expressions.Core;
using Sushi.Parsing.Nodes.Expressions.Infixes;
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
public sealed class NamespaceDeclarationNode([NotNull] Token namespaceToken, ExpressionNode? expression, Token? terminatorToken, [NotNull] string filePath) : StatementNode(terminatorToken)
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
        List<string> namespaceChain = [];

        if (this.Expression is null)
        {
            return namespaceChain;
        }

        await ConsumeNamespaceChain(this.Expression, namespaceChain);

        return namespaceChain;
    }

    /// <summary>
    /// Consumes a binary expression node or identifier node and adds the namespace part to the chain.
    /// </summary>
    /// <param name="node">
    /// The node to consume.
    /// </param>
    /// <param name="namespaceChain">
    /// The namespace chain to modify.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    private static async Task ConsumeNamespaceChain([NotNull] ExpressionNode node, [NotNull] List<string> namespaceChain)
    {
        if (node is BinaryExpressionNode binaryExpression)
        {
            if (binaryExpression.Left is null || binaryExpression.Right is null)
            {
                return;
            }

            await ConsumeNamespaceChain(binaryExpression.Left, namespaceChain);
            await ConsumeNamespaceChain(binaryExpression.Right, namespaceChain);
        }
        else if (node is IdentifierNode identifier)
        {
            namespaceChain.Add(identifier.Name);
        }

        return;
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<CompilerMessage> GetMessages()
    {
        if (this.TerminatorToken is null)
        {
            yield return new UnterminatedStatementError(namespaceToken, filePath);
        }

        if (this.Expression is not null)
        {
            await foreach (CompilerMessage message in this.Expression.GetMessages())
            {
                yield return message;
            }

            if ((await this.AssertValidNamespaceDeclaration(this.Expression)) is { } invalidMessage)
            {
                yield return invalidMessage;
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

    /// <summary>
    /// Asserts that the expression is a valid namespace expression (i.e. isn't an addition or something).
    /// </summary>
    /// <returns>
    /// <param name="expression">
    /// The expression to check recursively.
    /// </param>
    /// An awaitable <see cref="Task"/> that returns a <see cref="CompilerMessage"/> if the expression isn't valid,
    /// or null if everything is correct.
    /// </returns>
    private async Task<CompilerMessage?> AssertValidNamespaceDeclaration(ExpressionNode? expression)
    {
        if (expression is null)
        {
            return null;
        }

        if (expression is BinaryExpressionNode binaryExpression)
        {
            if (binaryExpression.Operator is not OperatorType.Navigation)
            {
                return new InvalidNamespaceDeclarationError(await expression.GetTokens().FirstAsync(), filePath);
            }

            CompilerMessage? leftMessage = await this.AssertValidNamespaceDeclaration(binaryExpression.Left);

            if (leftMessage is not null)
            {
                return leftMessage;
            }

            CompilerMessage? rightMessage = await this.AssertValidNamespaceDeclaration(binaryExpression.Right);

            if (rightMessage is not null)
            {
                return rightMessage;
            }

            return null;
        }

        if (expression is IdentifierNode)
        {
            return null;
        }

        return new InvalidNamespaceDeclarationError(await expression.GetTokens().FirstAsync(), filePath);

    }
}
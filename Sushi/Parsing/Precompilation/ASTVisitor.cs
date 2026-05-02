using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Nodes;
using Sushi.Parsing.Nodes.Expressions.Core;
using Sushi.Parsing.Nodes.Expressions.Infixes;
using Sushi.Parsing.Nodes.Expressions.Prefixes;
using Sushi.Parsing.Nodes.TopLevelStatements;

namespace Sushi.Parsing.Precompilation;

/// <summary>
/// Represents an object that visits each node in an <see cref="AbstractSyntaxTree"/> and performs some operation on it.
/// </summary>
public abstract class ASTVisitor
{
    /// <summary>
    /// Visits a <see cref="SyntaxNode"/>.
    /// </summary>
    /// <param name="node">
    /// The node to visit.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public virtual async Task Visit(SyntaxNode node)
    {
        await (node switch
        {
            AbstractSyntaxTree tree => this.VisitTree(tree),
            BinaryExpressionNode binary => this.VisitBinary(binary),
            ExpressionStatementNode expression => this.VisitExpressionStatement(expression),
            FileNode file => this.VisitFile(file),
            IdentifierNode identifier => this.VisitIdentifier(identifier),
            NamespaceNode namespaceNode => this.VisitNamespace(namespaceNode),
            NamespaceDeclarationNode namespaceDeclaration => this.VisitNamespaceDeclaration(namespaceDeclaration),
            UsingNode usingNode => this.VisitUsing(usingNode),
            _ => Task.CompletedTask
        });
    }

    /// <summary>
    /// Visits a <see cref="BinaryExpressionNode"/>.
    /// </summary>
    /// <param name="binary">
    /// The node to visit.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    protected virtual Task VisitBinary([NotNull] BinaryExpressionNode binary) => Task.CompletedTask;

    /// <summary>
    /// Visits an <see cref="ExpressionStatementNode"/>.
    /// </summary>
    /// <param name="expression">
    /// The node to visit.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    protected virtual Task VisitExpressionStatement([NotNull] ExpressionStatementNode expression) => Task.CompletedTask;

    /// <summary>
    /// Visits a <see cref="FileNode"/>.
    /// </summary>
    /// <param name="file">
    /// The node to visit.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    protected virtual Task VisitFile([NotNull] FileNode file) => Task.CompletedTask;

    /// <summary>
    /// Visits an <see cref="IdentifierNode"/>.
    /// </summary>
    /// <param name="identifier">
    /// The node to visit.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    protected virtual Task VisitIdentifier([NotNull] IdentifierNode identifier) => Task.CompletedTask;

    /// <summary>
    /// Visits a <see cref="NamespaceNode"/>.
    /// </summary>
    /// <param name="namespaceNode">
    /// The node to visit.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    protected virtual Task VisitNamespace([NotNull] NamespaceNode namespaceNode) => Task.CompletedTask;

    /// <summary>
    /// Visits a <see cref="NamespaceDeclarationNode"/>.
    /// </summary>
    /// <param name="namespaceDeclaration">
    /// The node to visit.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    protected virtual Task VisitNamespaceDeclaration([NotNull] NamespaceDeclarationNode namespaceDeclaration) => Task.CompletedTask;

    /// <summary>
    /// Visits an <see cref="AbstractSyntaxTree"/>.
    /// </summary>
    /// <param name="tree">
    /// The node to visit.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    protected virtual Task VisitTree([NotNull] AbstractSyntaxTree tree) => Task.CompletedTask;

    /// <summary>
    /// Visits a <see cref="UsingNode"/>.
    /// </summary>
    /// <param name="usingNode">
    /// The node to visit.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    protected virtual Task VisitUsing([NotNull] UsingNode usingNode) => Task.CompletedTask;
}

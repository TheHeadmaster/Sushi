using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Nodes;

namespace Sushi.Precompilation;

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
            AssignmentNode assignment => this.VisitAssignment(assignment),
            BinaryExpressionNode binary => this.VisitBinary(binary),
            BlockNode block => this.VisitBlock(block),
            ClassNode classNode => this.VisitClass(classNode),
            ConstantNode constant => this.VisitConstant(constant),
            CreateNode create => this.VisitCreate(create),
            DestroyNode destroy => this.VisitDestroy(destroy),
            DestroyerDeclarationNode destroyer => this.VisitDestroyerDeclaration(destroyer),
            DoWhileNode doWhile => this.VisitDoWhile(doWhile),
            ExpressionStatementNode expression => this.VisitExpressionStatement(expression),
            FileNode file => this.VisitFile(file),
            IdentifierNode identifier => this.VisitIdentifier(identifier),
            IfNode ifNode => this.VisitIf(ifNode),
            MemberDeclarationNode member => this.VisitMemberDeclaration(member),
            MethodCallNode method => this.VisitMethodCall(method),
            MethodDeclarationNode method => this.VisitMethodDeclaration(method),
            NamespaceDeclarationNode namespaceDeclaration => this.VisitNamespaceDeclaration(namespaceDeclaration),
            NamespaceNode namespaceNode => this.VisitNamespace(namespaceNode),
            ParameterListNode parameterList => this.VisitParameterList(parameterList),
            ParameterNode parameter => this.VisitParameter(parameter),
            TypeNode type => this.VisitType(type),
            UnaryExpressionNode unary => this.VisitUnary(unary),
            UsingNode usingNode => this.VisitUsing(usingNode),
            VariableDeclarationNode variable => this.VisitVariableDeclaration(variable),
            WhileNode whileNode => this.VisitWhile(whileNode),
            _ => Task.CompletedTask
        });
    }

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
    /// Visits an <see cref="AssignmentNode"/>.
    /// </summary>
    /// <param name="assignment">
    /// The node to visit.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    protected virtual Task VisitAssignment([NotNull] AssignmentNode assignment) => Task.CompletedTask;

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
    /// Visits a <see cref="BlockNode"/>.
    /// </summary>
    /// <param name="block">
    /// The node to visit.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    protected virtual Task VisitBlock([NotNull] BlockNode block) => Task.CompletedTask;

    /// <summary>
    /// Visits a <see cref="ClassNode"/>.
    /// </summary>
    /// <param name="classNode">
    /// The node to visit.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    protected virtual Task VisitClass([NotNull] ClassNode classNode) => Task.CompletedTask;

    /// <summary>
    /// Visits a <see cref="ConstantNode"/>.
    /// </summary>
    /// <param name="constant">
    /// The node to visit.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    protected virtual Task VisitConstant([NotNull] ConstantNode constant) => Task.CompletedTask;

    /// <summary>
    /// Visits a <see cref="DestroyNode"/>.
    /// </summary>
    /// <param name="destroy">
    /// The node to visit.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    protected virtual Task VisitDestroy([NotNull] DestroyNode destroy) => Task.CompletedTask;

    /// <summary>
    /// Visits a <see cref="DoWhileNode"/>.
    /// </summary>
    /// <param name="doWhile">
    /// The node to visit.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    protected virtual Task VisitDoWhile([NotNull] DoWhileNode doWhile) => Task.CompletedTask;

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
    /// Visits a <see cref="IfNode"/>.
    /// </summary>
    /// <param name="ifNode">
    /// The node to visit.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    protected virtual Task VisitIf([NotNull] IfNode ifNode) => Task.CompletedTask;

    /// <summary>
    /// Visits a <see cref="MemberDeclarationNode"/>.
    /// </summary>
    /// <param name="member">
    /// The node to visit.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    protected virtual Task VisitMemberDeclaration([NotNull] MemberDeclarationNode member) => Task.CompletedTask;

    /// <summary>
    /// Visits a <see cref="MethodCallNode"/>.
    /// </summary>
    /// <param name="method">
    /// The node to visit.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    protected virtual Task VisitMethodCall([NotNull] MethodCallNode method) => Task.CompletedTask;

    /// <summary>
    /// Visits a <see cref="MethodDeclarationNode"/>.
    /// </summary>
    /// <param name="method">
    /// The node to visit.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    protected virtual Task VisitMethodDeclaration([NotNull] MethodDeclarationNode method) => Task.CompletedTask;

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
    /// Visits a <see cref="TypeNode"/>.
    /// </summary>
    /// <param name="type">
    /// The node to visit.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    protected virtual Task VisitType([NotNull] TypeNode type) => Task.CompletedTask;

    /// <summary>
    /// Visits a <see cref="UnaryExpressionNode"/>.
    /// </summary>
    /// <param name="unary">
    /// The node to visit.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    protected virtual Task VisitUnary([NotNull] UnaryExpressionNode unary) => Task.CompletedTask;

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

    /// <summary>
    /// Visits a <see cref="WhileNode"/>.
    /// </summary>
    /// <param name="whileNode">
    /// The node to visit.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    protected virtual Task VisitWhile([NotNull] WhileNode whileNode) => Task.CompletedTask;

    /// <summary>
    /// Visits a <see cref="ParameterListNode"/>.
    /// </summary>
    /// <param name="parameterList">
    /// The node to visit.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    protected virtual Task VisitParameterList([NotNull] ParameterListNode parameterList) => Task.CompletedTask;

    /// <summary>
    /// Visits a <see cref="ParameterNode"/>.
    /// </summary>
    /// <param name="parameter">
    /// The node to visit.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    protected virtual Task VisitParameter([NotNull] ParameterNode parameter) => Task.CompletedTask;

    /// <summary>
    /// Visits a <see cref="DestroyerDeclarationNode"/>.
    /// </summary>
    /// <param name="destroyer">
    /// The node to visit.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    protected virtual Task VisitDestroyerDeclaration([NotNull] DestroyerDeclarationNode destroyer) => Task.CompletedTask;

    /// <summary>
    /// Visits a <see cref="VariableDeclarationNode"/>.
    /// </summary>
    /// <param name="variable">
    /// The node to visit.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    protected virtual Task VisitVariableDeclaration([NotNull] VariableDeclarationNode variable) => Task.CompletedTask;

    /// <summary>
    /// Visits a <see cref="CreateNode"/>.
    /// </summary>
    /// <param name="create">
    /// The node to visit.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    protected virtual Task VisitCreate([NotNull] CreateNode create) => Task.CompletedTask;
}

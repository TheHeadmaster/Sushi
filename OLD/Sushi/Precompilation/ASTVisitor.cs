using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Nodes;

namespace Sushi.Precompilation;

/// <summary>
/// Represents an object that visits each node in an <see cref="AbstractSyntaxTree"/> and performs some operation on it.
/// </summary>
public abstract class ASTVisitor
{

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

    /// <summary>
    /// Visits a <see cref="CreatorDeclarationNode"/>.
    /// </summary>
    /// <param name="creator">
    /// The node to visit.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    protected virtual Task VisitCreatorDeclaration([NotNull] CreatorDeclarationNode creator) => Task.CompletedTask;
}

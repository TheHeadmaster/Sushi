using System.Diagnostics.CodeAnalysis;
using Sushi.Diagnostics;
using Sushi.Diagnostics.Errors;
using Sushi.Parsing.Nodes;

namespace Sushi.Precompilation;

/// <summary>
/// Handles the enforcement of the linear type system.
/// </summary>
public sealed class LinearTypeEnforcer
{
    /// <summary>
    /// Opens a new scope.
    /// </summary>
    /// <returns></returns>
    public async Task OpenScope()
    {

    }

    /// <summary>
    /// Adds a type to the current scope.
    /// </summary>
    /// <returns></returns>
    public async Task AddTypeToScope()
    {

    }

    /// <summary>
    /// Marks a type as destroyed.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task MarkTypeDestroyed()
    {

    }

    /// <summary>
    /// Marks a type as returned.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task MarkTypeReturned()
    {

    }

    /// <summary>
    /// Closes the scope and verifies that no types went out of scope without being destroyed or changing owners.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task CloseScopeAndVerifyTypes()
    {

    }

    /// <summary>
    /// Verifies that a destroyer is valid and doesn't break any rules of the linear type system.
    /// </summary>
    /// <param name="classNode">
    /// The class that this destroyer belongs to.
    /// </param>
    /// <param name="messages">
    /// The compiler messages list.
    /// </param>
    /// <param name="destroyer">
    /// The destroyer node.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task VerifyDestroyer([NotNull] ClassNode classNode, [NotNull] List<CompilerMessage> messages, [NotNull] DestroyerDeclarationNode destroyer)
    {
        List<MemberDeclarationNode> members = [.. classNode.Members.OfType<MemberDeclarationNode>().Where(member => member.Type?.IsReferenceType() ?? false)];

        if (destroyer.Body is not null)
        {
            foreach (StatementNode statement in destroyer.Body.Statements)
            {
                if (statement is DestroyNode destroy && destroy.Object is not null)
                {
                    MemberDeclarationNode member = members.First(x => x.Identifier!.Name == destroy.Object.Name);
                    members.Remove(member);
                }
            }

            if (members.Count > 0)
            {
                messages.Add(new UndestroyedMemberError(destroyer.GetStartToken(), members));
            }
        }
    }
}

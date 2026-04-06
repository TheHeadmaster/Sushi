using System.Diagnostics.CodeAnalysis;
using Sushi.Diagnostics;
using Sushi.Diagnostics.Errors;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Precompilation;

/// <summary>
/// Handles the enforcement of the linear type system.
/// </summary>
public sealed class LinearTypeEnforcer
{
    private Dictionary<string, (int count, Token startToken)> types = [];

    /// <summary>
    /// Adds a type to the current scope.
    /// </summary>
    /// <param name="name">
    /// The name of the variable.
    /// </param>
    /// <param name="startToken">
    /// The start token.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task AddTypeToScope(string name, Token startToken) => this.types[name] = (0, startToken);

    /// <summary>
    /// Marks a type as used.
    /// </summary>
    /// <param name="name">
    /// The name of the variable.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task MarkTypeUsed(string name) => this.types[name] = (this.types[name].count + 1, this.types[name].startToken);

    /// <summary>
    /// Closes the scope and verifies that no types went out of scope without being destroyed or changing owners.
    /// </summary>
    /// <param name="messages">
    /// The compiler messages list.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task CloseScopeAndVerifyTypes([NotNull] List<CompilerMessage> messages)
    {
        foreach ((string type, (int count, Token startToken)) in this.types)
        {
            if (count == 1)
            {
                continue;
            }
            else if (count == 0)
            {

                messages.Add(new UnusedTypeError(startToken, type, this.filePath));
            }
            else
            {
                messages.Add(new OverusedTypeError(startToken, type, this.filePath));
            }
        }
    }

    private string filePath = string.Empty;

    public async Task ChangeFile(string filePath) => this.filePath = filePath;

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
                messages.Add(new UndestroyedMemberError(destroyer.GetStartToken(), members, this.filePath));
            }
        }
    }
}

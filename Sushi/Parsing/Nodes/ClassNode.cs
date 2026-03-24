using System.Diagnostics.CodeAnalysis;
using Sushi.Compilation;
using Sushi.Parsing.Core;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents a class declaration.
/// </summary>
/// <param name="token">
/// The <see cref="Token"/> that marks the start of the entire class declaration.
/// </param>
/// <param name="typeName">
/// The name as a <see cref="TypeNode"/>.
/// </param>
/// <param name="members">
/// The class members.
/// </param>
public sealed class ClassNode([NotNull] Token token, TypeNode? typeName, [NotNull] List<StatementNode> members) : StatementNode, ICanBeStatic, IAccessModifiable
{
    /// <inheritdoc />
    public bool IsStatic { get; set; }

    /// <summary>
    /// The name as a <see cref="TypeNode"/>.
    /// </summary>
    public TypeNode? TypeName { get; set; } = typeName;

    /// <summary>
    /// The class members.
    /// </summary>
    public List<StatementNode> Members { get; set; } = members;

    /// <inheritdoc />
    public AccessModifier AccessModifier { get; set; }

    /// <inheritdoc />
    public override Token GetStartToken() => token;

    /// <inheritdoc />
    public override async Task Compile([NotNull] CompilerVisitor compiler)
    {
        foreach (StatementNode node in this.Members)
        {
            await node.Compile(compiler);
        }
    }
}

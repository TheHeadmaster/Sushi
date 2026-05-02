using System.Diagnostics.CodeAnalysis;
using Sushi.Diagnostics;
using Sushi.Parsing.Nodes.Expressions.Core;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes.TopLevelStatements;

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
    public override List<CompilerMessage> AggregateMessages() => [.. this.Messages, .. this.Members.SelectMany(x => x.AggregateMessages()), .. this.TypeName.AggregateMessages()];

    /// <inheritdoc />
    public bool AllowsModifier(AccessModifier modifier) => modifier switch
    {
        AccessModifier.Public or AccessModifier.Internal => true,
        _ => false
    };

    /// <inheritdoc />
    public override Token? GetEndToken() => this.Members.LastOrDefault()?.GetEndToken();

    /// <inheritdoc />
    public override Token GetStartToken() => token;
}


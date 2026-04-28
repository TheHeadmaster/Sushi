using System.Diagnostics.CodeAnalysis;
using Sushi.Compilation;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents a member declaration, such as a field.
/// </summary>
/// <param name="token">
/// The starting <see cref="Token"/> of the member declaration.
/// </param>
/// <param name="type">
/// The type of the member.
/// </param>
/// <param name="identifier">
/// The identifier of the membe.r
/// </param>
public sealed class MemberDeclarationNode([NotNull] Token token, TypeNode? type, IdentifierNode? identifier) : StatementNode
{
    /// <summary>
    /// The type of the member.
    /// </summary>
    public TypeNode? Type { get; set; } = type;

    /// <summary>
    /// The identifier of the member.
    /// </summary>
    public IdentifierNode? Identifier { get; set; } = identifier;

    /// <inheritdoc />
    public override Token? GetStartToken() => token;
}

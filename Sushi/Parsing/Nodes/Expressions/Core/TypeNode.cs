using System.Diagnostics.CodeAnalysis;
using Sushi.Diagnostics;
using Sushi.Parsing.Scope;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes.Expressions.Core;

/// <summary>
/// Represents a type that is defined somewhere else in the code.
/// </summary>
/// <param name="token">
/// The <see cref="Token"/> used to mark the start of the node.
/// </param>
public sealed class TypeNode([NotNull] Token token) : StatementNode
{
    /// <summary>
    /// The name of the type.
    /// </summary>
    public string Name { get; set; } = token.Type is TokenType.Identifier ? token.Value : Constants.TryGetPrimitiveType(token);

    /// <summary>
    /// The resolved type of the node.
    /// </summary>
    public SushiType? ResolvedType { get; set; }

    /// <summary>
    /// Returns whether the type is a reference type or a copy type.
    /// </summary>
    /// <returns>
    /// True if the type is a reference type. False otherwise.
    /// </returns>
    public bool IsReferenceType()
    {
        if (this.ResolvedType is null)
        {
            return false;
        }

        return this.ResolvedType.IsReferenceType();
    }

    /// <inheritdoc />
    public override Token? GetStartToken() => token;

    /// <inheritdoc />
    public override List<CompilerMessage> AggregateMessages() => [.. this.Messages];

    /// <inheritdoc />
    public override Token? GetEndToken() => token;
}
using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents a single parameter.
/// </summary>
/// <param name="type">
/// The type of the parameter.
/// </param>
/// <param name="identifier">
/// The identifier of the parameter.
/// </param>
public sealed class ParameterNode(TypeNode? type, IdentifierNode? identifier) : StatementNode
{
    /// <summary>
    /// The type of the paramter.
    /// </summary>
    public TypeNode? Type { get; set; } = type;

    /// <summary>
    /// The identifier of the parameter.
    /// </summary>
    public IdentifierNode? Name { get; set; } = identifier;

    /// <inheritdoc />
    public override Token? GetStartToken() => this.Type?.GetStartToken();
}

using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents a creator declaration.
/// </summary>
/// <param name="token">
/// The token that is the start of the declaration.
/// </param>
/// <param name="name">
/// The name of the creator.
/// </param>
/// <param name="parameterList">
/// The list of parameters.
/// </param>
/// <param name="body">
/// The body of the creator.
/// </param>
public sealed class CreatorDeclarationNode([NotNull] Token token, IdentifierNode? name, ParameterListNode? parameterList, BlockNode? body) : StatementNode
{
    /// <summary>
    /// The name of the creator.
    /// </summary>
    public IdentifierNode? Name { get; set; } = name;

    /// <summary>
    /// The list of parameters.
    /// </summary>
    public ParameterListNode? ParameterList { get; set; } = parameterList;

    /// <summary>
    /// The body of the creator.
    /// </summary>
    public BlockNode? Body { get; set; } = body;

    /// <inheritdoc />
    public override Token GetStartToken() => token;
}

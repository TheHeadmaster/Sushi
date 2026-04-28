using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents a creator declaration.
/// </summary>
/// <param name="token">
/// The token that is the start of the declaration.
/// </param>
/// <param name="parameterList">
/// The list of parameters.
/// </param>
/// <param name="body">
/// The body of the creator.
/// </param>
public sealed class CreatorDeclarationNode([NotNull] Token token, ParameterListNode? parameterList, BlockNode? body) : StatementNode
{
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

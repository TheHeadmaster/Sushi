using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents a list of parameters.
/// </summary>
/// <param name="token">
/// The token that starts the parameter list.
/// </param>
/// <param name="parameters">
/// The list of parameters.
/// </param>
public sealed class ParameterListNode([NotNull] Token token, [NotNull] List<ParameterNode> parameters) : StatementNode
{
    /// <summary>
    /// The list of parameters.
    /// </summary>
    public List<ParameterNode> Parameters { get; set; } = parameters;

    /// <inheritdoc />
    public override Token? GetStartToken() => token;
}

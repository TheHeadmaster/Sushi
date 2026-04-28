using System.Diagnostics.CodeAnalysis;
using Sushi.Compilation;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents a block of statements that is associated with another declaration, such as a method declaration.
/// </summary>
/// <param name="token">
/// The token that starts the block.
/// </param>
/// <param name="statements">
/// The statements in the block.
/// </param>
public sealed class BlockNode([NotNull] Token token, List<StatementNode> statements) : StatementNode
{
    /// <summary>
    /// The statements in the block.
    /// </summary>
    public List<StatementNode> Statements { get; set; } = statements;

    /// <inheritdoc />
    public override Token? GetStartToken() => token;
}

using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents a node that is considered a standalone statement.
/// Some statements can be comprised of other statements.
/// </summary>
/// <param name="terminatorToken">
/// The terminator token that is expected to be at the end of the node.
/// Not all statements require a terminator, such as sub-statements.
/// </param>
public abstract class StatementNode(Token? terminatorToken) : SyntaxNode
{
    /// <summary>
    /// The terminator token that is expected to be at the end of the node.
    /// Not all statements require a terminator, such as sub-statements.
    /// </summary>
    public Token? TerminatorToken { get; set; } = terminatorToken;
}
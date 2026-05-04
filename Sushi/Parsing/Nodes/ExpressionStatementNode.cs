using Sushi.Diagnostics;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents an expression as a statement.
/// </summary>
/// <param name="expression">
/// The expression contained in the statement.
/// </param>
/// <param name="terminatorToken">
/// The terminator token that is expected to be at the end of the node.
/// Not all statements require a terminator, such as sub-statements.
/// </param>
public sealed class ExpressionStatementNode(ExpressionNode? expression, Token? terminatorToken) : StatementNode(terminatorToken)
{
    /// <summary>
    /// The expression.
    /// </summary>
    public ExpressionNode? Expression { get; set; } = expression;

    /// <inheritdoc />
    public override async IAsyncEnumerable<CompilerMessage> GetMessages()
    {
        if (this.Expression is not null)
        {
            await foreach (CompilerMessage message in this.Expression.GetMessages())
            {
                yield return message;
            }
        }
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<Token> GetTokens()
    {
        if (this.Expression is not null)
        {
            await foreach (Token token in this.Expression.GetTokens())
            {
                yield return token;
            }
        }

        if (this.TerminatorToken is not null)
        {
            yield return this.TerminatorToken;
        }
    }
}
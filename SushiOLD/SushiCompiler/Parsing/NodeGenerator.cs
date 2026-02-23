using SushiCompiler.Parsing.Nodes;
using SushiCompiler.Tokenization;
using System.Reflection;

namespace SushiCompiler.Parsing;

internal abstract class NodeGenerator
{
    protected static readonly List<string> valueTypes =
    [
        "int",
        "float"
    ];

    protected static async Task<Token?> Optional(Queue<Token> tokenQueue, params TokenType[] tokenTypes) => await TokenIsAnyOf(tokenQueue.Peek(), tokenTypes) ? tokenQueue.Dequeue() : null;

    protected static async Task<Token> Required(Queue<Token> tokenQueue, params TokenType[] tokenTypes) => await TokenIsAnyOf(tokenQueue.Peek(), tokenTypes) ? tokenQueue.Dequeue() : throw new InvalidOperationException("Unexpected token");

    protected static Task<bool> TokenIsAnyOf(Token token, params TokenType[] tokenTypes)
    {
        foreach (TokenType tokenType in tokenTypes)
        {
            if (token.Type == tokenType)
            {
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }

    protected static Task CheckEndOfFile(Queue<Token> tokenQueue)
    {
        return tokenQueue.Count == 0 ? throw new InvalidOperationException("Unexpected End of File without a token") : Task.CompletedTask;
    }

    protected static async Task DiscardIfExists(Queue<Token> tokenQueue, params TokenType[] tokenTypes)
    {
        if (await TokenIsAnyOf(tokenQueue.Peek(), tokenTypes))
        {
            tokenQueue.Dequeue();
        }
    }
}

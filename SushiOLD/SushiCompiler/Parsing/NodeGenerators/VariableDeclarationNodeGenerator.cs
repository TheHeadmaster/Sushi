using Serilog;
using SushiCompiler.Parsing.Nodes;
using SushiCompiler.Tokenization;

namespace SushiCompiler.Parsing.NodeGenerators;

internal class VariableDeclarationNodeGenerator : NodeGenerator
{
    internal override async Task<NodeGeneratorResult> TryGenerate(FileNode fileNode, Queue<Token> tokenQueue, List<NodeGenerator> nodeGenerators)
    {
        if (tokenQueue.TryPeek(out Token? token) && token.Type is TokenType.Keyword && valueTypes.Contains(token.Value))
        {
            VariableDeclarationNode variableDeclarationNode = new(tokenQueue.Dequeue());

            return await ParseRightHandSide(fileNode, variableDeclarationNode, tokenQueue, nodeGenerators);
        }
        else
        {
            return new NodeGeneratorResult()
            {
                IsHandled = false,
                Node = null
            };
        }
    }

    private static async Task<NodeGeneratorResult> ParseRightHandSide(FileNode fileNode, VariableDeclarationNode node, Queue<Token> tokenQueue, List<NodeGenerator> nodeGenerators)
    {
        while (true)
        {
            await CheckEndOfFile(tokenQueue);


            if (tokenQueue.Peek().Type is TokenType.WhiteSpace)
            {
                tokenQueue.Dequeue();
                continue;
            }

            if (tokenQueue.Peek().Type is TokenType.Identifier)
            {
                if (!string.IsNullOrWhiteSpace(node.VariableName))
                {
                    throw new InvalidOperationException("Unexpected identifier after variable identifier");
                }

                node.VariableName = tokenQueue.Peek().Value;
                tokenQueue.Dequeue();
                continue;
            }

            if (tokenQueue.Peek().Type is TokenType.Terminator)
            {
                if (string.IsNullOrWhiteSpace(node.VariableName))
                {
                    fileNode.ErrorCount++;
                    Log.Error("Class terminated without identifier");
                }

                tokenQueue.Dequeue();
                return new NodeGeneratorResult()
                {
                    IsHandled = true,
                    Node = node
                };
            }

            throw new InvalidOperationException($"Unexpected Token {tokenQueue.Peek().Type} {tokenQueue.Peek().Value}");
        }
    }
}
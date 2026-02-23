using Serilog;
using SushiCompiler.Parsing.Nodes;
using SushiCompiler.Tokenization;

namespace SushiCompiler.Parsing.NodeGenerators;

internal class ClassNodeGenerator : NodeGenerator
{
    internal override async Task<NodeGeneratorResult> TryGenerate(FileNode fileNode, Queue<Token> tokenQueue, List<NodeGenerator> nodeGenerators)
    {
        if (tokenQueue.TryPeek(out Token? token) && token.Type is TokenType.Keyword && token.Value == "class")
        {
            ClassNode classNode = new();

            tokenQueue.Dequeue();

            return await ParseRightHandSide(fileNode, classNode, tokenQueue, nodeGenerators);
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

    private static async Task<NodeGeneratorResult> ParseRightHandSide(FileNode fileNode, ClassNode node, Queue<Token> tokenQueue, List<NodeGenerator> nodeGenerators)
    {
        while (true)
        {
            await CheckEndOfFile(tokenQueue);

            await DiscardIfExists(tokenQueue, TokenType.NewLine, TokenType.WhiteSpace);

            if (tokenQueue.Peek().Type is TokenType.Identifier)
            {
                if (!string.IsNullOrWhiteSpace(node.ClassName))
                {
                    throw new InvalidOperationException("Unexpected identifier after class identifier");
                }

                node.ClassName = tokenQueue.Peek().Value;
                tokenQueue.Dequeue();
                continue;
            }

            if (tokenQueue.Peek().Type is TokenType.Terminator)
            {
                if (string.IsNullOrWhiteSpace(node.ClassName))
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
        }
    }
}
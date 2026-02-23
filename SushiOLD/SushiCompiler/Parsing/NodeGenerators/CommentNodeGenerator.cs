using SushiCompiler.Parsing.Nodes;
using SushiCompiler.Tokenization;

namespace SushiCompiler.Parsing.NodeGenerators;

internal class CommentNodeGenerator : NodeGenerator
{
    internal override Task<NodeGeneratorResult> TryGenerate(FileNode fileNode, Queue<Token> tokenQueue, List<NodeGenerator> nodeGenerators)
    {
        if (tokenQueue.TryPeek(out Token? token) && token.Type is TokenType.LineComment or TokenType.BlockComment)
        {
            CommentNode commentNode = new(tokenQueue.Dequeue());
            return Task.FromResult(new NodeGeneratorResult()
            {
                IsHandled = true,
                Node = commentNode
            });
        }
        else
        {
            return Task.FromResult(new NodeGeneratorResult()
            {
                IsHandled = false,
                Node = null
            });
        }
    }
}

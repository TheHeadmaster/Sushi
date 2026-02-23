using Serilog;
using SushiCompiler.Parsing;
using SushiCompiler.Parsing.Nodes;
using SushiCompiler.Tokenization;

namespace SushiCompiler.Steps;

internal static class SushiParser
{
    internal static async Task ParseFiles(List<SourceFile> sourceFiles)
    {
        List<NodeGenerator> nodeGenerators = NodeGenerator.GetGenerators();

        Log.Information("Parsing files...");
        foreach (SourceFile sourceFile in sourceFiles)
        {
            AbstractSyntaxTree.FileNodes.Add(await ParseFile(sourceFile, nodeGenerators));
        }

        Log.Information("{FileCount} files were parsed with {ErrorCount} syntax errors.", sourceFiles.Count, AbstractSyntaxTree.FileNodes.Sum(x => x.ErrorCount));

        if (AbstractSyntaxTree.FileNodes.Any(x => x.ErrorCount > 0))
        {
            Environment.Exit((int)ExitCode.ParsingSyntaxError);
        }
    }

    private static async Task<FileNode> ParseFile(SourceFile sourceFile, List<NodeGenerator> nodeGenerators)
    {
        FileNode fileNode = new(sourceFile);

        Queue<Token> tokenQueue = new(sourceFile.Tokens);

        while (tokenQueue.Count > 0)
        {
            try
            {
                Token currentToken = tokenQueue.Peek();

                if (currentToken.Type is TokenType.EndOfFile)
                {
                    break;
                }

                if (currentToken.Type is TokenType.WhiteSpace or TokenType.NewLine)
                {
                    tokenQueue.Dequeue();
                    continue;
                }

                SyntaxNode? node = await TryCreateNode(fileNode, tokenQueue, nodeGenerators);

                if (node is null)
                {
                    fileNode.ErrorCount++;
                    Token errantToken = tokenQueue.Dequeue();
                    Log.Error("Unexpected {Token} token in {FileName}: {Value}", errantToken.Type.ToString(), fileNode.FileName, errantToken.Value);
                    continue;
                }

                fileNode.Children.Add(node);
            }
            catch (Exception exception)
            {
                fileNode.ErrorCount++;
                Log.Error(exception, "Unexpected token");

                await Synchronize(tokenQueue);
            }
        }

        return fileNode;
    }

    private static async Task<SyntaxNode?> TryCreateNode(FileNode fileNode, Queue<Token> tokenQueue, List<NodeGenerator> nodeGenerators)
    {
        foreach (NodeGenerator generator in nodeGenerators)
        {
            NodeGeneratorResult result = await generator.TryGenerate(fileNode, tokenQueue, nodeGenerators);

            if (result.IsHandled)
            {
                return result.Node;
            }
        }

        return null;
    }

    private static async Task Synchronize(Queue<Token> tokenQueue)
    {
        tokenQueue.Dequeue();
    }
}

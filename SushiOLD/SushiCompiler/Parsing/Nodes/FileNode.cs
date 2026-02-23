using SushiCompiler.Compiling.L2.ILGenerators;
using SushiCompiler.Tokenization;

namespace SushiCompiler.Parsing.Nodes;

internal class FileNode(SourceFile sourceFile) : SyntaxNode, ISyntaxCollectionNode
{
    internal int ErrorCount { get; set; }
    internal string FileName { get; set; } = sourceFile.FileName;

    internal string FilePath { get; set; } = sourceFile.FilePath;

    public List<SyntaxNode> Children { get; } = [];

    internal override Type ILGenerator { get; } = typeof(FileILGenerator);
}

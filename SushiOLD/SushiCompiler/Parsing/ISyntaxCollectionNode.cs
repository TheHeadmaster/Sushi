namespace SushiCompiler.Parsing;

internal interface ISyntaxCollectionNode
{
    public List<SyntaxNode> Children { get; }
}

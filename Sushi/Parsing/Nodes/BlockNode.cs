using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

public class BlockNode([NotNull] Token token, List<StatementNode> statements) : StatementNode
{
    public List<StatementNode> Body { get; set; } = statements;

    public override Token GetStartToken() => token;
}

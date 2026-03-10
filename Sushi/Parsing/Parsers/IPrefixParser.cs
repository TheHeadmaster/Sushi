using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

public interface IPrefixParser : IParser
{
    public Task<ExpressionNode> Parse([NotNull] Parser parser, [NotNull] Token token);
}

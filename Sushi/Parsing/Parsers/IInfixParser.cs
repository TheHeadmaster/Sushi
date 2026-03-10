using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

public interface IInfixParser : IParser
{
    public Task<ExpressionNode> Parse([NotNull] Parser parser, [NotNull] ExpressionNode left, [NotNull] Token token);
}

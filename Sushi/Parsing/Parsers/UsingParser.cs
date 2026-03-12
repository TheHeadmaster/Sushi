using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

public class UsingParser : IStatementParser
{
    public async Task<StatementNode> Parse([NotNull] Parser parser, [NotNull] Token token)
    {
        await parser.ExpectAndPop(TokenType.Using);

        ExpressionNode expression = await parser.ParseExpression(BindingPower.Primary);

        if (expression is not NamespaceNode namespaceNode)
        {
            throw new NotImplementedException();
        }

        UsingNode usingStatement = new(token, namespaceNode);

        await parser.ExpectAndPop(TokenType.Terminator);

        return usingStatement;
    }
}

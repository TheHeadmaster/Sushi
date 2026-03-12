using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

/*
public class CreateParser : IStatementParser
{
    public async Task<StatementNode> Parse([NotNull] Parser parser, [NotNull] Token token)
    {
        await parser.ExpectAndPop(TokenType.Create);

        Token? currentToken = await parser.PeekAndExpectNotEOF();

        IdentifierNode obj = new(currentToken);

        await parser.ExpectAndPop(TokenType.Identifier);

        currentToken = parser.Peek();

        ExpressionNode? expression = currentToken is not null
            && currentToken.Type is TokenType.Identifier
            ? await parser.ParseExpression(BindingPower.Primary)
            : null;

        return new CreateNode(token, obj, expression);
    }
}
*/

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

public class NamespaceDeclarationParser : IStatementParser
{
    public async Task<StatementNode> Parse([NotNull] Parser parser, [NotNull] Token token)
    {
        await parser.ExpectAndPop(TokenType.Namespace);

        ExpressionNode expression = await parser.ParseExpression(BindingPower.Primary);

        if (expression is not NamespaceNode namespaceNode)
        {
            throw new NotImplementedException();
        }

        NamespaceDeclarationNode namespaceStatement = new(token, namespaceNode);

        await parser.ExpectAndPop(TokenType.Terminator);

        return namespaceStatement;
    }
}

using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Nodes;
using Sushi.Parsing.Parsers.SubStatements;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

/// <summary>
/// Handles the parsing of class declarations.
/// </summary>
public class ClassParser : IParser
{
    /// <inheritdoc />
    public ParserType Type { get; } = ParserType.Statement;

    /// <inheritdoc />
    public List<TokenType> AllowedStartTokens { get; } = [TokenType.Class];

    /// <inheritdoc />
    public async Task<StatementNode?> ParseStatement([NotNull] Parser parser, [NotNull] Token token)
    {
        Token? currentToken = await parser.ExpectAndPop(TokenType.Class);

        currentToken = await parser.PeekAndExpectNotEOF();

        await parser.ExpectAndPop(TokenType.Identifier);

        IdentifierNode identifier = new(currentToken);

        currentToken = await parser.PeekAndExpectNotEOF();

        StatementNode? body = await Parser.GetParser<BlockParser>().ParseStatement(parser, currentToken);

        if (body is not BlockNode block)
        {
            return null;
        }

        ClassNode classNode = new(token, identifier, block);

        return classNode;
    }

    /// <inheritdoc />
    public BindingPower Power(TokenType type) => BindingPower.Primary;
}

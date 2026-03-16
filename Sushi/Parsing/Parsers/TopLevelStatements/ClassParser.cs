using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Nodes;
using Sushi.Parsing.Parsers.SubStatements;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers.TopLevelStatements;

/// <summary>
/// Handles the parsing of class declarations.
/// </summary>
public sealed class ClassParser : IParser
{
    /// <inheritdoc />
    public ParserType Type { get; } = ParserType.Statement;

    /// <inheritdoc />
    public List<TokenType> AllowedStartTokens { get; } = [TokenType.Class];

    /// <inheritdoc />
    public List<ParserRole> Roles { get; } = [ParserRole.TopLevelStatement];

    /// <inheritdoc />
    public async Task<StatementNode?> ParseStatement([NotNull] Parser parser, [NotNull] Token token)
    {
        await parser.ExpectAndPop(TokenType.Class);

        Token? identifierToken = await parser.ExpectAndPop(TokenType.Identifier);

        IdentifierNode? identifier = identifierToken is null ? null : new(identifierToken);

        StatementNode? body = await Parser.GetParser<BlockParser>().ParseStatement(parser, parser.Peek()!);

        if (body is not BlockNode block)
        {
            return new ClassNode(token, identifier, null);
        }

        ClassNode classNode = new(token, identifier, block);

        return classNode;
    }

    /// <inheritdoc />
    public BindingPower Power(TokenType type) => BindingPower.Primary;
}

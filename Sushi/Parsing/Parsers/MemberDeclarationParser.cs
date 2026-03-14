using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

/// <summary>
/// Handles member declarations, such as method declarations and fields/properties.
/// </summary>
public class MemberDeclarationParser : IParser
{
    /// <inheritdoc />
    public ParserType Type { get; } = ParserType.Statement;

    /// <inheritdoc />
    public List<TokenType> AllowedStartTokens { get; } = [TokenType.Identifier];

    /// <inheritdoc />
    public static async Task<FieldDeclarationNode> Parse([NotNull] Parser parser, [NotNull] Token token)
    {
        Token? currentToken = token;

        await parser.ExpectAndPop(TokenType.Identifier);

        IdentifierNode typeNode = new(currentToken);

        currentToken = await parser.PeekAndExpectNotEOF();

        await parser.ExpectAndPop(TokenType.Identifier);

        IdentifierNode identifierNode = new(currentToken);

        await parser.ExpectAndPop(TokenType.Terminator);

        return new FieldDeclarationNode(token, typeNode, identifierNode);
    }

    /// <inheritdoc />
    public BindingPower Power(TokenType type) => BindingPower.Primary;
}
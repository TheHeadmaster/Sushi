using System.Diagnostics.CodeAnalysis;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Parsers;

/// <summary>
/// Represents a parser that can parse a specific token configuration.
/// </summary>
public interface IParser
{
    /// <summary>
    /// The binding power of the parser.
    /// </summary>
    /// <param name="type">
    /// The type of the token to check the <see cref="BindingPower"/> for.
    /// </param>
    /// <returns>
    /// The <see cref="BindingPower" />.
    /// </returns>
    public BindingPower Power(TokenType type);

    /// <summary>
    /// The type of the parser.
    /// </summary>
    public ParserType Type { get; }

    /// <summary>
    /// A list of roles that are used to determine when the parser can actually parse a statement.
    /// </summary>
    public List<ParserRole> Roles => [];

    /// <summary>
    /// The tokens which are recognized as being a starting point for this parser to consume them.
    /// </summary>
    public List<TokenType> AllowedStartTokens { get; }

    /// <summary>
    /// Parses a prefix <see cref="Token" />.
    /// </summary>
    /// <param name="parser">
    /// The main parser object.
    /// </param>
    /// <param name="token">
    /// The <see cref="Token" /> to parse.
    /// </param>
    /// <returns>
    /// An <see cref="ExpressionNode"/> or null if there was an issue.
    /// </returns>
    public Task<ExpressionNode?> ParsePrefix([NotNull] Parser parser, [NotNull] Token token) => Task.FromResult<ExpressionNode?>(null);

    /// <summary>
    /// Parses an infix <see cref="Token" />.
    /// </summary>
    /// <param name="parser">
    /// The main parser object.
    /// </param>
    /// <param name="left">
    /// The left side of the expression or null if there was an issue.
    /// </param>
    /// <param name="token">
    /// The <see cref="Token" /> to parse.
    /// </param>
    /// <returns>
    /// An <see cref="ExpressionNode"/> or null if there was an issue.
    /// </returns>
    public Task<ExpressionNode?> ParseInfix([NotNull] Parser parser, ExpressionNode? left, [NotNull] Token token) => Task.FromResult<ExpressionNode?>(null);

    /// <summary>
    /// Parses a statement <see cref="Token" />.
    /// </summary>
    /// <param name="parser">
    /// The main parser object.
    /// </param>
    /// <param name="token">
    /// The <see cref="Token"/> to parse.
    /// </param>
    /// <returns>
    /// An <see cref="ExpressionNode"/> or null if there was an issue.
    /// </returns>
    public Task<StatementNode?> ParseStatement([NotNull] Parser parser, [NotNull] Token token) => Task.FromResult<StatementNode?>(null);

    /*
    public TokenType? TerminatingToken { get; }
    */
}

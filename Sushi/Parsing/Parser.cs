using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Sushi.Diagnostics;
using Sushi.Diagnostics.Errors;
using Sushi.Parsing.Nodes;
using Sushi.Parsing.Parsers;
using Sushi.Parsing.Parsers.TopLevelStatements;
using Sushi.Tokenization;
using Sushi.Verification;

namespace Sushi.Parsing;

/// <summary>
/// Handles parsing a <see cref="List{T}"/> of <see cref="TokenFile"/> objects.
/// </summary>
public sealed class Parser
{
    /// <summary>
    /// The current index.
    /// </summary>
    private int currentIndex;

    /// <summary>
    /// The <see cref="List{T}"/> of <see cref="Token"/> objects to be parsed.
    /// </summary>
    private List<Token> tokens = null!;

    /// <summary>
    /// The list of messages accumulated from parsing errors and warnings.
    /// </summary>
    public List<CompilerMessage> Messages { get; set; } = [];

    /// <summary>
    /// The available parsers.
    /// </summary>

    private static readonly List<IParser> parsers = ReflectionEx.GetLeafSubclasses<IParser>();

    /// <summary>
    /// Parsers that are allowed to be a root (top-level) statement, which means they don't have to be a sub-statement in a block.
    /// </summary>
    private static readonly List<IParser> allowedRootStatementParsers = [];

    /// <summary>
    /// Returns whether the parser index is at the end of the file.
    /// </summary>
    /// <param name="lookahead">
    /// How many <see cref="Token"/> indices to look ahead. Defaults to 0.
    /// </param>
    /// <returns>
    /// True if the index is at or after the end of the file. False otherwise.
    /// </returns>
    private bool IsAtEnd(int lookahead = 0) => this.tokens.Count <= this.currentIndex + lookahead;

    /// <summary>
    /// Gets the parser of the specified type.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the parser. Must implement <see cref="IParser"/>.
    /// </typeparam>
    /// <returns>
    /// The <see cref="IParser"/> with the specified type.
    /// </returns>
    public static IParser GetParser<T>() where T : IParser, new() => parsers.OfType<T>().First();

    /// <summary>
    /// Peeks the next <see cref="Token"/>. If lookahead is greater than 0. Peeks at the position offset from the next <see cref="Token"/>.
    /// </summary>
    /// <param name="lookahead">How many indices to look ahead.</param>
    /// <returns>
    /// The <see cref="Token"/> or null if the position would be past the end of file.
    /// </returns>
    public Token? Peek(int lookahead = 0)
    {
        if (this.IsAtEnd(lookahead))
        {
            return null;
        }

        return this.tokens[this.currentIndex + lookahead];
    }

    /// <summary>
    /// Pops the next <see cref="Token"/> (does not remove it from the list, just advances the parser) and returns it.
    /// If the parser is already at the end of file, then this will return null and not advance.
    /// </summary>
    /// <returns>
    /// The <see cref="Token"/> or null if already at end of file.
    /// </returns>
    public Token? Pop()
    {
        Token? token = this.Peek();

        if (token is not null)
        {
            this.currentIndex++;
        }

        return token;
    }

    /// <summary>
    /// Gets the previous <see cref="Token"/> in the list.
    /// </summary>
    /// <returns>
    /// The <see cref="Token"/>. This can only be null if we are on the first <see cref="Token"/> in the list.
    /// </returns>
    public Token? Previous() => this.Peek(-1);

    /// <summary>
    /// Parses the source code from a <see cref="List{T}"/> of <see cref="TokenFile"/> objects into an <see cref="AbstractSyntaxTree"/>.
    /// </summary>
    /// <param name="tokenFiles">
    /// The source files to parse.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/> that returns an <see cref="AbstractSyntaxTree"/>.
    /// </returns>
    public async Task<AbstractSyntaxTree> ParseSource([NotNull] List<TokenFile> tokenFiles)
    {
        if (!allowedRootStatementParsers.Any())
        {
            allowedRootStatementParsers.Add(GetParser<UsingParser>());
            allowedRootStatementParsers.Add(GetParser<ClassParser>());
        }

        AbstractSyntaxTree tree = new();
        this.Messages = [];

        foreach (TokenFile file in tokenFiles)
        {
            this.tokens = file.Tokens;
            this.currentIndex = 0;
            tree.Children.Add(new FileNode(file.FilePath, file.FileName, await this.ParseStatements()));
        }

        tree.Messages = this.Messages;

        VerificationContext context = new();

        await tree.Verify(context);

        tree.Messages.AddRange(context.Messages);

        return tree;
    }

    /// <summary>
    /// Uses a snippet of tokens as the source instead of a source file. Mostly used for testing.
    /// </summary>
    /// <param name="tokens">
    /// The tokens that comprise the snippet.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public Task UseSnippet([NotNull] List<Token> tokens)
    {
        this.tokens = tokens;
        this.currentIndex = 0;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Parses the next set of tokens as an expression until the expression is complete.
    /// </summary>
    /// <param name="power">
    /// The current <see cref="BindingPower"/> that the parser is at.
    /// If the next token doesn't have a lower binding power than this
    /// value, return what we have.
    /// </param>
    /// <returns>
    /// The <see cref="ExpressionNode"/> or null if there no <see cref="Token" /> or there was an issue parsing the <see cref="Token"/>.
    /// </returns>
    public async Task<ExpressionNode?> ParseExpression(BindingPower power)
    {
        Token? token = await this.PeekAndExpectNotEOF();

        if (token is null)
        {
            return null;
        }

        if (parsers.FirstOrDefault(parser => parser.Type is ParserType.Prefix && parser.AllowedStartTokens.Contains(token.Type)) is not IParser prefix)
        {
            this.Messages.Add(new UnexpectedPrefixOperator(token));
            return null;
        }

        ExpressionNode? left = await prefix.ParsePrefix(this, token);

        this.Pop();

        if (left is null)
        {
            return null;
        }

        while ((token = this.Peek()) is not null && (int)power < (int)await GetPrecedence(token))
        {
            this.Pop();

            if (parsers.FirstOrDefault(parser => parser.Type is ParserType.Infix && parser.AllowedStartTokens.Contains(token.Type)) is not IParser infix)
            {
                this.Messages.Add(new UnexpectedInfixOperator(token));
                return left;
            }

            left = await infix.ParseInfix(this, left, token);

            if (left is null)
            {
                return null;
            }
        }

        return left;
    }

    /// <summary>
    /// Parses all of the statements until there is no token left.
    /// </summary>
    /// <returns>
    /// The <see cref="List{T}"/> of <see cref="StatementNode"/> objects that were parsed.
    /// </returns>
    public async Task<List<StatementNode>> ParseStatements()
    {
        Token? token;

        List<StatementNode> returnStatements = [];

        while ((token = this.Peek()) is not null)
        {
            StatementNode? statement = await this.ParseStatement(token, ParserRole.TopLevelStatement);

            if (statement is null)
            {
                continue;
            }

            returnStatements.Add(statement);
        }

        return returnStatements;
    }

    /// <summary>
    /// Parses the next <see cref="Token"/> as a statement.
    /// </summary>
    /// <param name="token">
    /// The <see cref="Token"/> to parse.
    /// </param>
    /// <param name="role">
    /// The <see cref="ParserRole"/> that determines what parsers can actually handle the statement.
    /// </param>
    /// <returns>
    /// The <see cref="StatementNode"/> or null if there was an issue.
    /// </returns>
    public async Task<StatementNode?> ParseStatement([NotNull] Token token, [NotNull] ParserRole role)
    {
        if (parsers.FirstOrDefault(parser => parser.Type is ParserType.Statement && parser.Roles.Contains(role) && parser.AllowedStartTokens.Contains(token.Type)) is not IParser statement)
        {
            StatementNode returnStatement = new ExpressionStatementNode(await this.ParseExpression(BindingPower.Primary));
            await this.ExpectAndPop(TokenType.Terminator);
            return returnStatement;
        }

        return await statement.ParseStatement(this, token);
    }

    /// <summary>
    /// Gets the precedence of the specified <see cref="Token"/>.
    /// </summary>
    /// <returns>
    /// The <see cref="BindingPower"/> of the <see cref="Token"/>.
    /// </returns>
    private static async Task<BindingPower> GetPrecedence([NotNull] Token token)
    {
        if (parsers.FirstOrDefault(parser => parser.Type is ParserType.Infix && parser.AllowedStartTokens.Contains(token.Type)) is not IParser infix)
        {
            return BindingPower.Primary;
        }

        return infix.Power(token.Type);
    }

    /// <summary>
    /// Asserts that the current <see cref="Token"/> is one of the specified <see cref="TokenType"/> values,
    /// and emits an error if it is not or if the end of file was reached.
    /// </summary>
    /// <param name="types">
    /// The <see cref="TokenType"/> values to expect.
    /// </param>
    /// <returns>
    /// The <see cref="Token"/> that was popped or null if the end of file was reached.
    /// </returns>
    public async Task<Token?> ExpectAndPop(params TokenType[] types)
    {
        if (await this.PeekAndExpectNotEOF() is not Token token)
        {
            return null;
        }

        if (!types.Contains(token.Type))
        {
            this.Messages.Add(new WrongTokenError(token, types));
        }

        this.Pop();

        return token;
    }

    /// <summary>
    /// Peeks the current <see cref="Token"/> and emits an error if there isn't one.
    /// </summary>
    /// <returns>
    /// The <see cref="Token"/> or null if there isn't one.
    /// </returns>
    public Task<Token?> PeekAndExpectNotEOF()
    {
        Token? token = this.Peek();

        if (token is null)
        {
            // Since we filter out files that have no tokens in the lexing step,
            // We can assume every file has at least one token, and therefore
            // if Peek(0) returns null then Previous() must return a non-null value.
            Token previous = this.Previous()!;
            this.Messages.Add(new UnexpectedEndOfFile(previous));
        }

        return Task.FromResult(token);
    }
}
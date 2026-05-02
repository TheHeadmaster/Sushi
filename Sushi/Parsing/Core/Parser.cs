using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Sushi.Diagnostics.Errors;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.Parsing.Core;

/// <summary>
/// Handles parsing a <see cref="List{T}"/> of <see cref="TokenFile"/> objects into an <see cref="AbstractSyntaxTree"/>.
/// </summary>
public sealed class Parser
{
    /// <summary>
    /// The current <see cref="AbstractSyntaxTree"/> that was generated as a result of parsing. Can be updated incrementally after first generation.
    /// </summary>
    private AbstractSyntaxTree tree = new();

    /// <summary>
    /// The <see cref="List{T}"/> of <see cref="Token"/> objects to be parsed.
    /// </summary>
    private List<Token> tokens = null!;

    /// <summary>
    /// The current index.
    /// </summary>
    private int currentIndex;

    /// <summary>
    /// The current file.
    /// </summary>
    private TokenFile currentFile = null!;

    /// <summary>
    /// The current file node.
    /// </summary>
    public FileNode CurrentFileNode { get; private set; } = null!;

    /// <summary>
    /// The available parsers.
    /// </summary>

    private static readonly List<IParser> parsers = ReflectionEx.GetLeafSubclasses<IParser>();

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
    public async Task<AbstractSyntaxTree> ParseFiles([NotNull] List<TokenFile> tokenFiles)
    {
        AbstractSyntaxTree tree = new();

        foreach (TokenFile file in tokenFiles)
        {
            await this.ParseFile(file);
        }

        this.tree = tree;

        return tree;
    }

    /// <summary>
    /// Parses the source code from a <see cref="TokenFile"/> into a <see cref="FileNode"/>.
    /// </summary>
    /// <param name="file">
    /// The source file to parse.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task ParseFile([NotNull] TokenFile file)
    {
        this.tokens = file.Tokens;
        this.currentIndex = 0;
        this.currentFile = file;

        List<StatementNode> statements = [];
        this.CurrentFileNode = new FileNode(file.FilePath, file.FileName, statements);
        statements.AddRange(await this.ParseStatements());
        this.tree.Children.Add(this.CurrentFileNode);
    }

    /// <summary>
    /// Parses all of the statements until there is no tokens left.
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
    /// Peeks the current <see cref="Token"/> and emits an error if there isn't one.
    /// </summary>
    /// <returns>
    /// The <see cref="Token"/> or null if there isn't one.
    /// </returns>
    public async Task<Token?> PeekAndExpectNotEOF()
    {
        Token? token = this.Peek();

        if (token is null)
        {
            // Since we filter out files that have no tokens in the lexing step,
            // We can assume every file has at least one token, and therefore
            // if Peek(0) returns null then Previous() must return a non-null value.
            Token previous = this.Previous()!;
            await this.CurrentFileNode.AddMessage(new UnexpectedEndOfFileError(previous, this.currentFile.FilePath));
        }

        return token;
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
            await this.CurrentFileNode.AddMessage(new WrongTokenError(token, types, this.currentFile.FilePath));
        }

        this.Pop();

        return token;
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
            await this.CurrentFileNode.AddMessage(new UnexpectedPrefixOperator(token, this.currentFile.FilePath));
            return null;
        }

        ExpressionNode? left = await prefix.ParsePrefix(this, token);

        if (left is null)
        {
            return null;
        }

        while ((token = this.Peek()) is not null && (int)power < (int)await GetPrecedence(token))
        {
            this.Pop();

            if (parsers.FirstOrDefault(parser => parser.Type is ParserType.Infix && parser.AllowedStartTokens.Contains(token.Type)) is not IParser infix)
            {
                await this.CurrentFileNode.AddMessage(new UnexpectedInfixOperator(token, this.currentFile.FilePath));
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
    /// Adds a new <see cref="TokenFile"/> to the tree as a <see cref="FileNode"/>.
    /// </summary>
    /// <param name="file">
    /// The <see cref="TokenFile"/> to add.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task AddFile([NotNull] TokenFile file) => await this.ParseFile(file);

    /// <summary>
    /// Removes an existing <see cref="TokenFile"/> from the tree.
    /// </summary>
    /// <param name="file">
    /// The <see cref="TokenFile"/> to remove.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task RemoveFile([NotNull] TokenFile file)
    {
        FileNode? existing = this.tree.Children.FirstOrDefault(x => x.FilePath.IsSamePath(file.FilePath));

        if (existing is null)
        {
            return;
        }

        this.tree.Children.Remove(existing);
    }

    /// <summary>
    /// Updates an existing <see cref="TokenFile"/> from the tree.
    /// </summary>
    /// <param name="file">
    /// The <see cref="TokenFile"/> to update.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task UpdateFile([NotNull] TokenFile file)
    {
        await this.RemoveFile(file);
        await this.AddFile(file);
    }
}

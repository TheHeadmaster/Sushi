using System.Diagnostics.CodeAnalysis;
using Sushi.Diagnostics;
using Sushi.Parsing.Nodes;
using Sushi.Parsing.Parsers;
using Sushi.Tokenization;

namespace Sushi.Parsing;

/// <summary>
/// Handles parsing a <see cref="List{T}"/> of <see cref="TokenFile"/> objects.
/// </summary>
public sealed class Parser
{
    private int currentIndex;

    private List<Token> tokens = null!;

    public List<CompilerMessage> Messages { get; set; } = [];

    private static readonly Dictionary<TokenType, IPrefixParser> prefixes = new()
    {
        { TokenType.Minus, new PrefixOperatorParser() },
        { TokenType.NumberLiteral, new ConstantParser() },
        { TokenType.TrueLiteral, new ConstantParser() },
        { TokenType.FalseLiteral, new ConstantParser() },
        { TokenType.Identifier, new IdentifierParser() },
        { TokenType.OpeningParenthesis, new GroupParser() },
    };

    private static readonly Dictionary<TokenType, IInfixParser> infixes = new()
    {
        { TokenType.Plus, new InfixOperatorParser(BindingPower.SumDifference) },
        { TokenType.Minus, new InfixOperatorParser(BindingPower.SumDifference) },
        { TokenType.Asterisk, new InfixOperatorParser(BindingPower.ProductQuotient) },
        { TokenType.Slash, new InfixOperatorParser(BindingPower.ProductQuotient) },
        { TokenType.Assignment, new AssignmentParser()  },
        { TokenType.OpeningParenthesis, new MethodCallParser() },
        { TokenType.Dot, new NamespaceParser() }
    };

    private static readonly Dictionary<TokenType, IStatementParser> statements = new()
    {
        { TokenType.Using, new UsingParser() },
        { TokenType.Namespace, new NamespaceDeclarationParser() },
        { TokenType.If, new IfParser() }
    };

    private bool IsAtEnd(int lookahead = 0) => this.tokens.Count <= this.currentIndex + lookahead;

    public Token? Peek(int lookahead = 0)
    {
        if (this.IsAtEnd(lookahead))
        {
            return null;
        }

        return this.tokens[this.currentIndex + lookahead];
    }

    public Token? Pop()
    {
        Token? token = this.Peek();

        if (token is not null)
        {
            this.currentIndex++;
        }

        return token;
    }

    private Token Previous() => this.Peek(-1)!;

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
        AbstractSyntaxTree tree = new();
        this.Messages = [];

        foreach (TokenFile file in tokenFiles)
        {
            this.tokens = file.Tokens;
            this.currentIndex = 0;
            tree.Children.AddRange(await this.ParseStatements());
        }

        tree.Messages = this.Messages;

        return tree;
    }

    public async Task<ExpressionNode> ParseExpression(BindingPower power)
    {
        Token? token = this.Pop() ?? throw new NotImplementedException();

        if (!prefixes.TryGetValue(token.Type, out IPrefixParser? prefix))
        {
            throw new NotImplementedException();
        }

        ExpressionNode left = await prefix.Parse(this, token);

        while ((int)power < (int)await this.GetPrecedence())
        {
            if (this.Peek() is null)
            {
                break;
            }

            token = this.Pop()!;

            IInfixParser infix = infixes[token.Type];

            left = await infix.Parse(this, left, token);
        }

        return left;
    }

    public async Task<List<StatementNode>> ParseStatements()
    {
        Token? token;

        List<StatementNode> returnStatements = [];

        while ((token = this.Peek()) is not null)
        {
            returnStatements.Add(await this.ParseStatement(token));
        }

        return returnStatements;
    }

    public async Task<StatementNode> ParseStatement([NotNull] Token token)
    {
        if (!statements.TryGetValue(token.Type, out IStatementParser? statement))
        {
            StatementNode returnStatement = new ExpressionStatementNode(await this.ParseExpression(BindingPower.Primary));
            await this.ExpectAndPop(TokenType.Terminator);
            return returnStatement;
        }

        return await statement.Parse(this, token);
    }

    private async Task<BindingPower> GetPrecedence()
    {
        Token? token = this.Peek();
        if (token is null || !infixes.TryGetValue(token.Type, out IInfixParser? infix))
        {
            return 0;
        }

        return infix.Power();
    }

    public Task ExpectAndPop(TokenType type)
    {
        if (this.Peek()?.Type != type)
        {
            throw new NotImplementedException();
        }

        this.Pop();

        return Task.CompletedTask;
    }
}
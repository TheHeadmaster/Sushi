using System.Diagnostics.CodeAnalysis;
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

    private static readonly Dictionary<TokenType, IPrefixParser> prefixes = new()
    {
        { TokenType.Minus, new PrefixOperatorParser() },
        { TokenType.NumberLiteral, new ConstantParser() },
        { TokenType.TrueLiteral, new ConstantParser() },
        { TokenType.FalseLiteral, new ConstantParser() },
        { TokenType.Identifier, new IdentifierParser() },
        { TokenType.OpeningParenthesis, new GroupParser() }
    };

    private static readonly Dictionary<TokenType, IInfixParser> infixes = new()
    {
        { TokenType.Plus, new InfixOperatorParser(BindingPower.SumDifference) },
        { TokenType.Minus, new InfixOperatorParser(BindingPower.SumDifference) },
        { TokenType.Asterisk, new InfixOperatorParser(BindingPower.ProductQuotient) },
        { TokenType.Slash, new InfixOperatorParser(BindingPower.ProductQuotient) }
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

        foreach (TokenFile file in tokenFiles)
        {
            this.tokens = file.Tokens;
            this.currentIndex = 0;
            tree.Children.Add(await this.ParseExpression(BindingPower.Primary));
        }

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

        while ((int)power < (int)await GetPrecedence(token = this.Peek()))
        {
            if (token is null)
            {
                break;
            }

            this.Pop();

            IInfixParser infix = infixes[token.Type];

            left = await infix.Parse(this, left, token);
        }

        return left;
    }

    private static async Task<BindingPower> GetPrecedence(Token? token)
    {
        if (token is null || !infixes.TryGetValue(token.Type, out IInfixParser? infix))
        {
            return 0;
        }

        return infix.Power();
    }
}
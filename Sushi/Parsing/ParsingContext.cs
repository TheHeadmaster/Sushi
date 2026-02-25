using System;
using System.Collections.Generic;
using System.Text;
using Sushi.Lexing.Tokenization;

namespace Sushi.Parsing;

public sealed class ParsingContext
{
    public required List<Token> Tokens { get; set; }

    private int currentIndex;

    public bool IsAtEnd(int lookahead = 0) => this.Tokens.Count <= this.currentIndex + lookahead;

    public Token? Peek(int lookahead = 0)
    {
        if (this.IsAtEnd(lookahead))
        {
            return null;
        }

        return this.Tokens[this.currentIndex + lookahead];
    }

    public void Pop() => this.currentIndex++;

    public bool IsAnyOf(params TokenType[] types)
    {
        ArgumentNullException.ThrowIfNull(types);

        Token? token = this.Peek();

        if (token is null)
        {
            return false;
        }

        foreach (TokenType type in types)
        {
            if (token.Type == type)
            {
                return true;
            }
        }

        return false;
    }

    public Token EndOfFileToken()
    {
        Token previousToken = this.Previous();
        return new()
        {
            LineNumber = previousToken.LineNumber,
            LinePosition = previousToken.LinePosition + previousToken.Value.Length,
            CurrentLine = previousToken.CurrentLine,
            Type = TokenType.Unknown,
            Value = "",
        };
    }

    public Token Previous() => this.Peek(-1)!;

    public List<CompilerError> Errors { get; } = [];
}

using System;
using System.Collections.Generic;
using System.Text;
using Serilog.Parsing;
using Sushi.Lexing.Tokenization;

namespace Sushi.Parsing;

public sealed class ParsingContext
{
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

    public List<CompilerError> Errors { get; } = [];
}

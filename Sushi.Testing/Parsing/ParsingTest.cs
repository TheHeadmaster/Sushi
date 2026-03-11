using System;
using System.Collections.Generic;
using System.Text;
using Sushi.Tokenization;

namespace Sushi.Testing.Parsing;

public abstract class ParsingTest
{
    protected Token DummyToken(TokenType type, string value = "") => new()
    {
        LineNumber = 0,
        LinePosition = 0,
        Type = type,
        Value = value
    };
}

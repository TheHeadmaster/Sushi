using System;
using System.Collections.Generic;
using System.Text;

namespace Sushi.Lexing;

public sealed class SushiToken
{
    public required TokenType Type { get; set; }

    public required string Value { get; set; }
}

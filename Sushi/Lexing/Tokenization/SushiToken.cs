using System;
using System.Collections.Generic;
using System.Text;

namespace Sushi.Lexing.Tokenization;

public sealed class SushiToken
{
    public required TokenType Type { get; set; }

    public required string Value { get; set; }

    public required long LineNumber { get; set; }

    public required long LinePosition { get; set; }
}

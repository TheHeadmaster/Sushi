using System;
using System.Collections.Generic;
using System.Text;
using Sushi.Lexing.Tokenization;

namespace Sushi.Lexing;

public sealed class SourceFile
{
    public required string FileName { get; set; }

    public required string FilePath { get; set; }

    public required string Source { get; set; }

    public List<SushiToken> Tokens { get; set; } = [];
}

using System;
using System.Collections.Generic;
using System.Text;

namespace Sushi.Lexing.Tokenization;

public sealed class CommentTokenGenerator : TokenGenerator
{
    public override Task<TokenGeneratorResult> Generate(string source, int index)
    {
    }
}

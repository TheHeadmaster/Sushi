using System;
using System.Collections.Generic;
using System.Text;

namespace Sushi.Lexing.Tokenization;

public abstract class TokenGenerator
{
    public abstract Task<TokenGeneratorResult> Generate(string source, long index);
}

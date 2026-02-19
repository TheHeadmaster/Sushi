using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Sushi.Lexing.Tokenization;

public sealed class TokenGeneratorResult
{
    public required bool CanGenerate { get; set; }

    [MemberNotNullWhen(true, nameof(CanGenerate))]
    public int? Affinity { get; set; }

    [MemberNotNullWhen(true, nameof(CanGenerate))]
    public Token? Token { get; set; }
}

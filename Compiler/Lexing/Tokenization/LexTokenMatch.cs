using Sushi.Diagnostics;

namespace Sushi.Lexing.Tokenization;

public readonly record struct LexTokenMatch(LexTokenType Type, int Length, IReadOnlyList<SushiDiagnostic>? Diagnostics = null);
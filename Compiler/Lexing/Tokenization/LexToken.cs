using Sushi.Source;

namespace Sushi.Lexing.Tokenization;

public readonly record struct LexToken(LexTokenType Type, SourceSpan Span);
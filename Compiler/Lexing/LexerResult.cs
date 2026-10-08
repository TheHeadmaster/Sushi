using Sushi.Diagnostics;
using Sushi.Lexing.Tokenization;
using Sushi.Source;

namespace Sushi.Lexing;

/// <summary>
/// Represents a result of lexing a source snapshot.
/// </summary>
/// <param name="Snapshot">
/// The source snapshot that was lexed.
/// </param>
/// <param name="Tokens">
/// The <see cref="LexToken"/> objects produced while lexing.
/// </param>
public sealed record LexerResult(SourceSnapshot Snapshot, IReadOnlyList<LexToken> Tokens);
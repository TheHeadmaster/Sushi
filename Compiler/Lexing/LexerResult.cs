using Sushi.Diagnostics;
using Sushi.Source;

namespace Sushi.Lexing;

/// <summary>
/// Represents a result of lexing a source snapshot.
/// </summary>
/// <param name="Snapshot">
/// The source snapshot that was lexed.
/// </param>
/// <param name="Diagnostics">
/// The diagnostics produced while lexing.
/// </param>
public sealed record LexerResult(SourceSnapshot Snapshot, IReadOnlyList<SushiDiagnostic> Diagnostics);
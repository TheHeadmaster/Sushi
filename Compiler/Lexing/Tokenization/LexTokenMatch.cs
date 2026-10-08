using Sushi.Diagnostics;

namespace Sushi.Lexing.Tokenization;

/// <summary>
/// Describes a lexical element recognized by a tokenizer before it is committed to the lexical and diagnostic streams.
/// </summary>
/// <param name="Type">
/// The lexical classification of the recognized element.
/// </param>
/// <param name="Length">
/// The number of canonical source bytes consumed by the recognized element.
/// </param>
/// <param name="DiagnosesFollowingBoundary">
/// Whether at least one diagnostic produced by this match semantically accounts for a lexical failure at the source boundary immediately following the recognized element.
/// </param>
/// <param name="DiagnosticReporter">
/// Diagnostic reporter produced while recognizing the element, or null when recognition produced no diagnostics.
/// </param>
public readonly record struct LexTokenMatch(LexTokenType Type, int Length, bool DiagnosesFollowingBoundary = false,  IDiagnosticReporter? DiagnosticReporter = null);
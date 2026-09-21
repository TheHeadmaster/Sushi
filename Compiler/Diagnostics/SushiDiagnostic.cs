using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sushi.Source;

namespace Sushi.Diagnostics;

/// <summary>
/// Represents a diagnostic message from any point in the compiler pipeline that should be shown to the user while in an IDE.
/// </summary>
/// <param name="Code">
/// The diagnostic code.
/// </param>
/// <param name="Message">
/// The message text.
/// </param>
/// <param name="Severity">
/// The severity class of the diagnostic.
/// </param>
/// <param name="Span">
/// The <see cref="SourceSpan"/> that the diagnostic originated from.
/// </param>
public sealed record SushiDiagnostic(string Code, string Message, DiagnosticSeverity Severity, SourceSpan Span);
using Sushi.Source;

namespace Sushi.Diagnostics;

/// <summary>
/// Represents a diagnostic message from any point in the compiler pipeline that should be reported.
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
public sealed record SushiDiagnostic(int Code, string Message, DiagnosticSeverity Severity, SourceSpan Span)
{
    /// <summary>
    /// Gets the display name of this <see cref="SushiDiagnostic"/>. Display names are in the format SUS(E|W|I|H)[0-9]{4}.
    /// </summary>
    public string DisplayName => this.Severity switch
    {
        DiagnosticSeverity.Error => $"SUSE{this.Code:D4}",
        DiagnosticSeverity.Warning => $"SUSW{this.Code:D4}",
        DiagnosticSeverity.Information => $"SUSI{this.Code:D4}",
        DiagnosticSeverity.Hint => $"SUSH{this.Code:D4}",
        _ => throw new InvalidOperationException($"Severity is invalid value {this.Severity}.")
    };

    /// <summary>
    /// Returns whether the specified error type is the same type represented by this diagnostic's code and severity.
    /// </summary>
    /// <param name="type">
    /// The type to check.
    /// </param>
    /// <returns>
    /// True if it is the same error type. False otherwise.
    /// </returns>
    public bool IsType(ErrorType type) => this.Code == (int)type && this.Severity == DiagnosticSeverity.Error;

    /// <summary>
    /// Returns whether the specified warning type is the same type represented by this diagnostic's code and severity.
    /// </summary>
    /// <param name="type">
    /// The type to check.
    /// </param>
    /// <returns>
    /// True if it is the same warning type. False otherwise.
    /// </returns>
    public bool IsType(WarningType type) => this.Code == (int)type && this.Severity == DiagnosticSeverity.Warning;
}
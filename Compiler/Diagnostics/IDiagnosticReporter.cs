using Sushi.Source;

namespace Sushi.Diagnostics;

/// <summary>
/// Handles diagnostic reporting such as generating and accumulating diagnostics,
/// and reporting them down the pipeline.
/// </summary>
public interface IDiagnosticReporter
{
    /// <summary>
    /// Reports the diagnostics accumulated by the diagnostic reporter.
    /// </summary>
    /// <returns>
    /// A read-only list containing the <see cref="SushiDiagnostic"/> items.
    /// </returns>
    public IReadOnlyList<SushiDiagnostic> ReportDiagnostics();

    /// <summary>
    /// Returns whether the diagnostic reporter has accumulated any errors so far.
    /// </summary>
    /// <returns>
    /// True if the diagnostic reporter has accumulated at least one error. False otherwise.
    /// </returns>
    public bool HasErrors();

    /// <summary>
    /// Returns whether the diagnostic reporter has accumulated any warnings so far.
    /// </summary>
    /// <returns>
    /// True if the diagnostic reporter has accumulated at least one warning. False otherwise.
    /// </returns>
    public bool HasWarnings();

    /// <summary>
    /// Returns whether the diagnostic reporter has accumulated any diagnostics so far.
    /// </summary>
    /// <returns>
    /// True if the diagnostic reporter has accumulated at least one diagnostic. False otherwise.
    /// </returns>
    public bool HasDiagnostics();

    /// <summary>
    /// Generates an error of the specified <see cref="ErrorType"/> and posts it to the diagnostic reporter.
    /// </summary>
    /// <param name="type">
    /// The <see cref="ErrorType"/> of the error to generate.
    /// </param>
    /// <param name="sourceSpan">
    /// The <see cref="SourceSpan"/> that this error pertains to.
    /// </param>
    public void GenerateError(ErrorType type, SourceSpan sourceSpan);

    /// <summary>
    /// Generates an error of the specified <see cref="ErrorType"/> with a custom message and posts it to the diagnostic reporter.
    /// </summary>
    /// <param name="type">
    /// The <see cref="ErrorType"/> of the error to generate.
    /// </param>
    /// <param name="message">
    /// The message for the error. Used for parameterized error messages that reference identifiers or similar.
    /// </param>
    /// <param name="sourceSpan">
    /// The <see cref="SourceSpan"/> that this error pertains to.
    /// </param>
    public void GenerateErrorWithCustomMessage(ErrorType type, string message, SourceSpan sourceSpan);

    /// <summary>
    /// Generates a warning of the specified <see cref="WarningType"/> and posts it to the diagnostic reporter.
    /// </summary>
    /// <param name="type">
    /// The <see cref="WarningType"/> of the warning to generate.
    /// </param>
    /// <param name="sourceSpan">
    /// The <see cref="SourceSpan"/> that this warning pertains to.
    /// </param>
    public void GenerateWarning(WarningType type, SourceSpan sourceSpan);

    /// <summary>
    /// Generates a warning of the specified <see cref="WarningType"/> with a custom message and posts it to the diagnostic reporter.
    /// </summary>
    /// <param name="type">
    /// The <see cref="WarningType"/> of the warning to generate.
    /// </param>
    /// <param name="message">
    /// The message for the warning. Used for parameterized warning messages that reference identifiers or similar.
    /// </param>
    /// <param name="sourceSpan">
    /// The <see cref="SourceSpan"/> that this warning pertains to.
    /// </param>
    public void GenerateWarningWithCustomMessage(WarningType type, string message, SourceSpan sourceSpan);

    /// <summary>
    /// Commits a reporter's diagnostics to this diagnostic reporter.
    /// </summary>
    /// <param name="diagnosticReporter">
    /// The diagnostic reporter whose diagnostics to commit.
    /// </param>
    public void CommitReporter(IDiagnosticReporter diagnosticReporter);
}
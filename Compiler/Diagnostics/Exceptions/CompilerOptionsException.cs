namespace Sushi.Diagnostics.Exceptions;

/// <summary>
/// Thrown when <see cref="CompilerOptions"/> encounters an options validation or syntax error.
/// </summary>
#pragma warning disable CA1032 // Implement standard exception constructors
public sealed class CompilerOptionsException(CompilerOptionsError error, string message) : Exception(message)
#pragma warning restore CA1032 // Implement standard exception constructors
{
    /// <summary>
    /// The type of the error that was thrown.
    /// </summary>
    public CompilerOptionsError Error { get; } = error;
}
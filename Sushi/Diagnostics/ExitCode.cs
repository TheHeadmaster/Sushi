namespace Sushi.Diagnostics;

/// <summary>
/// Represents a specific reason why the program has exited.
/// </summary>
public enum ExitCode
{
    Success,
    UnhandledException,
    ProjectPathNotSpecified,
    ProjectPathNotFound,
    InvalidParameterSyntax
}
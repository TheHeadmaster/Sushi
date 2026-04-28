namespace Sushi.Diagnostics;

/// <summary>
/// Represents a specific reason why the program has exited.
/// </summary>
public enum ExitCode
{
    /// <summary>
    /// The program exited successfully.
    /// </summary>
    Success,

    /// <summary>
    /// There was an unhandled exception.
    /// </summary>
    UnhandledException,

    /// <summary>
    /// The project path was not specified.
    /// </summary>
    ProjectPathNotSpecified,

    /// <summary>
    /// The proejct path was not found on disk.
    /// </summary>
    ProjectPathNotFound,

    /// <summary>
    /// The parameter syntax for command-line arguments was invalid.
    /// </summary>
    InvalidParameterSyntax,

    /// <summary>
    /// There was a lexing syntax error and the project should not be compiled.
    /// </summary>
    LexingSyntaxError,

    /// <summary>
    /// A process (usually gcc) failed to start.
    /// </summary>
    ProcessFailedToStart,

    /// <summary>
    /// A process (usually gcc) failed while executing and needed to stop.
    /// </summary>
    ProcessFailedDuringExecution
}
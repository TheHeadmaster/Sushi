namespace Sushi.Diagnostics;

/// <summary>
/// Represents the type of message a <see cref="CompilerMessage"/> is.
/// </summary>
public enum CompilerMessageType
{
    /// <summary>
    /// This message is an error. Compilation will not complete.
    /// </summary>
    Error,

    /// <summary>
    /// This message is a warning. Compilation will continue, but it will be reported.
    /// </summary>
    Warning
}

namespace Sushi.Diagnostics;

/// <summary>
/// Represents the severity of a <see cref="CompilerError"/>. Warnings do not stop
/// compilation, but warn the user of potential code flaws such as dead code.
/// </summary>
public enum ErrorSeverity
{
    Error,
    Warning
}
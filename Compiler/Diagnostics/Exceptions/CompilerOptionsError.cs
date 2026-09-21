namespace Sushi.Diagnostics.Exceptions;

/// <summary>
/// Represents the type of error that <see cref="CompilerOptions"/> threw.
/// </summary>
public enum CompilerOptionsError
{
    InvalidParameterSyntax,
    InvalidProjectFileOrFolder
}
using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Diagnostics.Errors;

/// <summary>
/// Represents a syntax error.
/// </summary>
/// <param name="currentLine">
/// The line that the syntax error was reported on.
/// </param>
/// <param name="lineNumber">
/// The line number of the syntax error.
/// </param>
/// <param name="linePosition">
/// The position in the line that the syntax error was reported on.
/// </param>
public sealed class SyntaxError([NotNull] string currentLine, [NotNull] int lineNumber, [NotNull] int linePosition, [NotNull] Token token, [NotNull] string filePath) : CompilerMessage(currentLine, lineNumber, linePosition, filePath)
{
    /// <inheritdoc />
    public override int MessageNumber => 1;

    /// <inheritdoc />
    public override CompilerMessageType Type => CompilerMessageType.Error;

    /// <inheritdoc />
    public override Task<string> GetDescription() => Task.FromResult($"Invalid syntax — token \"{token.Value}\" is not valid in this context");

    /// <inheritdoc />
    public override Task<int> GetMessageSpan() => Task.FromResult(token.Value.Length);
}

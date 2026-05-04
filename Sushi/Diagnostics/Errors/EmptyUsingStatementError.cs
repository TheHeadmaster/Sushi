using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Diagnostics.Errors;

/// <summary>
/// Error that is emitted when a using statement doesn't have an expression body.
/// </summary>
/// <param name="usingToken">
/// The <see cref="Token"/> with the using keyword.
/// </param>
/// <param name="filePath">
/// The path of the file that the error came from.
/// </param>
public sealed class EmptyUsingStatementError([NotNull] Token usingToken, [NotNull] string filePath) : CompilerMessage(usingToken.CurrentLine, usingToken.LineNumber, usingToken.LinePosition, filePath)
{
    /// <inheritdoc />
    public override CompilerMessageType Type => CompilerMessageType.Error;

    /// <inheritdoc />
    public override Task<string> GetDescription() => Task.FromResult($"Using statement must be followed by a valid expression");

    /// <inheritdoc />
    public override Task<int> GetMessageSpan() => Task.FromResult(usingToken.Value.Length);
}

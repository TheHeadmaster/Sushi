using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Diagnostics.Errors;

/// <summary>
/// Error that is emitted when an expression term cannot be parsed.
/// </summary>
/// <param name="token">
/// The <see cref="Token"/> that couldn't be parsed.
/// </param>
/// <param name="filePath">
/// The path of the file that the error came from.
/// </param>
public sealed class InvalidExpressionTermError([NotNull] Token token, [NotNull] string filePath) : CompilerMessage(token.CurrentLine, token.LineNumber, token.LinePosition, filePath)
{
    /// <inheritdoc />
    public override CompilerMessageType Type => CompilerMessageType.Error;

    /// <inheritdoc />
    public override Task<string> GetDescription() => Task.FromResult($"\"{token.Value}\" is not a valid expression term");

    /// <inheritdoc />
    public override Task<int> GetMessageSpan() => Task.FromResult(token.Value.Length);
}

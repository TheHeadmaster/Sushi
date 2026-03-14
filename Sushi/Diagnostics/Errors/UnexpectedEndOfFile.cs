using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Diagnostics.Errors;

/// <summary>
/// Error that is emitted when the parser has reached the end of the file in a context where it expects another token,
/// Such as halfway through an incomplete statement.
/// </summary>
/// <param name="startToken">
/// The token where the error starts.
/// </param>
public sealed class UnexpectedEndOfFile([NotNull] Token startToken) : CompilerMessage(startToken.CurrentLine, startToken.LineNumber, startToken.LinePosition + startToken.Value.Length)
{
    /// <inheritdoc />
    public override int MessageNumber => 3;

    /// <inheritdoc />
    public override CompilerMessageType Type => CompilerMessageType.Error;

    /// <inheritdoc />
    public override Task<string> GetDescription() => Task.FromResult("Unexpected end of file");

    /// <inheritdoc />
    public override Task<int> GetMessageSpan() => Task.FromResult(1);
}

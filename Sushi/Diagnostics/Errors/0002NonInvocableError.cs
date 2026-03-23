using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Diagnostics.Errors;

/// <summary>
/// Error that is emitted when the author tries to write something like 3() or something similar.
/// Only an expression that resolves to a method can be called like this.
/// </summary>
/// <param name="startToken">
/// The token where the error starts.
/// </param>
/// <param name="endToken">
/// The token where the error ends.
/// </param>
public sealed class NonInvocableError([NotNull] Token startToken, [NotNull] Token endToken) : CompilerMessage(startToken.CurrentLine, startToken.LineNumber, startToken.LinePosition)
{
    /// <inheritdoc />
    public override int MessageNumber => 2;

    /// <inheritdoc />
    public override CompilerMessageType Type => CompilerMessageType.Error;

    /// <inheritdoc />
    public override Task<string> GetDescription() => Task.FromResult($"\"{startToken.Value}\" is non-invocable and cannot be used like a method");

    /// <inheritdoc />
    public override Task<int> GetMessageSpan() => Task.FromResult(endToken.LinePosition - startToken.LinePosition + endToken.Value.Length);
}

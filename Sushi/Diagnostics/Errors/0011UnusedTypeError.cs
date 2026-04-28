using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Diagnostics.Errors;

/// <summary>
/// Error that is emitted when a linear type is not used.
/// </summary>
/// <param name="startToken">
/// The token where the error starts.
/// </param>
/// <param name="name">
/// The variable name.
/// </param>
public sealed class UnusedTypeError([NotNull] Token startToken, string name, [NotNull] string filePath) : CompilerMessage(startToken.CurrentLine, startToken.LineNumber, startToken.LinePosition, filePath)
{
    /// <inheritdoc />
    public override int MessageNumber => 11;

    /// <inheritdoc />
    public override CompilerMessageType Type => CompilerMessageType.Error;

    /// <inheritdoc />
    public override Task<string> GetDescription() => Task.FromResult($"{name} is not explicitly used and will go out of scope");

    /// <inheritdoc />
    public override Task<int> GetMessageSpan() => Task.FromResult(startToken.Value.Length);
}
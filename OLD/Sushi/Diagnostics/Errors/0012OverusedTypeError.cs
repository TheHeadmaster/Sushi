using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Diagnostics.Errors;

/// <summary>
/// Error that is emitted when a linear type is used more than once.
/// </summary>
/// <param name="startToken">
/// The token where the error starts.
/// </param>
/// <param name="name">
/// The variable name.
/// </param>
public sealed class OverusedTypeError([NotNull] Token startToken, string name, [NotNull] string filePath) : CompilerMessage(startToken.CurrentLine, startToken.LineNumber, startToken.LinePosition, filePath)
{
    /// <inheritdoc />
    public override int MessageNumber => 12;

    /// <inheritdoc />
    public override CompilerMessageType Type => CompilerMessageType.Error;

    /// <inheritdoc />
    public override Task<string> GetDescription() => Task.FromResult($"{name} is used more than once");

    /// <inheritdoc />
    public override Task<int> GetMessageSpan() => Task.FromResult(startToken.Value.Length);
}
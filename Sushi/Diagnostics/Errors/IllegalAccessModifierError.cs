using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Diagnostics.Errors;

/// <summary>
/// Error that is emitted when a modifier is used on something that isn't modifiable or restricted to a specific subset of modifiers.
/// </summary>
/// <param name="startToken">
/// The token where the error starts.
/// </param>
/// <param name="description">
/// The description of why the modifier is illegal.
/// </param>
public sealed class IllegalModifierError([NotNull] Token startToken, [NotNull] string description, [NotNull] string filePath) : CompilerMessage(startToken.CurrentLine, startToken.LineNumber, startToken.LinePosition, filePath)
{
    /// <inheritdoc />
    public override CompilerMessageType Type => CompilerMessageType.Error;

    /// <inheritdoc />
    public override Task<string> GetDescription() => Task.FromResult(description);

    /// <inheritdoc />
    public override Task<int> GetMessageSpan() => Task.FromResult(startToken.Value.Length);
}

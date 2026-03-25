using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Diagnostics.Errors;

/// <summary>
/// Error that is emitted when an access modifier is used on something that isn't access modifiable.
/// </summary>
/// <param name="startToken">
/// The token where the error starts.
/// </param>
public sealed class IllegalAccessModifierError([NotNull] Token startToken) : CompilerMessage(startToken.CurrentLine, startToken.LineNumber, startToken.LinePosition)
{
    /// <inheritdoc />
    public override int MessageNumber => 9;

    /// <inheritdoc />
    public override CompilerMessageType Type => CompilerMessageType.Error;

    /// <inheritdoc />
    public override Task<string> GetDescription() => Task.FromResult($"Access modifier \"{startToken.Value}\" cannot be used in this context");

    /// <inheritdoc />
    public override Task<int> GetMessageSpan() => Task.FromResult(startToken.Value.Length);
}
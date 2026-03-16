using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Diagnostics.Errors;

/// <summary>
/// Error that is emitted when a namespace declaration is resolved into an invalid expression.
/// Namespace declarations should only contain identifiers and the dot operator.
/// </summary>
/// <param name="startToken">
/// The token where the error starts.
/// </param>
/// <param name="endToken">
/// The token where the error ends.
/// </param>
public sealed class InvalidNamespaceError([NotNull] Token startToken, [NotNull] Token endToken) : CompilerMessage(startToken.CurrentLine, startToken.LineNumber, startToken.LinePosition)
{
    /// <inheritdoc />
    public override int MessageNumber => 7;

    /// <inheritdoc />
    public override CompilerMessageType Type => CompilerMessageType.Error;

    /// <inheritdoc />
    public override Task<string> GetDescription() => Task.FromResult($"Namespace expressions can only contain identifiers and the dot operator.");

    /// <inheritdoc />
    public override Task<int> GetMessageSpan() => Task.FromResult(endToken.LinePosition - startToken.LinePosition + endToken.Value.Length);
}
using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Diagnostics.Errors;

/// <summary>
/// Error that is emitted when a namespace statement has an invalid expression body.
/// </summary>
/// <param name="namespaceToken">
/// The <see cref="Token"/> with the namespace keyword.
/// </param>
/// <param name="filePath">
/// The path of the file that the error came from.
/// </param>
public sealed class InvalidNamespaceDeclarationError([NotNull] Token namespaceToken, [NotNull] string filePath) : CompilerMessage(namespaceToken.CurrentLine, namespaceToken.LineNumber, namespaceToken.LinePosition, filePath)
{
    /// <inheritdoc />
    public override CompilerMessageType Type => CompilerMessageType.Error;

    /// <inheritdoc />
    public override Task<string> GetDescription() => Task.FromResult($"Namespace statement must be followed by a valid expression");

    /// <inheritdoc />
    public override Task<int> GetMessageSpan() => Task.FromResult(namespaceToken.Value.Length);
}

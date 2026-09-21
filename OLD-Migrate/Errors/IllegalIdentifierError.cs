using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Diagnostics.Errors;

/// <summary>
/// Error that is emitted when an identifier is an illegal string, i.e. starting with a lowercase character.
/// </summary>
/// <param name="identifierToken">
/// The <see cref="Token"/> with the identifier.
/// </param>
/// <param name="filePath">
/// The path of the file that the error came from.
/// </param>
public sealed class IllegalIdentifierError([NotNull] Token identifierToken, [NotNull] string filePath) : CompilerMessage(identifierToken.CurrentLine, identifierToken.LineNumber, identifierToken.LinePosition, filePath)
{
    /// <inheritdoc />
    public override CompilerMessageType Type => CompilerMessageType.Error;

    /// <inheritdoc />
    public override Task<string> GetDescription() => Task.FromResult($"Identifier must start with an uppercase character");

    /// <inheritdoc />
    public override Task<int> GetMessageSpan() => Task.FromResult(identifierToken.Value.Length);
}

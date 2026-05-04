using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Diagnostics.Errors;

/// <summary>
/// Error that is emitted when a statement isn't terminated with a terminator character.
/// </summary>
/// <param name="statementToken">
/// The <see cref="Token"/> with the statement.
/// </param>
/// <param name="filePath">
/// The path of the file that the error came from.
/// </param>
public sealed class UnterminatedStatementError([NotNull] Token statementToken, [NotNull] string filePath) : CompilerMessage(statementToken.CurrentLine, statementToken.LineNumber, statementToken.LinePosition, filePath)
{
    /// <inheritdoc />
    public override CompilerMessageType Type => CompilerMessageType.Error;

    /// <inheritdoc />
    public override Task<string> GetDescription() => Task.FromResult($"Statement must be terminated with a \";\" character");

    /// <inheritdoc />
    public override Task<int> GetMessageSpan() => Task.FromResult(statementToken.Value.Length);
}


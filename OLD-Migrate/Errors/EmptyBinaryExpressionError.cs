using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Diagnostics.Errors;

/// <summary>
/// Error that is emitted when a binary expression has an empty left or right operand.
/// </summary>
/// <param name="operatorToken">
/// The <see cref="Token"/> with the operator.
/// </param>
/// <param name="side">
/// The side that was empty.
/// </param>
/// <param name="filePath">
/// The path of the file that the error came from.
/// </param>
public sealed class EmptyBinaryExpressionError([NotNull] Token operatorToken, string side, [NotNull] string filePath) : CompilerMessage(operatorToken.CurrentLine, operatorToken.LineNumber, operatorToken.LinePosition, filePath)
{
    /// <inheritdoc />
    public override CompilerMessageType Type => CompilerMessageType.Error;

    /// <inheritdoc />
    public override Task<string> GetDescription() => Task.FromResult($"{side}-hand side of operator \"{operatorToken.Value}\" cannot be empty");

    /// <inheritdoc />
    public override Task<int> GetMessageSpan() => Task.FromResult(operatorToken.Value.Length);
}

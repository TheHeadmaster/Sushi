using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Diagnostics.Errors;

/// <summary>
/// Error that is emitted when the parser expects a prefix token, but finds a non-prefix one in its place.
/// </summary>
/// <param name="token">
/// The <see cref="Token"/> that was actually found.
/// </param>
public sealed class UnexpectedPrefixOperator([NotNull] Token token) : CompilerMessage(token.CurrentLine, token.LineNumber, token.LinePosition)
{
    /// <inheritdoc />
    public override int MessageNumber => 5;

    /// <inheritdoc />
    public override CompilerMessageType Type => CompilerMessageType.Error;

    /// <inheritdoc />
    public override Task<string> GetDescription() => Task.FromResult($"Token \"{token.Value}\" cannot be used as a prefix operator");

    /// <inheritdoc />
    public override Task<int> GetMessageSpan() => Task.FromResult(token.Value.Length);
}
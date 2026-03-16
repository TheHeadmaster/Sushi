using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Diagnostics.Errors;

/// <summary>
/// Error that is emitted when the parser expects a specific token or one of a range of tokens, but finds another one in its place.
/// </summary>
/// <param name="token">
/// The <see cref="Token"/> that was actually found.
/// </param>
/// <param name="expectedType">
/// The <see cref="TokenType"/> values that were allowed in this context.
/// </param>
public sealed class WrongTokenError([NotNull] Token token, [NotNull] TokenType[] expectedTypes) : CompilerMessage(token.CurrentLine, token.LineNumber, token.LinePosition)
{
    /// <inheritdoc />
    public override int MessageNumber => 4;

    /// <inheritdoc />
    public override CompilerMessageType Type => CompilerMessageType.Error;

    /// <inheritdoc />
    public override Task<string> GetDescription()
    {
        if (expectedTypes.Length == 1)
        {
            return Task.FromResult($"Expected {Enum.GetName(expectedTypes[0])}, but found \"{token.Value}\"");
        }

        return Task.FromResult($"Expected one of ({string.Join(',', expectedTypes.Select(x => Enum.GetName(x)))}), but found \"{token.Value}\"");
    }

    /// <inheritdoc />
    public override Task<int> GetMessageSpan() => Task.FromResult(token.Value.Length);
}

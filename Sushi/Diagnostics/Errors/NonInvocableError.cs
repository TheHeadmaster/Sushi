using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sushi.Tokenization;

namespace Sushi.Diagnostics.Errors;

public class NonInvocableError([NotNull]Token startToken, [NotNull] Token endToken) : CompilerMessage(startToken.CurrentLine, startToken.LineNumber, startToken.LinePosition)
{
    /// <inheritdoc />
    public override int MessageNumber => 2;

    /// <inheritdoc />
    public override CompilerMessageType Type => CompilerMessageType.Error;

    public override Task<string> GetDescription() => Task.FromResult($"\"{startToken.Value}\" is non-invocable and cannot be used like a method.");

    /// <inheritdoc />
    public override Task<int> GetMessageSpan() => Task.FromResult(endToken.LinePosition - startToken.LinePosition + endToken.Value.Length);
}

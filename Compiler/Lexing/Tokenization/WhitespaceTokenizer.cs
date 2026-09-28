using Sushi.Source;

namespace Sushi.Lexing.Tokenization;

public sealed class WhitespaceTokenizer : ILexTokenizer
{
    public bool CanStart(byte firstByte) => throw new NotImplementedException();
    public bool TryRecognize(SourceSnapshot snapshot, int position, CancellationToken cancellationToken, out LexTokenMatch match) => throw new NotImplementedException();
}
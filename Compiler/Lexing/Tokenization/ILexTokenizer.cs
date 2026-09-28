using Sushi.Source;

namespace Sushi.Lexing.Tokenization;

public interface ILexTokenizer
{
    public bool CanStart(byte firstByte);

    public bool TryRecognize(SourceSnapshot snapshot, int position, CancellationToken cancellationToken, out LexTokenMatch match)
}
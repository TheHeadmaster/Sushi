using Sushi.Source;

namespace Sushi.Lexing;

public sealed class ProjectLexer : Lexer
{
    public override LexerResult Lex(SourceSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        cancellationToken.ThrowIfCancellationRequested();

        return new LexerResult(snapshot, []);
    }
}
using System.Diagnostics.CodeAnalysis;
using Sushi.Diagnostics;
using Sushi.Source;

namespace Sushi.Lexing;

/// <summary>
/// Performs lexical analysis of text for a specific kind of document, such as a source file or project file.
/// </summary>
public abstract class Lexer
{
    public abstract LexerResult Lex(SourceSnapshot snapshot, CancellationToken cancellationToken);
}
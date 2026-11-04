using System.Text;
using Sushi.Diagnostics;
using Sushi.Lexing;
using Sushi.Lexing.Tokenization;
using Sushi.Source;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Validates rename spellings with the same lexer that recognizes source identifiers.
/// </summary>
internal static class SourceRenameIdentifier
{
    /// <summary>
    /// Determines whether the complete requested spelling is exactly one Sushi identifier token.
    /// </summary>
    /// <param name="spelling">The exact replacement text requested by the client.</param>
    /// <param name="semanticName">The compiler-facing name when validation succeeds.</param>
    /// <param name="cancellationToken">Cancels lexical validation.</param>
    /// <returns>True only for one complete ordinary or escaped identifier token.</returns>
    internal static bool TryValidate(string spelling, out string semanticName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(spelling);
        cancellationToken.ThrowIfCancellationRequested();
        semanticName = string.Empty;
        if (spelling.Length == 0)
        {
            return false;
        }

        SourceSnapshot snapshot = SourceSnapshot.FromText(new Uri("file:///rename-validation.sus"), null, spelling);
        DiagnosticReporter diagnostics = new();
        LexerResult result = new SourceLexer().Lex(snapshot, diagnostics, cancellationToken);
        if (diagnostics.HasDiagnostics() || result.Tokens.Count != 1)
        {
            return false;
        }

        LexToken token = result.Tokens[0];
        if (token.Type is not LexTokenType.Identifier and not LexTokenType.EscapedIdentifier
            || token.Span.Start != 0 || token.Span.End != snapshot.SourceLength)
        {
            return false;
        }

        int nameStart = token.Type is LexTokenType.EscapedIdentifier ? 1 : 0;
        semanticName = Encoding.UTF8.GetString(snapshot.Bytes.Span[nameStart..]);
        return semanticName.Length > 0 && !semanticName.All(character => character == '_');
    }
}

using System.Text;
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.Semantics;

/// <summary>
/// Projects source-backed identifier syntax into its semantic name.
/// </summary>
internal static class IdentifierBinder
{
    public static string? Bind(SyntaxToken token)
    {
        if (!token.IsSourceBacked || token.Type is not SyntaxType.IdentifierToken and not SyntaxType.EscapedIdentifierToken)
        {
            return null;
        }

        SourceSpan span = token.Span;

        int start = token.Type is SyntaxType.EscapedIdentifierToken ? span.Start + 1 : span.Start;

        return Encoding.UTF8.GetString(span.Snapshot.Bytes.Span[start..span.End]);
    }
}
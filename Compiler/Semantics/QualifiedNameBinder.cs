using System.Text;
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.Semantics;

/// <summary>
/// Projects a structurally complete qualified name into its canonical semantic spelling.
/// </summary>
internal static class QualifiedNameBinder
{
    public static string? Bind(QualifiedNameSyntax name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);

        cancellationToken.ThrowIfCancellationRequested();

        if (name.Segments.Any(segment => !segment.IsSourceBacked) || name.Separators.Any(separator => !separator.IsSourceBacked))
        {
            return null;
        }

        List<string> components = [with(name.Segments.Count)];

        foreach (SyntaxToken segment in name.Segments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SourceSpan span = segment.Span;
            int start = segment.Type is SyntaxType.EscapedIdentifierToken ? span.Start + 1 : span.Start;

            components.Add(Encoding.UTF8.GetString(span.Snapshot.Bytes.Span[start..span.End]));
        }

        return string.Join(".", components);
    }
}
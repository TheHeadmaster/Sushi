using Sushi.Parsing.Syntax;

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

            string? component = IdentifierBinder.Bind(segment);

            if (component is null)
            {
                return null;
            }

            components.Add(component);
        }

        return string.Join(".", components);
    }
}
using System.Text;
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.Semantics;

/// <summary>
/// Projects a structurally complete package declaration into its semantic package identity.
/// </summary>
public sealed class PackageDeclarationBinder
{
    /// <summary>
    /// Binds a parsed package declaration when all syntax required to identify the package
    /// originates from source rather than parser recovery.
    /// </summary>
    /// <param name="declaration">
    /// The package declaration to bind.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel binding.
    /// </param>
    /// <returns>
    /// The declared package identity, or null when the declaration contains synthetic missing syntax.
    /// </returns>
    public PackageIdentity? Bind(PackageDeclarationSyntax declaration, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(declaration);

        cancellationToken.ThrowIfCancellationRequested();

        if (!IsStructurallyComplete(declaration))
        {
            return null;
        }

        List<string> components = new(declaration.Name.Segments.Count);

        foreach (SyntaxToken segment in declaration.Name.Segments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            components.Add(GetSemanticIdentifierName(segment));
        }

        return new PackageIdentity(string.Join(".", components));
    }

    private static bool IsStructurallyComplete(PackageDeclarationSyntax declaration)
        => declaration.PackageKeyword.IsSourceBacked
            && declaration.SemicolonToken.IsSourceBacked
            && declaration.Name.Segments.All(segment => segment.IsSourceBacked)
            && declaration.Name.Separators.All(separator => separator.IsSourceBacked);

    private static string GetSemanticIdentifierName(SyntaxToken token)
    {
        SourceSpan span = token.Span;

        int start = token.Type is SyntaxType.EscapedIdentifierToken ? span.Start + 1 : span.Start;

        return Encoding.UTF8.GetString(span.Snapshot.Bytes.Span[start..span.End]);
    }
}
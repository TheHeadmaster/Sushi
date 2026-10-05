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

        if (!declaration.PackageKeyword.IsSourceBacked || !declaration.SemicolonToken.IsSourceBacked)
        {
            return null;
        }

        string? qualifiedName = QualifiedNameBinder.Bind(declaration.Name, cancellationToken);

        return qualifiedName is null ? null : new PackageIdentity(qualifiedName);
    }
}
using Sushi.Parsing.Syntax;

namespace Sushi.Semantics;

/// <summary>
/// Projects a structurally complete namespace declaration into its semantic namespace identity.
/// </summary>
public sealed class NamespaceDeclarationBinder
{
    /// <summary>
    /// Binds a parsed namespace declaration when all syntax required to identify the namespace
    /// originates from source rather than parser recovery.
    /// </summary>
    /// <param name="declaration">
    /// The namespace declaration to bind.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel binding.
    /// </param>
    /// <returns>
    /// The declared namespace identity, or null when the declaration contains synthetic missing syntax.
    /// </returns>
    public NamespaceIdentity? Bind(NamespaceDeclarationSyntax declaration, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(declaration);

        cancellationToken.ThrowIfCancellationRequested();

        if (!declaration.NamespaceKeyword.IsSourceBacked || !declaration.SemicolonToken.IsSourceBacked)
        {
            return null;
        }

        string? qualifiedName = QualifiedNameBinder.Bind(declaration.Name, cancellationToken);

        return qualifiedName is null ? null : new NamespaceIdentity(qualifiedName);
    }
}
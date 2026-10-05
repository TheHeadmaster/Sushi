namespace Sushi.Semantics;

/// <summary>
/// Identifies a Sushi namespace by its canonical semantic qualified name.
/// Syntax-only escaping is not included in the identity.
/// </summary>
public sealed record NamespaceIdentity
{
    /// <summary>
    /// Gets the canonical dot-qualified namespace name.
    /// </summary>
    public string QualifiedName { get; }

    /// <summary>
    /// Creates a namespace identity from its canonical semantic name.
    /// </summary>
    /// <param name="qualifiedName">
    /// The complete dot-qualified namespace name with syntax-only escaping removed.
    /// </param>
    public NamespaceIdentity(string qualifiedName)
    {
        ArgumentException.ThrowIfNullOrEmpty(qualifiedName);

        this.QualifiedName = qualifiedName;
    }

    /// <inheritdoc />
    public override string ToString() => this.QualifiedName;
}
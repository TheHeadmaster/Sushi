namespace Sushi.Semantics;

/// <summary>
/// Identifies a Sushi package by its canonical semantic qualified name.
/// Syntax-only escaping is not included in the identity.
/// </summary>
public sealed record PackageIdentity
{
    /// <summary>
    /// Gets the canonical dot-qualified package name.
    /// </summary>
    public string QualifiedName { get; }

    /// <summary>
    /// Creates a package identity from its canonical semantic name.
    /// </summary>
    /// <param name="qualifiedName">
    /// The complete dot-qualified package name with syntax-only escaping removed.
    /// </param>
    public PackageIdentity(string qualifiedName)
    {
        ArgumentException.ThrowIfNullOrEmpty(qualifiedName);

        this.QualifiedName = qualifiedName;
    }

    /// <inheritdoc />
    public override string ToString() => this.QualifiedName;
}
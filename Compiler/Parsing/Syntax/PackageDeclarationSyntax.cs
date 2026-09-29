using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents a file-scoped Sushi package declaration.
/// </summary>
public sealed class PackageDeclarationSyntax : SyntaxNode
{
    /// <inheritdoc />
    public override SyntaxType Type => SyntaxType.PackageDeclaration;

    /// <inheritdoc />
    public override SourceSpan Span => new(this.PackageKeyword.Span.Snapshot, this.PackageKeyword.Span.Start, this.SemicolonToken.Span.End);

    /// <summary>
    /// Gets the keyword introducing the declaration.
    /// </summary>
    public SyntaxToken PackageKeyword { get; }

    /// <summary>
    /// Gets the declared package name.
    /// </summary>
    public QualifiedNameSyntax Name { get; }

    /// <summary>
    /// Gets the terminating semicolon.
    /// </summary>
    public SyntaxToken SemicolonToken { get; }

    /// <summary>
    /// Creates a package declaration.
    /// </summary>
    /// <param name="packageKeyword">
    /// The package keyword introducing the declaration.
    /// </param>
    /// <param name="name">
    /// The qualified package name.
    /// </param>
    /// <param name="semicolonToken">
    /// The semicolon terminating the declaration.
    /// </param>
    public PackageDeclarationSyntax(SyntaxToken packageKeyword, QualifiedNameSyntax name, SyntaxToken semicolonToken)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (packageKeyword.Type != SyntaxType.PackageKeyword)
        {
            throw new ArgumentException("Package declarations require a package keyword token.", nameof(packageKeyword));
        }

        if (semicolonToken.Type != SyntaxType.SemicolonToken)
        {
            throw new ArgumentException("Package declarations require a semicolon token.", nameof(semicolonToken));
        }

        SourceSnapshot snapshot = packageKeyword.Span.Snapshot;

        if (!ReferenceEquals(name.Span.Snapshot, snapshot) || !ReferenceEquals(semicolonToken.Span.Snapshot, snapshot))
        {
            throw new ArgumentException("All package-declaration syntax must originate from the same source snapshot.");
        }

        this.PackageKeyword = packageKeyword;
        this.Name = name;
        this.SemicolonToken = semicolonToken;
    }
}
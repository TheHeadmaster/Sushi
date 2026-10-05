using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents a file-scoped Sushi namespace declaration.
/// </summary>
public sealed class NamespaceDeclarationSyntax : SyntaxNode
{
    /// <inheritdoc />
    public override SyntaxType Type => SyntaxType.NamespaceDeclaration;

    /// <inheritdoc />
    public override SourceSpan Span => new(this.NamespaceKeyword.Span.Snapshot, this.NamespaceKeyword.Span.Start, this.SemicolonToken.Span.End);

    /// <summary>
    /// Gets the keyword introducing the declaration.
    /// </summary>
    public SyntaxToken NamespaceKeyword { get; }

    /// <summary>
    /// Gets the declared namespace name.
    /// </summary>
    public QualifiedNameSyntax Name { get; }

    /// <summary>
    /// Gets the terminating semicolon.
    /// </summary>
    public SyntaxToken SemicolonToken { get; }

    /// <summary>
    /// Creates a namespace declaration.
    /// </summary>
    /// <param name="namespacekeyword">
    /// The namespace keyword introducing the declaration.
    /// </param>
    /// <param name="name">
    /// The qualified namespace name.
    /// </param>
    /// <param name="semicolonToken">
    /// The semicolon terminating the declaration.
    /// </param>
    public NamespaceDeclarationSyntax(SyntaxToken namespacekeyword, QualifiedNameSyntax name, SyntaxToken semicolonToken)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (namespacekeyword.Type is not SyntaxType.NamespaceKeyword)
        {
            throw new ArgumentException("Namespace declarations require a namespace keyword token.", nameof(namespacekeyword));
        }

        if (semicolonToken.Type is not SyntaxType.SemicolonToken)
        {
            throw new ArgumentException("Namespace declarations require a semicolon token.", nameof(semicolonToken));
        }

        SourceSnapshot snapshot = namespacekeyword.Span.Snapshot;

        if (!ReferenceEquals(name.Span.Snapshot, snapshot) || !ReferenceEquals(semicolonToken.Span.Snapshot, snapshot))
        {
            throw new ArgumentException("All namespace-declaration syntax must originate from the same source snapshot.");
        }

        this.NamespaceKeyword = namespacekeyword;
        this.Name = name;
        this.SemicolonToken = semicolonToken;
    }
}
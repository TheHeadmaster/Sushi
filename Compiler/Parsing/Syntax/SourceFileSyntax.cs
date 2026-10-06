using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents a Sushi source file, including its parsed declarations and any remaining source
/// that the current parser does not yet support.
/// </summary>
public sealed class SourceFileSyntax : SyntaxNode
{
    /// <inheritdoc />
    public override SyntaxType Type => SyntaxType.SourceFile;

    /// <inheritdoc />
    public override SourceSpan Span => new(this.Snapshot, 0, this.Snapshot.SourceLength);

    /// <summary>
    /// Gets the source snapshot represented by this source file.
    /// </summary>
    public SourceSnapshot Snapshot { get; }

    /// <summary>
    /// Gets the leading package declarations recognized by the parser. Valid source contains
    /// exactly one, but additional declarations are retained for recovery and diagnostics.
    /// </summary>
    public IReadOnlyList<PackageDeclarationSyntax> PackageDeclarations { get; }

    /// <summary>
    /// Gets the leading file-scoped namespace declarations recognized by the parser.
    /// Valid source contains exactly one, but additional declarations are retained for diagnostics.
    /// </summary>
    public IReadOnlyList<NamespaceDeclarationSyntax> NamespaceDeclarations { get; }

    /// <summary>
    /// Gets the package-level function declarations recognized by the parser.
    /// </summary>
    public IReadOnlyList<FunctionDeclarationSyntax> FunctionDeclarations { get; }

    /// <summary>
    /// Gets the source range beginning at the first unparsed significant lexical element
    /// and continuing through the end of the file. Null indicates that no significant
    /// lexical elements remain unparsed.
    /// </summary>
    public SourceSpan? UnparsedContentSpan { get; }

    /// <summary>
    /// Creates the root of a partially or completely parsed source file.
    /// </summary>
    /// <param name="snapshot">
    /// The authoritative source snapshot represented by the source file.
    /// </param>
    /// <param name="packageDeclarations">
    /// The leading package declarations recognized by the current parser, including duplicates.
    /// </param>
    /// <param name="namespaceDeclarations">
    /// The leading namespace declarations recognized by the current parser, including duplicates.
    /// </param>
    /// <param name="functionDeclarations">
    /// The package-level function declarations recognized by the current parser.
    /// </param>
    /// <param name="unparsedContentSpan">
    /// The remaining source range, beginning at its first unparsed significant element.
    /// </param>
    public SourceFileSyntax(SourceSnapshot snapshot, IReadOnlyList<PackageDeclarationSyntax> packageDeclarations, IReadOnlyList<NamespaceDeclarationSyntax> namespaceDeclarations, IReadOnlyList<FunctionDeclarationSyntax> functionDeclarations, SourceSpan? unparsedContentSpan)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(packageDeclarations);
        ArgumentNullException.ThrowIfNull(namespaceDeclarations);
        ArgumentNullException.ThrowIfNull(functionDeclarations);

        PackageDeclarationSyntax[] packages = [.. packageDeclarations];
        NamespaceDeclarationSyntax[] namespaces = [.. namespaceDeclarations];
        FunctionDeclarationSyntax[] functions = [.. functionDeclarations];

        if (packages.Any(declaration => !ReferenceEquals(declaration.Span.Snapshot, snapshot)))
        {
            throw new ArgumentException("All package declarations must belong to the source file's source snapshot.", nameof(packageDeclarations));
        }

        if (namespaces.Any(declaration => !ReferenceEquals(declaration.Span.Snapshot, snapshot)))
        {
            throw new ArgumentException("All namespace declarations must belong to the source file's source snapshot.", nameof(namespaceDeclarations));
        }

        if (functions.Any(declaration => !ReferenceEquals(declaration.Span.Snapshot, snapshot)))
        {
            throw new ArgumentException("All function declarations must belong to the source file's source snapshot.", nameof(functionDeclarations));
        }

        for (int i = 1; i < packages.Length; i++)
        {
            if (packages[i - 1].Span.End > packages[i].Span.Start)
            {
                throw new ArgumentException("Package declarations must occur in source order without overlapping.", nameof(packageDeclarations));
            }
        }

        for (int i = 1; i < namespaces.Length; i++)
        {
            if (namespaces[i - 1].Span.End > namespaces[i].Span.Start)
            {
                throw new ArgumentException("Namespace declarations must occur in source order without overlapping.", nameof(namespaceDeclarations));
            }
        }

        for (int i = 1; i < namespaces.Length; i++)
        {
            if (functions[i - 1].Span.End > functions[i].Span.Start)
            {
                throw new ArgumentException("Function declarations must occur in source order without overlapping.", nameof(functionDeclarations));
            }
        }

        if (packages.Length > 0 && namespaces.Length > 0 && packages[^1].Span.End > namespaces[0].Span.Start)
        {
            throw new ArgumentException("Namespace declarations must follow parsed package declarations.", nameof(namespaceDeclarations));
        }

        int headerEnd = namespaces.Length > 0
            ? namespaces[^1].Span.End
            : packages.Length > 0
                ? packages[^1].Span.End
                : 0;

        if (functions.Length > 0 && headerEnd > functions[0].Span.Start)
        {
            throw new ArgumentException("Function declarations must follow parsed file-header declarations.", nameof(functionDeclarations));
        }

        if (unparsedContentSpan is SourceSpan remainder)
        {
            int parsedEnd = functions.Length > 0
                ? functions[^1].Span.End
                : namespaces.Length > 0
                ? namespaces[^1].Span.End
                : packages.Length > 0
                ? packages[^1].Span.End
                : 0;

            if (!ReferenceEquals(remainder.Snapshot, snapshot)
                || remainder.Start < parsedEnd
                || remainder.Start >= remainder.End
                || remainder.End != snapshot.SourceLength)
            {
                throw new ArgumentException("The unparsed source range must follow parsed syntax and end at the source boundary.", nameof(unparsedContentSpan));
            }
        }

        this.Snapshot = snapshot;
        this.PackageDeclarations = packages;
        this.NamespaceDeclarations = namespaces;
        this.FunctionDeclarations = functions;
        this.UnparsedContentSpan = unparsedContentSpan;
    }
}
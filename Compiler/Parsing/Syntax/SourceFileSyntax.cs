
using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents a Sushi source file, including parsed declarations and remaining unsupported source.
/// </summary>
public sealed class SourceFileSyntax : SyntaxNode
{
    /// <inheritdoc />
    public override SyntaxType Type => SyntaxType.SourceFile;

    /// <inheritdoc />
    public override SourceSpan Span => new(this.Snapshot, 0, this.Snapshot.SourceLength);

    /// <summary>
    /// Gets the authoritative source snapshot.
    /// </summary>
    public SourceSnapshot Snapshot { get; }

    /// <summary>
    /// Gets the leading package declarations, including duplicates retained for diagnostics.
    /// </summary>
    public IReadOnlyList<PackageDeclarationSyntax> PackageDeclarations { get; }

    /// <summary>
    /// Gets the leading file-scoped namespace declarations, including duplicates.
    /// </summary>
    public IReadOnlyList<NamespaceDeclarationSyntax> NamespaceDeclarations { get; }

    /// <summary>
    /// Gets package-level function declarations in source order.
    /// </summary>
    public IReadOnlyList<FunctionDeclarationSyntax> FunctionDeclarations { get; }

    /// <summary>
    /// Gets activity declarations in source order.
    /// </summary>
    public IReadOnlyList<ActivityDeclarationSyntax> ActivityDeclarations { get; }

    /// <summary>
    /// Gets all recognized package-level functions and activities in their original source order.
    /// </summary>
    public IReadOnlyList<SyntaxNode> Declarations { get; }

    /// <summary>
    /// Gets the remaining unparsed source range, or null when no significant content remains.
    /// </summary>
    public SourceSpan? UnparsedContentSpan { get; }

    /// <summary>
    /// Creates a source file using the original function-only declaration model.
    /// </summary>
    /// <param name="snapshot">
    /// The authoritative source snapshot.
    /// </param>
    /// <param name="packageDeclarations">
    /// The parsed package declarations.
    /// </param>
    /// <param name="namespaceDeclarations">
    /// The parsed namespace declarations.
    /// </param>
    /// <param name="functionDeclarations">
    /// The parsed package-level functions.
    /// </param>
    /// <param name="unparsedContentSpan">
    /// The unparsed source remainder, if present.
    /// </param>
    public SourceFileSyntax(
        SourceSnapshot snapshot,
        IReadOnlyList<PackageDeclarationSyntax> packageDeclarations,
        IReadOnlyList<NamespaceDeclarationSyntax> namespaceDeclarations,
        IReadOnlyList<FunctionDeclarationSyntax> functionDeclarations,
        SourceSpan? unparsedContentSpan)
        : this(snapshot, packageDeclarations, namespaceDeclarations, functionDeclarations, [], unparsedContentSpan)
    {
    }

    /// <summary>
    /// Creates a source file with separate and source-ordered function and activity declarations.
    /// </summary>
    /// <param name="snapshot">
    /// The authoritative source snapshot.
    /// </param>
    /// <param name="packageDeclarations">
    /// The leading package declarations, including duplicates.
    /// </param>
    /// <param name="namespaceDeclarations">
    /// The leading namespace declarations, including duplicates.
    /// </param>
    /// <param name="functionDeclarations">
    /// The package-level functions.
    /// </param>
    /// <param name="activityDeclarations">
    /// The activity declarations.
    /// </param>
    /// <param name="unparsedContentSpan">
    /// The source remainder beginning at the first unparsed significant element.
    /// </param>
    public SourceFileSyntax(
        SourceSnapshot snapshot,
        IReadOnlyList<PackageDeclarationSyntax> packageDeclarations,
        IReadOnlyList<NamespaceDeclarationSyntax> namespaceDeclarations,
        IReadOnlyList<FunctionDeclarationSyntax> functionDeclarations,
        IReadOnlyList<ActivityDeclarationSyntax> activityDeclarations,
        SourceSpan? unparsedContentSpan)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(packageDeclarations);
        ArgumentNullException.ThrowIfNull(namespaceDeclarations);
        ArgumentNullException.ThrowIfNull(functionDeclarations);
        ArgumentNullException.ThrowIfNull(activityDeclarations);

        PackageDeclarationSyntax[] packages = [.. packageDeclarations];
        NamespaceDeclarationSyntax[] namespaces = [.. namespaceDeclarations];
        FunctionDeclarationSyntax[] functions = [.. functionDeclarations];
        ActivityDeclarationSyntax[] activities = [.. activityDeclarations];

        ValidateDeclarations(snapshot, packages, nameof(packageDeclarations));
        ValidateDeclarations(snapshot, namespaces, nameof(namespaceDeclarations));
        ValidateDeclarations(snapshot, functions, nameof(functionDeclarations));
        ValidateDeclarations(snapshot, activities, nameof(activityDeclarations));

        if (packages.Length > 0 && namespaces.Length > 0 && packages[^1].Span.End > namespaces[0].Span.Start)
        {
            throw new ArgumentException("Namespace declarations must follow package declarations.", nameof(namespaceDeclarations));
        }

        SyntaxNode[] declarations = [.. functions, .. activities];
        Array.Sort(declarations, static (left, right) => left.Span.Start.CompareTo(right.Span.Start));

        int headerEnd = namespaces.Length > 0
            ? namespaces[^1].Span.End
            : packages.Length > 0
                ? packages[^1].Span.End
                : 0;

        int previousEnd = headerEnd;

        foreach (SyntaxNode declaration in declarations)
        {
            if (declaration.Span.Start < previousEnd)
            {
                throw new ArgumentException("Package-level declarations must follow the file header without overlapping.");
            }

            previousEnd = declaration.Span.End;
        }

        if (unparsedContentSpan is SourceSpan remainder)
        {
            if (!ReferenceEquals(remainder.Snapshot, snapshot)
                || remainder.Start < previousEnd
                || remainder.Start >= remainder.End
                || remainder.End != snapshot.SourceLength)
            {
                throw new ArgumentException("The unparsed source range must follow parsed declarations and end at the source boundary.", nameof(unparsedContentSpan));
            }
        }

        this.Snapshot = snapshot;
        this.PackageDeclarations = packages;
        this.NamespaceDeclarations = namespaces;
        this.FunctionDeclarations = functions;
        this.ActivityDeclarations = activities;
        this.Declarations = declarations;
        this.UnparsedContentSpan = unparsedContentSpan;
    }

    private static void ValidateDeclarations<T>(SourceSnapshot snapshot, IReadOnlyList<T> declarations, string parameterName) where T : SyntaxNode
    {
        int previousEnd = 0;

        foreach (T declaration in declarations)
        {
            if (declaration is null || !ReferenceEquals(declaration.Span.Snapshot, snapshot) || declaration.Span.Start < previousEnd)
            {
                throw new ArgumentException("Declarations must belong to the source snapshot and occur in order without overlapping.", parameterName);
            }

            previousEnd = declaration.Span.End;
        }
    }
}

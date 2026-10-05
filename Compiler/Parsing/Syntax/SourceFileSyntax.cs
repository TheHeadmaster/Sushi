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
    /// <param name="unparsedContentSpan">
    /// The remaining source range, beginning at its first unparsed significant element.
    /// </param>
    public SourceFileSyntax(SourceSnapshot snapshot, IReadOnlyList<PackageDeclarationSyntax> packageDeclarations, SourceSpan? unparsedContentSpan)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(packageDeclarations);

        PackageDeclarationSyntax[] declarations = [.. packageDeclarations];

        if (declarations.Any(declaration => !ReferenceEquals(declaration.Span.Snapshot, snapshot)))
        {
            throw new ArgumentException("All package declarations must belong to the source file's source snapshot.", nameof(packageDeclarations));
        }

        for (int i = 1; i < declarations.Length; i++)
        {
            if (declarations[i - 1].Span.End > declarations[i].Span.Start)
            {
                throw new ArgumentException("Package declarations must occur in source order without overlapping.", nameof(packageDeclarations));
            }
        }

        if (unparsedContentSpan is SourceSpan remainder)
        {
            int parsedEnd = declarations.Length > 0 ? declarations[^1].Span.End : 0;

            if (!ReferenceEquals(remainder.Snapshot, snapshot)
                || remainder.Start < parsedEnd
                || remainder.Start >= remainder.End
                || remainder.End != snapshot.SourceLength)
            {
                throw new ArgumentException("The unparsed source range must follow parsed syntax and end at the source boundary.", nameof(unparsedContentSpan));
            }
        }

        this.Snapshot = snapshot;
        this.PackageDeclarations = declarations;
        this.UnparsedContentSpan = unparsedContentSpan;
    }
}
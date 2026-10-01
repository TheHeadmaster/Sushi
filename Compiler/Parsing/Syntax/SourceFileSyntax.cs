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
    /// Gets the parsed package declaration, or null when the current parser did
    /// not recognize a package declaration at the beginning of the file.
    /// </summary>
    public PackageDeclarationSyntax? PackageDeclaration { get; }

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
    /// <param name="packageDeclaration">
    /// The package declaration recognized by the current parser, if present.
    /// </param>
    /// <param name="unparsedContentSpan">
    /// The remaining source range, beginning at its first unparsed significant element.
    /// </param>
    public SourceFileSyntax(SourceSnapshot snapshot, PackageDeclarationSyntax? packageDeclaration, SourceSpan? unparsedContentSpan)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (packageDeclaration is not null && !ReferenceEquals(packageDeclaration.Span.Snapshot, snapshot))
        {
            throw new ArgumentException("The package declaration must belong to the source file's source snapshot.", nameof(packageDeclaration));
        }

        if (unparsedContentSpan is SourceSpan remainder)
        {
            int parsedEnd = packageDeclaration?.Span.End ?? 0;

            if (!ReferenceEquals(remainder.Snapshot, snapshot)
                || remainder.Start < parsedEnd
                || remainder.Start >= remainder.End
                || remainder.End != snapshot.SourceLength)
            {
                throw new ArgumentException("The unparsed source range must follow parsed syntax and end at the source boundary.", nameof(unparsedContentSpan));
            }
        }

        this.Snapshot = snapshot;
        this.PackageDeclaration = packageDeclaration;
        this.UnparsedContentSpan = unparsedContentSpan;
    }
}
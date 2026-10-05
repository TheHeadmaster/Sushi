using Sushi.Diagnostics;
using Sushi.Parsing.Syntax;

namespace Sushi.Semantics;

/// <summary>
/// Binds file-level semantic information and validates invariants that span complete declarations.
/// </summary>
public sealed class SourceFileBinder
{
    // Placeholders until diagnostic numbering is settled.
    private const string MissingPackageDeclarationCode = "SUSE008";

    private const string DuplicatePackageDeclarationCode = "SUSE009";

    private readonly PackageDeclarationBinder packageDeclarationBinder = new();

    /// <summary>
    /// Binds semantic information from a parsed Sushi source file.
    /// </summary>
    /// <param name="sourceFile">
    /// The source-file syntax to bind.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel binding.
    /// </param>
    /// <returns>
    /// The bound package identity when ambiguous, together with file-level semantic diagnostics.
    /// </returns>
    public SourceFileBindResult Bind(SourceFileSyntax sourceFile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceFile);

        cancellationToken.ThrowIfCancellationRequested();

        if (sourceFile.PackageDeclarations.Count == 0)
        {
            int position = sourceFile.UnparsedContentSpan?.Start ?? sourceFile.Snapshot.SourceLength;

            SushiDiagnostic diagnostic = new(
                MissingPackageDeclarationCode,
                "A Sushi source file must declare exactly one package.",
                DiagnosticSeverity.Error,
                new Source.SourceSpan(sourceFile.Snapshot, position, position));

            return new SourceFileBindResult(null, [diagnostic]);
        }

        if (sourceFile.PackageDeclarations.Count > 1)
        {
            List<SushiDiagnostic> diagnostics = [];

            for (int i = 1; i < sourceFile.PackageDeclarations.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                PackageDeclarationSyntax duplicate = sourceFile.PackageDeclarations[i];

                diagnostics.Add(new SushiDiagnostic(
                    DuplicatePackageDeclarationCode,
                    "A Sushi source file may declare only one package.",
                    DiagnosticSeverity.Error,
                    duplicate.PackageKeyword.Span));
            }
       
            return new SourceFileBindResult(null, diagnostics);
        }

        PackageIdentity? package = this.packageDeclarationBinder.Bind(sourceFile.PackageDeclarations[0], cancellationToken);

        return new SourceFileBindResult(package, []);
    }
}
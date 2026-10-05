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

    private const string MissingNamespaceDeclarationCode = "SUSE010";

    private const string DuplicateNamespaceDeclarationCode = "SUSE011";

    private readonly PackageDeclarationBinder packageDeclarationBinder = new();

    private readonly NamespaceDeclarationBinder namespaceDeclarationBinder = new();

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
    /// The bound package identity when unambiguous, together with file-level semantic diagnostics.
    /// </returns>
    public SourceFileBindResult Bind(SourceFileSyntax sourceFile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceFile);

        cancellationToken.ThrowIfCancellationRequested();

        List<SushiDiagnostic> diagnostics = [];

        PackageIdentity? package = this.BindPackage(sourceFile, diagnostics, cancellationToken);
        NamespaceIdentity? @namespace = this.BindNamespace(sourceFile, diagnostics, cancellationToken);

        return new SourceFileBindResult(package, @namespace, diagnostics);
    }

    private PackageIdentity? BindPackage(SourceFileSyntax sourceFile, List<SushiDiagnostic> diagnostics, CancellationToken cancellationToken)
    {      
        if (sourceFile.PackageDeclarations.Count == 0)
        {
            int position = sourceFile.NamespaceDeclarations.Count > 0
                ? sourceFile.NamespaceDeclarations[0].Span.Start
                : sourceFile.UnparsedContentSpan?.Start ?? sourceFile.Snapshot.SourceLength;

            diagnostics.Add(new SushiDiagnostic(
                MissingPackageDeclarationCode,
                "A Sushi source file must declare exactly one package.",
                DiagnosticSeverity.Error,
                new Source.SourceSpan(sourceFile.Snapshot, position, position)));

            return null;
        }

        if (sourceFile.PackageDeclarations.Count > 1)
        {
            for (int i = 1; i < sourceFile.PackageDeclarations.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                diagnostics.Add(new SushiDiagnostic(
                    DuplicatePackageDeclarationCode,
                    "A Sushi source file may declare only one package.",
                    DiagnosticSeverity.Error,
                    sourceFile.PackageDeclarations[i].PackageKeyword.Span));
            }
       
            return null;
        }

        return this.packageDeclarationBinder.Bind(sourceFile.PackageDeclarations[0], cancellationToken);
    }

    private NamespaceIdentity? BindNamespace(SourceFileSyntax sourceFile, List<SushiDiagnostic> diagnostics, CancellationToken cancellationToken)
    {
        if (sourceFile.NamespaceDeclarations.Count == 0)
        {
            int position = sourceFile.UnparsedContentSpan?.Start ?? sourceFile.Snapshot.SourceLength;

            diagnostics.Add(new SushiDiagnostic(
                MissingNamespaceDeclarationCode,
                "A Sushi source file must declare exactly one namespace.",
                DiagnosticSeverity.Error,
                new Source.SourceSpan(sourceFile.Snapshot, position, position)));

            return null;
        }

        if (sourceFile.NamespaceDeclarations.Count > 1)
        {
            for (int i = 1; i < sourceFile.NamespaceDeclarations.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                diagnostics.Add(new SushiDiagnostic(
                    DuplicateNamespaceDeclarationCode,
                    "A Sushi source file may declare only one namespace.",
                    DiagnosticSeverity.Error,
                    sourceFile.NamespaceDeclarations[i].NamespaceKeyword.Span));
            }
       
            return null;
        }

        return this.namespaceDeclarationBinder.Bind(sourceFile.NamespaceDeclarations[0], cancellationToken);
    }
}
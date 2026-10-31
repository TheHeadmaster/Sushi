
using Sushi.Diagnostics;
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.Semantics;

/// <summary>
/// Binds file-level semantic information and validates invariants that span complete declarations.
/// </summary>
public sealed class SourceFileBinder
{
    /// <summary>
    /// Binds semantic information from a parsed Sushi source file.
    /// </summary>
    /// <param name="sourceFile">
    /// The source-file syntax to bind.
    /// </param>
    /// <param name="diagnosticReporter">
    /// The diagnostic reporter used to accumulate diagnostics.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel binding.
    /// </param>
    /// <returns>
    /// The bound package, namespace, functions, and valid activity declarations.
    /// </returns>
    public static SourceFileBindResult Bind(SourceFileSyntax sourceFile, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceFile);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);
        cancellationToken.ThrowIfCancellationRequested();

        IDiagnosticReporter localDiagnostics = new DiagnosticReporter();

        PackageIdentity? package = BindPackage(sourceFile, localDiagnostics, cancellationToken);
        NamespaceIdentity? @namespace = BindNamespace(sourceFile, localDiagnostics, cancellationToken);

        List<BoundFunction> functions = [];
        List<BoundActivity> activities = [];

        foreach (FunctionDeclarationSyntax declaration in sourceFile.FunctionDeclarations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            FunctionBindResult result = FunctionDeclarationBinder.Bind(declaration, localDiagnostics, cancellationToken);

            if (result.Function is not null)
            {
                functions.Add(result.Function);
            }
        }

        foreach (ActivityDeclarationSyntax declaration in sourceFile.ActivityDeclarations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            BoundActivity? activity = ActivityDeclarationBinder.Bind(declaration, localDiagnostics, cancellationToken);

            if (activity is not null)
            {
                activities.Add(activity);
            }
        }

        diagnosticReporter.CommitReporter(localDiagnostics);

        return new SourceFileBindResult(package, @namespace, functions)
        {
            Activities = activities
        };
    }

    private static PackageIdentity? BindPackage(SourceFileSyntax sourceFile, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        if (sourceFile.PackageDeclarations.Count == 0)
        {
            int position = sourceFile.NamespaceDeclarations.Count > 0
                ? sourceFile.NamespaceDeclarations[0].Span.Start
                : GetFirstDeclarationPosition(sourceFile);

            diagnosticReporter.GenerateError(ErrorType.MissingPackageDeclaration, new SourceSpan(sourceFile.Snapshot, position, position));
            return null;
        }

        if (sourceFile.PackageDeclarations.Count > 1)
        {
            for (int i = 1; i < sourceFile.PackageDeclarations.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                diagnosticReporter.GenerateError(ErrorType.DuplicatePackageDeclaration, sourceFile.PackageDeclarations[i].PackageKeyword.Span);
            }

            return null;
        }

        return PackageDeclarationBinder.Bind(sourceFile.PackageDeclarations[0], cancellationToken);
    }

    private static NamespaceIdentity? BindNamespace(SourceFileSyntax sourceFile, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        if (sourceFile.NamespaceDeclarations.Count == 0)
        {
            int position = GetFirstDeclarationPosition(sourceFile);
            diagnosticReporter.GenerateError(ErrorType.MissingNamespaceDeclaration, new SourceSpan(sourceFile.Snapshot, position, position));
            return null;
        }

        if (sourceFile.NamespaceDeclarations.Count > 1)
        {
            for (int i = 1; i < sourceFile.NamespaceDeclarations.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                diagnosticReporter.GenerateError(ErrorType.DuplicateNamespaceDeclaration, sourceFile.NamespaceDeclarations[i].NamespaceKeyword.Span);
            }

            return null;
        }

        return NamespaceDeclarationBinder.Bind(sourceFile.NamespaceDeclarations[0], cancellationToken);
    }

    private static int GetFirstDeclarationPosition(SourceFileSyntax sourceFile)
        => sourceFile.Declarations.Count > 0
            ? sourceFile.Declarations[0].Span.Start
            : sourceFile.UnparsedContentSpan?.Start ?? sourceFile.Snapshot.SourceLength;
}
using Sushi.Analysis;
using Sushi.Diagnostics;
using Sushi.Parsing.Syntax;
using Sushi.Documentation;
using Sushi.Semantics;

namespace Sushi.Compilation;

/// <summary>
/// Shares signature collection, symbol assignment, and visibility without binding function bodies.
/// </summary>
internal static class ProjectCallableCatalog
{
    /// <summary>
    /// Pairs ordered declarations with their signatures and deterministic compiler-generated symbols.
    /// </summary>
    internal static IReadOnlyList<ProjectCallable> Collect(
        IReadOnlyList<SourceAnalysisResult> sources,
        IDiagnosticReporter diagnosticReporter,
        CancellationToken cancellationToken)
    {
        List<ProjectCallable> catalog = [];

        foreach (SourceAnalysisResult source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SourceFileSyntax syntax = (SourceFileSyntax)source.SyntaxTree.Root;

            if (source.Functions.Count != syntax.FunctionDeclarations.Count)
            {
                diagnosticReporter.GenerateErrorWithCustomMessage(
                    ErrorType.UnsupportedMVPExpression,
                    "A project function has a signature unsupported by the current executable subset.",
                    syntax.Span);

                continue;
            }

            Dictionary<SyntaxNode, DocumentationBlock> documentationByDeclaration = new(ReferenceEqualityComparer.Instance);
            foreach (DocumentationBlock block in source.DocumentationBlocks)
            {
                if (block.Declaration is SyntaxNode declaration)
                {
                    documentationByDeclaration.Add(declaration, block);
                }
            }

            for (int i = 0; i < syntax.FunctionDeclarations.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                BoundFunction signature = source.Functions[i];
                FunctionDeclarationSyntax declaration = syntax.FunctionDeclarations[i];
                string symbol = $"__sushi_package_function_{catalog.Count}";

                documentationByDeclaration.TryGetValue(declaration, out DocumentationBlock? documentation);
                PackageCallable callable = new(signature, symbol) { Declaration = declaration, Documentation = documentation };
                catalog.Add(new ProjectCallable(source, declaration, callable));
            }
        }

        return catalog;
    }

    /// <summary>
    /// Selects signatures using the executable compiler's package, namespace, and accessibility rules.
    /// </summary>
    internal static IReadOnlyList<PackageCallable> SelectVisible(SourceAnalysisResult source, IReadOnlyList<ProjectCallable> catalog, CancellationToken cancellationToken)
    {
        List<PackageCallable> visible = [];
        foreach (ProjectCallable item in catalog)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (BelongsToSameScope(item.Source, source)
                && item.Callable.Function.Accessibility is not (Accessibility.Private or Accessibility.Protected))
            {
                visible.Add(item.Callable);
            }
        }

        return visible;
    }

    /// <summary>
    /// Tests the established package and namespace boundary shared by compilation consumers.
    /// </summary>
    internal static bool BelongsToSameScope(SourceAnalysisResult left, SourceAnalysisResult right)
        => left.Package is not null
            && right.Package is not null
            && left.Namespace is not null
            && right.Namespace is not null
            && StringComparer.Ordinal.Equals(left.Package.QualifiedName, right.Package.QualifiedName)
            && StringComparer.Ordinal.Equals(left.Namespace.QualifiedName, right.Namespace.QualifiedName);
}

/// <summary>
/// Connects a compiler-owned callable identity to its exact defining source declaration.
/// </summary>
internal sealed record ProjectCallable(SourceAnalysisResult Source, FunctionDeclarationSyntax Declaration, PackageCallable Callable);

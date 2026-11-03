using System.Globalization;
using System.Text;
using Sushi.Compilation;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.Inspection;

/// <summary>
/// Records the semantic inputs that must remain equivalent before a compiled function can be reused.
/// </summary>
internal sealed class CompilerInspectionDependencies
{
    private readonly string projectPath;
    private readonly string projectConfigurationPath;
    private readonly byte[] projectConfiguration;
    private readonly string[] sourcePaths;
    private readonly string selectedSourcePath;
    private readonly string selectedSymbol;
    private readonly string selectedPackage;
    private readonly string selectedNamespace;
    private readonly string selectedContract;
    private readonly byte[] selectedDeclaration;
    private readonly IReadOnlyList<CallDependency> calls;

    private CompilerInspectionDependencies(string projectPath, SourceSnapshot projectSnapshot, IReadOnlyList<SourceSnapshot> sources,
        ProjectCallable selected, IReadOnlyList<PackageCallable> visibleCallables, IReadOnlyList<string> callSymbols)
    {
        this.projectPath = CompilerInspectionCacheKey.NormalizePath(projectPath);
        this.projectConfigurationPath = CompilerInspectionCacheKey.NormalizePath(projectSnapshot.Uri.LocalPath);
        this.projectConfiguration = projectSnapshot.Bytes.ToArray();
        this.sourcePaths = [.. sources.Select(source => CompilerInspectionCacheKey.NormalizePath(source.Uri.LocalPath))];
        this.selectedSourcePath = CompilerInspectionCacheKey.NormalizePath(selected.Declaration.Span.Snapshot.Uri.LocalPath);
        this.selectedSymbol = selected.Callable.Symbol;
        this.selectedPackage = selected.Source.Package?.QualifiedName ?? string.Empty;
        this.selectedNamespace = selected.Source.Namespace?.QualifiedName ?? string.Empty;
        this.selectedContract = DescribeCallable(selected.Callable, this.selectedPackage, this.selectedNamespace);
        this.selectedDeclaration = selected.Declaration.Span.Snapshot.Bytes.Span[
            selected.Declaration.Span.Start..selected.Declaration.Span.End].ToArray();
        this.calls = this.CollectCallDependencies(callSymbols, visibleCallables);
    }

    /// <summary>
    /// Creates a certificate only when every bound call can be mapped back to its visible catalog entry.
    /// </summary>
    /// <param name="projectPath">
    /// The normalized project identity for the inspected function.
    /// </param>
    /// <param name="projectSnapshot">
    /// The configuration snapshot used to construct the callable catalog.
    /// </param>
    /// <param name="sources">
    /// The discovered authoritative source snapshots in compiler order.
    /// </param>
    /// <param name="selected">
    /// The selected declaration and its compiler-generated identity.
    /// </param>
    /// <param name="visibleCallables">
    /// The catalog entries visible to the selected function under compiler rules.
    /// </param>
    /// <param name="boundFunction">
    /// The successfully bound body whose calls establish the certificate's dependencies.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels traversal when the bound function contains many nested expressions.
    /// </param>
    /// <returns>
    /// A dependency certificate, or null when the bound tree contains an unsupported node.
    /// </returns>
    internal static CompilerInspectionDependencies? TryCreate(string projectPath, SourceSnapshot projectSnapshot,
        IReadOnlyList<SourceSnapshot> sources, ProjectCallable selected, IReadOnlyList<PackageCallable> visibleCallables,
        BoundFunction boundFunction, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectPath);
        ArgumentNullException.ThrowIfNull(projectSnapshot);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentNullException.ThrowIfNull(visibleCallables);
        ArgumentNullException.ThrowIfNull(boundFunction);

        if (!BoundCallSymbolCollector.TryCollect(boundFunction.Body, cancellationToken, out IReadOnlyList<string> symbols))
        {
            return null;
        }

        HashSet<string> visibleSymbols = new(visibleCallables.Select(callable => callable.Symbol), StringComparer.Ordinal);
        return symbols.All(visibleSymbols.Contains)
            ? new CompilerInspectionDependencies(projectPath, projectSnapshot, sources, selected, visibleCallables, symbols)
            : null;
    }

    /// <summary>
    /// Checks the current project declarations against the original function and its recorded call candidates.
    /// </summary>
    /// <param name="projectPath">
    /// The current resolved project path.
    /// </param>
    /// <param name="projectSnapshot">
    /// The current authoritative project configuration.
    /// </param>
    /// <param name="sources">
    /// The current discovered source snapshots in compiler order.
    /// </param>
    /// <param name="selected">
    /// The selected declaration from the current callable catalog.
    /// </param>
    /// <param name="visibleCallables">
    /// The current compiler-visible callables for that declaration.
    /// </param>
    /// <returns>
    /// True only when every captured semantic dependency still has the same identity and contract.
    /// </returns>
    internal bool IsCompatibleWith(string projectPath, SourceSnapshot projectSnapshot, IReadOnlyList<SourceSnapshot> sources,
        ProjectCallable selected, IReadOnlyList<PackageCallable> visibleCallables)
    {
        if (!StringComparer.Ordinal.Equals(this.projectPath, CompilerInspectionCacheKey.NormalizePath(projectPath))
            || !StringComparer.Ordinal.Equals(this.projectConfigurationPath, CompilerInspectionCacheKey.NormalizePath(projectSnapshot.Uri.LocalPath))
            || !this.projectConfiguration.AsSpan().SequenceEqual(projectSnapshot.Bytes.Span)
            || !SequenceEqual(this.sourcePaths, sources.Select(source => CompilerInspectionCacheKey.NormalizePath(source.Uri.LocalPath)))
            || !StringComparer.Ordinal.Equals(this.selectedSourcePath, CompilerInspectionCacheKey.NormalizePath(selected.Declaration.Span.Snapshot.Uri.LocalPath))
            || !StringComparer.Ordinal.Equals(this.selectedSymbol, selected.Callable.Symbol)
            || !StringComparer.Ordinal.Equals(this.selectedPackage, selected.Source.Package?.QualifiedName ?? string.Empty)
            || !StringComparer.Ordinal.Equals(this.selectedNamespace, selected.Source.Namespace?.QualifiedName ?? string.Empty)
            || !StringComparer.Ordinal.Equals(this.selectedContract, DescribeCallable(selected.Callable, this.selectedPackage, this.selectedNamespace))
            || !this.selectedDeclaration.AsSpan().SequenceEqual(selected.Declaration.Span.Snapshot.Bytes.Span[
                selected.Declaration.Span.Start..selected.Declaration.Span.End]))
        {
            return false;
        }

        foreach (CallDependency dependency in this.calls)
        {
            PackageCallable[] currentCandidates = [.. visibleCallables.Where(callable => StringComparer.Ordinal.Equals(callable.Name, dependency.Name))];
            if (currentCandidates.Length != dependency.Candidates.Length)
            {
                return false;
            }

            for (int i = 0; i < currentCandidates.Length; i++)
            {
                string descriptor = DescribeCallable(currentCandidates[i], this.selectedPackage, this.selectedNamespace);
                if (!StringComparer.Ordinal.Equals(dependency.Candidates[i], descriptor))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static string DescribeCallable(PackageCallable callable, string package, string @namespace)
    {
        StringBuilder descriptor = new();
        AppendField(descriptor, callable.Symbol);
        AppendField(descriptor, callable.Name);
        AppendField(descriptor, ((int)callable.Function.Accessibility).ToString(CultureInfo.InvariantCulture));
        AppendField(descriptor, ((int)callable.Function.ReturnType).ToString(CultureInfo.InvariantCulture));
        AppendField(descriptor, package);
        AppendField(descriptor, @namespace);
        AppendField(descriptor, callable.Function.Parameters.Count.ToString(CultureInfo.InvariantCulture));
        foreach (BoundParameter parameter in callable.Function.Parameters)
        {
            AppendField(descriptor, parameter.Name);
            AppendField(descriptor, ((int)parameter.Type).ToString(CultureInfo.InvariantCulture));
        }

        return descriptor.ToString();
    }

    private static void AppendField(StringBuilder builder, string value)
    {
        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        builder.Append(':');
        builder.Append(value);
    }

    private static bool SequenceEqual(IReadOnlyList<string> left, IEnumerable<string> right)
        => left.SequenceEqual(right, StringComparer.Ordinal);

    private IReadOnlyList<CallDependency> CollectCallDependencies(IReadOnlyList<string> callSymbols,
        IReadOnlyList<PackageCallable> visibleCallables)
    {
        Dictionary<string, PackageCallable> callablesBySymbol = visibleCallables.ToDictionary(callable => callable.Symbol, StringComparer.Ordinal);
        HashSet<string> seenNames = new(StringComparer.Ordinal);
        List<CallDependency> dependencies = [];

        foreach (string symbol in callSymbols)
        {
            if (!callablesBySymbol.TryGetValue(symbol, out PackageCallable? called))
            {
                throw new InvalidOperationException("A bound call does not map to a compiler-visible callable.");
            }

            if (!seenNames.Add(called.Name))
            {
                continue;
            }

            string[] candidates = [.. visibleCallables
                .Where(callable => StringComparer.Ordinal.Equals(callable.Name, called.Name))
                .Select(callable => DescribeCallable(callable, this.selectedPackage, this.selectedNamespace))];
            dependencies.Add(new CallDependency(called.Name, candidates));
        }

        return Array.AsReadOnly(dependencies.ToArray());
    }

    private sealed record CallDependency(string Name, string[] Candidates);

    private static class BoundCallSymbolCollector
    {
        internal static bool TryCollect(BoundBlock body, CancellationToken cancellationToken, out IReadOnlyList<string> symbols)
        {
            List<string> collected = [];
            if (!VisitBlock(body, collected, cancellationToken))
            {
                symbols = Array.Empty<string>();
                return false;
            }

            symbols = Array.AsReadOnly(collected.ToArray());
            return true;
        }

        private static bool VisitBlock(BoundBlock block, List<string> symbols, CancellationToken cancellationToken)
        {
            foreach (BoundStatement statement in block.Statements)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool supported = statement switch
                {
                    BoundVoidCallStatement call => VisitCall(call.Symbol, call.Arguments, symbols, cancellationToken),
                    BoundLocalDeclarationStatement local => VisitExpression(local.Initializer, symbols, cancellationToken),
                    BoundLocalAssignmentStatement assignment => VisitExpression(assignment.Value, symbols, cancellationToken),
                    BoundIntegerUpdateStatement => true,
                    BoundReturnStatement returned => returned.Expression is null || VisitExpression(returned.Expression, symbols, cancellationToken),
                    BoundIfStatement conditional => VisitExpression(conditional.Condition, symbols, cancellationToken)
                        && VisitBlock(conditional.Then, symbols, cancellationToken)
                        && (conditional.Else is null || VisitBlock(conditional.Else, symbols, cancellationToken)),
                    BoundWhileStatement loop => VisitExpression(loop.Condition, symbols, cancellationToken)
                        && VisitBlock(loop.Body, symbols, cancellationToken),
                    BoundDoWhileStatement loop => VisitBlock(loop.Body, symbols, cancellationToken)
                        && VisitExpression(loop.Condition, symbols, cancellationToken),
                    BoundForStatement loop => VisitBlock(loop.Initialization, symbols, cancellationToken)
                        && VisitExpression(loop.Condition, symbols, cancellationToken)
                        && VisitBlock(loop.Iteration, symbols, cancellationToken)
                        && VisitBlock(loop.Body, symbols, cancellationToken),
                    BoundLoopTransferStatement => true,
                    _ => false
                };

                if (!supported)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool VisitExpression(BoundExpression expression, List<string> symbols, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return expression switch
            {
                BoundCallExpression call => VisitCall(call.Symbol, call.Arguments, symbols, cancellationToken),
                BoundBooleanBinaryExpression binary => VisitExpression(binary.Left, symbols, cancellationToken)
                    && VisitExpression(binary.Right, symbols, cancellationToken),
                BoundBooleanNotExpression not => VisitExpression(not.Operand, symbols, cancellationToken),
                BoundIntegerBinaryExpression binary => VisitExpression(binary.Left, symbols, cancellationToken)
                    && VisitExpression(binary.Right, symbols, cancellationToken),
                BoundIntegerComparisonExpression comparison => VisitExpression(comparison.Left, symbols, cancellationToken)
                    && VisitExpression(comparison.Right, symbols, cancellationToken),
                BoundIntegerRangeComparisonExpression comparison => VisitExpression(comparison.Operand, symbols, cancellationToken),
                BoundIntegerUnaryExpression unary => VisitExpression(unary.Operand, symbols, cancellationToken),
                BoundIntegerUpdateExpression => true,
                BoundBooleanConstantExpression or BoundIntegerLiteralExpression or BoundParameterReferenceExpression or BoundLocalReferenceExpression => true,
                _ => false
            };
        }

        private static bool VisitCall(string symbol, IReadOnlyList<BoundCallArgument> arguments, List<string> symbols,
            CancellationToken cancellationToken)
        {
            symbols.Add(symbol);
            foreach (BoundCallArgument argument in arguments)
            {
                if (!VisitExpression(argument.Expression, symbols, cancellationToken))
                {
                    return false;
                }
            }

            return true;
        }
    }
}

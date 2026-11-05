using Sushi.Analysis;
using Sushi.Diagnostics;
using Sushi.Documentation;
using Sushi.Lexing;
using Sushi.Lexing.Tokenization;
using Sushi.Parsing.Syntax;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.Compilation;

/// <summary>
/// Performs source diagnostics with the callable visibility of each applicable project.
/// </summary>
internal sealed class ProjectDiagnosticAnalyzer
{
    private readonly SourceAnalyzer sourceAnalyzer = new();

    /// <summary>
    /// Analyzes current workspace snapshots without invoking native code generation.
    /// </summary>
    /// <param name="snapshots">
    /// The authoritative snapshots currently held by the workspace.
    /// </param>
    /// <param name="affectedUris">
    /// Documents whose changes require dependent project contexts to be reanalyzed, or null for a full workspace pass.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels declaration analysis and function-body binding.
    /// </param>
    /// <returns>
    /// Results and diagnostics kept separate for each originating document snapshot.
    /// </returns>
    internal async Task<IReadOnlyList<ProjectDiagnosticResult>> AnalyzeAsync(
        IReadOnlyList<SourceSnapshot> snapshots,
        IReadOnlyList<Uri>? affectedUris,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        cancellationToken.ThrowIfCancellationRequested();

        List<SourceSnapshot> sourceSnapshots = [.. snapshots.Where(snapshot => IsExtension(snapshot, ".sus"))];
        List<ProjectDiagnosticResult> results = [];
        List<ProjectContext> projects = [];
        List<string> invalidProjectDirectories = [];
        bool analyzeEntireWorkspace = affectedUris is null || affectedUris.Any(uri =>
            Path.GetExtension(uri.LocalPath).Equals(".susproj", StringComparison.OrdinalIgnoreCase)
            || Path.GetExtension(uri.LocalPath).Equals(".susln", StringComparison.OrdinalIgnoreCase)
            || (Path.GetExtension(uri.LocalPath).Equals(".sus", StringComparison.OrdinalIgnoreCase) && !File.Exists(uri.LocalPath)));

        foreach (SourceSnapshot snapshot in snapshots.Where(item => IsExtension(item, ".susproj")))
        {
            cancellationToken.ThrowIfCancellationRequested();
            DiagnosticReporter reporter = new();
            ProjectAnalysisResult result = (ProjectAnalysisResult)await ProjectAnalyzer.Analyze(snapshot, reporter, cancellationToken);
            if (analyzeEntireWorkspace || affectedUris!.Any(uri => uri.IsSamePath(snapshot.Uri)))
            {
                results.Add(new ProjectDiagnosticResult(result, reporter));
            }

            if (result.Project is null)
            {
                string? invalidDirectory = Path.GetDirectoryName(snapshot.Uri.LocalPath);
                if (invalidDirectory is not null)
                {
                    invalidProjectDirectories.Add(invalidDirectory);
                }

                continue;
            }

            string projectDirectory = Path.GetDirectoryName(snapshot.Uri.LocalPath)
                ?? throw new InvalidOperationException($"Project \"{snapshot.Uri.LocalPath}\" has no containing directory.");
            IReadOnlyList<string> paths = ProjectSourceLoader.DiscoverSourcePaths(projectDirectory, result.Project, cancellationToken);
            List<SourceSnapshot> members = [];
            foreach (string path in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Uri sourceUri = new(path);
                SourceSnapshot? source = sourceSnapshots.FirstOrDefault(item => item.Uri.IsSamePath(sourceUri));
                if (source is null)
                {
                    source = await ProjectSourceLoader.LoadSnapshot(path, cancellationToken);
                    sourceSnapshots.Add(source);
                }

                members.Add(source);
            }

            if (members.Count > 0)
            {
                projects.Add(new ProjectContext(projectDirectory, snapshot.Uri, Array.AsReadOnly(members.ToArray())));
            }
        }

        ProjectContext[] selectedProjects = [.. projects.Where(project => analyzeEntireWorkspace
            || affectedUris!.Any(uri => project.Members.Any(member => member.Uri.IsSamePath(uri))
                || IsWithinDirectory(uri, project.Directory)))];
        HashSet<SourceSnapshot> selectedSnapshots = new(ReferenceEqualityComparer.Instance);
        if (analyzeEntireWorkspace)
        {
            selectedSnapshots.UnionWith(sourceSnapshots);
        }
        else
        {
            foreach (ProjectContext project in selectedProjects)
            {
                selectedSnapshots.UnionWith(project.Members);
            }

            foreach (SourceSnapshot source in sourceSnapshots.Where(snapshot => IsExtension(snapshot, ".sus")
                && affectedUris!.Any(uri => uri.IsSamePath(snapshot.Uri))))
            {
                selectedSnapshots.Add(source);
            }
        }

        List<SourceState> sources = [];
        foreach (SourceSnapshot snapshot in selectedSnapshots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DiagnosticReporter reporter = new();
            SourceAnalysisResult result = (SourceAnalysisResult)await this.sourceAnalyzer.AnalyzeDeclarations(snapshot, reporter, cancellationToken);
            sources.Add(new SourceState(result, reporter));
        }

        Dictionary<SourceSnapshot, SourceState> sourceStates = new(ReferenceEqualityComparer.Instance);
        foreach (SourceState source in sources)
        {
            sourceStates.Add(source.Result.Snapshot, source);
        }

        foreach (ProjectContext project in selectedProjects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SourceState[] members = [.. project.Members.Select(snapshot => sourceStates[snapshot])];
            if (members.Any(source => source.Reporter.HasErrors()))
            {
                continue;
            }

            IDiagnosticReporter catalogDiagnostics = new DiagnosticReporter();
            IReadOnlyList<ProjectCallable> catalog = ProjectCallableCatalog.Collect(
                [.. members.Select(source => source.Result)], catalogDiagnostics, cancellationToken);
            if (catalogDiagnostics.HasErrors())
            {
                RouteCatalogDiagnostics(members, catalogDiagnostics.ReportDiagnostics());
                continue;
            }

            foreach (SourceState source in members)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (CountContainingProjects(source, projects) != 1 || IsWithinInvalidProject(source.Result.Snapshot.Uri, invalidProjectDirectories))
                {
                    continue;
                }

                BindBodies(source, ProjectCallableCatalog.SelectVisible(source.Result, catalog, cancellationToken), catalog, project.ProjectUri, cancellationToken);
            }
        }

        foreach (SourceState source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int membershipCount = CountContainingProjects(source, projects);
            if (membershipCount != 0 || source.Reporter.HasErrors()
                || IsWithinInvalidProject(source.Result.Snapshot.Uri, invalidProjectDirectories))
            {
                continue;
            }

            IDiagnosticReporter catalogDiagnostics = new DiagnosticReporter();
            IReadOnlyList<ProjectCallable> catalog = ProjectCallableCatalog.Collect([source.Result], catalogDiagnostics, cancellationToken);
            if (catalogDiagnostics.HasErrors())
            {
                source.Reporter.CommitReporter(catalogDiagnostics);
                continue;
            }

            BindBodies(source, ProjectCallableCatalog.SelectVisible(source.Result, catalog, cancellationToken), catalog, null, cancellationToken);
        }

        foreach (SourceState source in sources)
        {
            results.Add(new ProjectDiagnosticResult(source.Result, source.Reporter));
        }

        return results;
    }

    private static void BindBodies(SourceState source, IReadOnlyList<PackageCallable> callables, IReadOnlyList<ProjectCallable> catalog, Uri? projectUri, CancellationToken cancellationToken)
    {
        SourceFileSyntax syntax = (SourceFileSyntax)source.Result.SyntaxTree.Root;
        List<BoundFunction> functions = [];
        List<SourceSemanticOccurrence> occurrences = [];
        List<BoundParameterHint> parameterHints = [];
        IReadOnlyList<BoundFunction> signatures = source.Result.Functions;
        Dictionary<SyntaxNode, DocumentationBlock> documentationByDeclaration = new(ReferenceEqualityComparer.Instance);
        foreach (DocumentationBlock block in source.Result.DocumentationBlocks)
        {
            if (block.Declaration is SyntaxNode declaration)
            {
                documentationByDeclaration.Add(declaration, block);
            }
        }
        for (int index = 0; index < syntax.FunctionDeclarations.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FunctionDeclarationSyntax declaration = syntax.FunctionDeclarations[index];
            documentationByDeclaration.TryGetValue(declaration, out DocumentationBlock? documentation);
            BoundFunction signature = signatures[index];
            AddFunctionDeclarationOccurrences(declaration, signature, documentation, occurrences);
            FunctionBindResult result = FunctionDeclarationBinder.Bind(declaration, source.Reporter, callables, cancellationToken, documentation, captureSourceSemanticOccurrences: true);
            if (result.Function is not null)
            {
                functions.Add(result.Function);
                occurrences.AddRange(result.SemanticOccurrences);
                parameterHints.AddRange(result.SemanticParameterHints);
            }
        }

        foreach (BoundActivity activity in source.Result.Activities)
        {
            documentationByDeclaration.TryGetValue(activity.Declaration, out DocumentationBlock? activityDocumentation);
            SourceSemanticSymbol activitySymbol = new(activity.Declaration, SourceSemanticSymbolKind.Activity, 0,
                activity.Name, null, null, activityDocumentation, null, activity.Accessibility,
                DefinitionSpan: activity.Declaration.Name.Span);
            occurrences.Add(new SourceSemanticOccurrence(activity.Declaration.Name.Span, activitySymbol, IsDeclaration: true));

            foreach (ActivityMethodDeclarationSyntax methodDeclaration in activity.Declaration.Members.OfType<ActivityMethodDeclarationSyntax>())
            {
                BoundActivityMethod? method = methodDeclaration == activity.Entry.Declaration
                    ? activity.Entry
                    : methodDeclaration == activity.Stop?.Declaration ? activity.Stop : null;
                if (method is null)
                {
                    continue;
                }

                documentationByDeclaration.TryGetValue(methodDeclaration, out DocumentationBlock? methodDocumentation);
                BoundFunction? signature = CreateActivityMethodSignature(method, cancellationToken);
                SourceSemanticSymbol methodSymbol = new(methodDeclaration, SourceSemanticSymbolKind.ActivityMethod, 0,
                    method.Name, signature?.ReturnType, signature, methodDocumentation, activity.Name, method.Accessibility,
                    DefinitionSpan: methodDeclaration.Name.Span);
                occurrences.Add(new SourceSemanticOccurrence(methodDeclaration.Name.Span, methodSymbol, IsDeclaration: true));
                if (signature is not null)
                {
                    AddActivityMethodSignatureOccurrences(methodDeclaration, signature, methodDocumentation, method.Name, occurrences);
                }
            }
        }

        AddDocumentationReferenceOccurrences(source.Result, callables, catalog, occurrences, source.Reporter, cancellationToken);

        occurrences.Sort((left, right) => left.Span.Start.CompareTo(right.Span.Start));
        parameterHints.Sort((left, right) => left.ArgumentSpan.Start.CompareTo(right.ArgumentSpan.Start));
        source.Result = source.Result with
        {
            Functions = functions,
            SemanticOccurrences = Array.AsReadOnly(occurrences.ToArray()),
            ParameterHints = Array.AsReadOnly(parameterHints.ToArray()),
            SemanticProjectUri = projectUri
        };
    }



    internal static void AnalyzeDocumentationReferences(
        SourceAnalysisResult source,
        IReadOnlyList<ProjectCallable> catalog,
        IDiagnosticReporter reporter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(reporter);
        List<SourceSemanticOccurrence> occurrences = [];
        SourceFileSyntax syntax = (SourceFileSyntax)source.SyntaxTree.Root;
        Dictionary<SyntaxNode, DocumentationBlock> documentationByDeclaration = new(ReferenceEqualityComparer.Instance);
        foreach (DocumentationBlock block in source.DocumentationBlocks)
        {
            if (block.Declaration is SyntaxNode declaration)
            {
                documentationByDeclaration.Add(declaration, block);
            }
        }

        for (int index = 0; index < syntax.FunctionDeclarations.Count && index < source.Functions.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FunctionDeclarationSyntax declaration = syntax.FunctionDeclarations[index];
            documentationByDeclaration.TryGetValue(declaration, out DocumentationBlock? documentation);
            AddFunctionDeclarationOccurrences(declaration, source.Functions[index], documentation, occurrences);
        }

        foreach (BoundActivity activity in source.Activities)
        {
            cancellationToken.ThrowIfCancellationRequested();
            documentationByDeclaration.TryGetValue(activity.Declaration, out DocumentationBlock? documentation);
            occurrences.Add(new SourceSemanticOccurrence(activity.Declaration.Name.Span,
                new SourceSemanticSymbol(activity.Declaration, SourceSemanticSymbolKind.Activity, 0, activity.Name,
                    null, null, documentation, null, activity.Accessibility, DefinitionSpan: activity.Declaration.Name.Span),
                IsDeclaration: true));

            foreach (ActivityMethodDeclarationSyntax methodDeclaration in activity.Declaration.Members.OfType<ActivityMethodDeclarationSyntax>())
            {
                BoundActivityMethod? method = methodDeclaration == activity.Entry.Declaration
                    ? activity.Entry
                    : methodDeclaration == activity.Stop?.Declaration ? activity.Stop : null;
                if (method is null)
                {
                    continue;
                }

                documentationByDeclaration.TryGetValue(methodDeclaration, out DocumentationBlock? methodDocumentation);
                BoundFunction? signature = CreateActivityMethodSignature(method, cancellationToken);
                occurrences.Add(new SourceSemanticOccurrence(methodDeclaration.Name.Span,
                    new SourceSemanticSymbol(methodDeclaration, SourceSemanticSymbolKind.ActivityMethod, 0, method.Name,
                        signature?.ReturnType, signature, methodDocumentation, activity.Name, method.Accessibility,
                        DefinitionSpan: methodDeclaration.Name.Span),
                    IsDeclaration: true));
                if (signature is not null)
                {
                    AddActivityMethodSignatureOccurrences(methodDeclaration, signature, methodDocumentation, method.Name, occurrences);
                }
            }
        }

        AddDocumentationReferenceOccurrences(source, ProjectCallableCatalog.SelectVisible(source, catalog, cancellationToken),
            catalog, occurrences, reporter, cancellationToken);
    }

    private static void AddDocumentationReferenceOccurrences(
        SourceAnalysisResult source,
        IReadOnlyList<PackageCallable> visibleCallables,
        IReadOnlyList<ProjectCallable> catalog,
        ICollection<SourceSemanticOccurrence> occurrences,
        IDiagnosticReporter reporter,
        CancellationToken cancellationToken)
    {
        foreach (DocumentationBlock block in source.DocumentationBlocks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (block.Declaration is not SyntaxNode declaration)
            {
                continue;
            }

            foreach (DocumentationReference reference in block.Content.InlineReferences)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SourceSpan diagnosticSpan = reference.TargetNameSpan ?? reference.Span;
                if (!reference.IsWellFormed || reference.TargetNameSpelling is null
                    || !TryBindDocumentationIdentifier(reference.TargetNameSpelling, cancellationToken, out SyntaxToken targetToken))
                {
                    reporter.GenerateWarning(reference.Kind is DocumentationReferenceKind.Parameter
                        ? WarningType.UnknownDocumentationReferenceParameter
                        : WarningType.UnresolvedDocumentationReference, diagnosticSpan);
                    continue;
                }

                string? targetName = IdentifierBinder.Bind(targetToken);
                if (targetName is null
                    || (reference.Kind is DocumentationReferenceKind.Link && !HasLexicallyValidQualifiedTarget(reference, cancellationToken)))
                {
                    reporter.GenerateWarning(reference.Kind is DocumentationReferenceKind.Parameter
                        ? WarningType.UnknownDocumentationReferenceParameter
                        : WarningType.UnresolvedDocumentationReference, diagnosticSpan);
                    continue;
                }

                if (reference.Kind is DocumentationReferenceKind.Parameter)
                {
                    SourceSemanticOccurrence? parameter = occurrences.FirstOrDefault(item =>
                        item.IsDeclaration
                        && item.Symbol.Kind is SourceSemanticSymbolKind.Parameter
                        && ReferenceEquals(item.Symbol.Declaration, declaration)
                        && StringComparer.Ordinal.Equals(item.Symbol.Name, targetName));
                    if (parameter is null)
                    {
                        reporter.GenerateWarning(WarningType.UnknownDocumentationReferenceParameter, diagnosticSpan);
                        continue;
                    }

                    occurrences.Add(new SourceSemanticOccurrence(diagnosticSpan, parameter.Symbol));
                    continue;
                }

                List<ProjectCallable> sameName = [.. catalog.Where(item =>
                    StringComparer.Ordinal.Equals(item.Callable.Function.Name, targetName))];
                List<ProjectCallable> qualifiedCandidates = reference.TargetQualifier is string qualifier
                    ? [.. sameName.Where(item => QualifierMatches(item.Source, qualifier, source, cancellationToken))]
                    : [.. sameName.Where(item => ProjectCallableCatalog.BelongsToSameScope(item.Source, source))];
                List<ProjectCallable> signatureCandidates = [.. qualifiedCandidates.Where(item =>
                    SignatureMatches(item.Callable.Function, reference, cancellationToken))];

                if (signatureCandidates.Count == 0)
                {
                    List<SourceSemanticOccurrence> localCandidates = [.. occurrences.Where(item =>
                        item.IsDeclaration
                        && item.Symbol.Kind is SourceSemanticSymbolKind.Activity or SourceSemanticSymbolKind.ActivityMethod
                        && StringComparer.Ordinal.Equals(item.Symbol.Name, targetName)
                        && (reference.TargetQualifier is not string localQualifier || QualifierMatches(source, localQualifier, cancellationToken))
                        && SignatureMatches(item.Symbol.Callable, reference, cancellationToken))];
                    if (localCandidates.Count == 1)
                    {
                        if (IsAccessible(localCandidates[0].Symbol.Accessibility))
                        {
                            occurrences.Add(new SourceSemanticOccurrence(diagnosticSpan, localCandidates[0].Symbol));
                        }
                        else
                        {
                            reporter.GenerateWarning(WarningType.InaccessibleDocumentationReference, diagnosticSpan);
                        }

                        continue;
                    }

                    if (localCandidates.Count > 1)
                    {
                        reporter.GenerateWarning(WarningType.AmbiguousDocumentationReference, diagnosticSpan);
                        continue;
                    }

                    bool localInaccessible = occurrences.Any(item =>
                        item.IsDeclaration
                        && item.Symbol.Kind is SourceSemanticSymbolKind.Activity or SourceSemanticSymbolKind.ActivityMethod
                        && StringComparer.Ordinal.Equals(item.Symbol.Name, targetName)
                        && (reference.TargetQualifier is not string localQualifier || QualifierMatches(source, localQualifier, cancellationToken))
                        && SignatureMatches(item.Symbol.Callable, reference, cancellationToken)
                        && !IsAccessible(item.Symbol.Accessibility));
                    if (localInaccessible)
                    {
                        reporter.GenerateWarning(WarningType.InaccessibleDocumentationReference, diagnosticSpan);
                        continue;
                    }
                }

                if (signatureCandidates.Count == 0)
                {
                    reporter.GenerateWarning(WarningType.UnresolvedDocumentationReference, diagnosticSpan);
                    continue;
                }

                List<ProjectCallable> accessibleCandidates = [.. signatureCandidates.Where(item =>
                    IsAccessibleDocumentationTarget(source, item.Source, item.Callable.Function.Accessibility)
                    && (reference.TargetQualifier is not null || visibleCallables.Contains(item.Callable)))];
                if (accessibleCandidates.Count == 0)
                {
                    reporter.GenerateWarning(WarningType.InaccessibleDocumentationReference, diagnosticSpan);
                    continue;
                }

                if (accessibleCandidates.Count != 1)
                {
                    reporter.GenerateWarning(WarningType.AmbiguousDocumentationReference, diagnosticSpan);
                    continue;
                }

                ProjectCallable selected = accessibleCandidates[0];
                SourceSemanticSymbol symbol = new(
                    selected.Declaration,
                    SourceSemanticSymbolKind.Function,
                    0,
                    selected.Callable.Function.Name,
                    selected.Callable.Function.ReturnType,
                    selected.Callable.Function,
                    selected.Callable.Documentation,
                    null,
                    CompilerSymbol: selected.Callable.Symbol,
                    DefinitionSpan: selected.Declaration.Name.Span);
                occurrences.Add(new SourceSemanticOccurrence(diagnosticSpan, symbol));
            }
        }
    }

    private static bool TryBindDocumentationIdentifier(string spelling, CancellationToken cancellationToken, out SyntaxToken token)
    {
        if (spelling.Contains('\uFEFF', StringComparison.Ordinal))
        {
            token = default;
            return false;
        }

        SourceSnapshot snapshot = SourceSnapshot.FromText(new Uri("file:///SushiDocumentationReference.sus"), 0, spelling);
        LexerResult lexed = new SourceLexer().Lex(snapshot, new DiagnosticReporter(), cancellationToken);
        LexToken[] identifiers = [.. lexed.Tokens.Where(item =>
            item.Type is LexTokenType.Identifier or LexTokenType.EscapedIdentifier)];
        if (identifiers.Length != 1 || identifiers[0].Span.Start != 0 || identifiers[0].Span.End != snapshot.SourceLength
            || lexed.Tokens.Any(item => item.Type is LexTokenType.Unknown))
        {
            token = default;
            return false;
        }

        token = new SyntaxToken(
            identifiers[0].Type is LexTokenType.EscapedIdentifier ? SyntaxType.EscapedIdentifierToken : SyntaxType.IdentifierToken,
            identifiers[0]);
        return true;
    }


    private static bool HasLexicallyValidQualifiedTarget(DocumentationReference reference, CancellationToken cancellationToken)
    {
        string? writtenTarget = reference.WrittenTarget;
        if (writtenTarget is null)
        {
            return false;
        }

        int signatureStart = writtenTarget.IndexOf('(', StringComparison.Ordinal);
        string qualifiedName = signatureStart < 0 ? writtenTarget : writtenTarget[..signatureStart];
        string[] components = qualifiedName.Split('.');
        if (components.Length == 0 || components.Any(string.IsNullOrWhiteSpace)
            || !StringComparer.Ordinal.Equals(components[^1], reference.TargetNameSpelling))
        {
            return false;
        }

        foreach (string component in components)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryBindDocumentationIdentifier(component, cancellationToken, out _))
            {
                return false;
            }
        }

        return true;
    }

    private static bool QualifierMatches(SourceAnalysisResult source, string qualifier, CancellationToken cancellationToken)
        => QualifierMatches(source, qualifier, source, cancellationToken);

    private static bool QualifierMatches(SourceAnalysisResult target, string qualifier, SourceAnalysisResult requester, CancellationToken cancellationToken)
    {
        string[] components = qualifier.Split('.');
        if (components.Length == 0 || components.Any(string.IsNullOrWhiteSpace))
        {
            return false;
        }

        List<string> names = [];
        foreach (string component in components)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryBindDocumentationIdentifier(component, cancellationToken, out SyntaxToken token))
            {
                return false;
            }

            names.Add(IdentifierBinder.Bind(token)!);
        }

        string writtenQualifier = string.Join(".", names);
        string fullName = string.Join(".", new[] { target.Package?.QualifiedName, target.Namespace?.QualifiedName }
            .Where(item => !string.IsNullOrWhiteSpace(item)));
        bool samePackage = StringComparer.Ordinal.Equals(target.Package?.QualifiedName, requester.Package?.QualifiedName);
        return StringComparer.Ordinal.Equals(writtenQualifier, fullName)
            || (samePackage && StringComparer.Ordinal.Equals(writtenQualifier, target.Namespace?.QualifiedName));
    }

    private static bool IsAccessibleDocumentationTarget(
        SourceAnalysisResult requester,
        SourceAnalysisResult target,
        Accessibility accessibility)
        => accessibility switch
        {
            Accessibility.Public or Accessibility.Internal => true,
            Accessibility.Package => StringComparer.Ordinal.Equals(
                requester.Package?.QualifiedName, target.Package?.QualifiedName),
            Accessibility.Private or Accessibility.Protected => false,
            _ => false
        };

    private static bool SignatureMatches(BoundFunction? callable, DocumentationReference reference, CancellationToken cancellationToken)
    {
        if (!reference.HasExplicitSignature)
        {
            return true;
        }

        IReadOnlyList<string> parameterTypes = reference.ParameterTypeSpellings;
        if (callable is null || callable.Parameters.Count != parameterTypes.Count)
        {
            return false;
        }

        for (int index = 0; index < callable.Parameters.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BoundType? expected = parameterTypes[index] switch
            {
                "int32" => BoundType.Int32,
                "bool" => BoundType.Bool,
                _ => null
            };
            if (expected is null || callable.Parameters[index].Type != expected)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAccessible(Accessibility? accessibility)
        => accessibility is not (Accessibility.Private or Accessibility.Protected);

    private static BoundFunction? CreateActivityMethodSignature(BoundActivityMethod method, CancellationToken cancellationToken)
    {
        ActivityMethodDeclarationSyntax declaration = method.Declaration;
        if (!FunctionDeclarationBinder.TryBindReturnType(declaration.ReturnType, out BoundType returnType))
        {
            return null;
        }

        List<BoundParameter> parameters = [];
        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (ParameterSyntax parameter in declaration.Parameters)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? name = IdentifierBinder.Bind(parameter.NameToken);
            if (!FunctionDeclarationBinder.TryBindReturnType(parameter.TypeToken, out BoundType type)
                || type is BoundType.Void || name is null || !names.Add(name))
            {
                return null;
            }

            parameters.Add(new BoundParameter(parameters.Count, name, type));
        }

        return new BoundFunction(method.Accessibility, method.Name, returnType, parameters, new BoundBlock([]));
    }

    private static void AddActivityMethodSignatureOccurrences(
        ActivityMethodDeclarationSyntax declaration,
        BoundFunction signature,
        DocumentationBlock? documentation,
        string containingName,
        ICollection<SourceSemanticOccurrence> occurrences)
    {
        for (int index = 0; index < signature.Parameters.Count && index < declaration.Parameters.Count; index++)
        {
            BoundParameter parameter = signature.Parameters[index];
            SourceSemanticSymbol parameterSymbol = new(declaration, SourceSemanticSymbolKind.Parameter, parameter.Index,
                parameter.Name, parameter.Type, signature, documentation, containingName,
                DefinitionSpan: declaration.Parameters[index].NameToken.Span);
            occurrences.Add(new SourceSemanticOccurrence(declaration.Parameters[index].NameToken.Span, parameterSymbol, IsDeclaration: true));
            AddTypeOccurrence(declaration.Parameters[index].TypeToken, declaration, signature, occurrences);
        }

        AddTypeOccurrence(declaration.ReturnType, declaration, signature, occurrences);
    }

    private static void AddFunctionDeclarationOccurrences(
        FunctionDeclarationSyntax declaration,
        BoundFunction function,
        DocumentationBlock? documentation,
        ICollection<SourceSemanticOccurrence> occurrences)
    {
        SourceSemanticSymbol functionSymbol = new(declaration, SourceSemanticSymbolKind.Function, 0, function.Name,
            function.ReturnType, function, documentation, null, DefinitionSpan: declaration.Name.Span);
        occurrences.Add(new SourceSemanticOccurrence(declaration.Name.Span, functionSymbol, IsDeclaration: true));

        for (int index = 0; index < function.Parameters.Count && index < declaration.Parameters.Count; index++)
        {
            BoundParameter parameter = function.Parameters[index];
            SourceSemanticSymbol parameterSymbol = new(declaration, SourceSemanticSymbolKind.Parameter, parameter.Index,
                parameter.Name, parameter.Type, function, documentation, function.Name,
                DefinitionSpan: declaration.Parameters[index].NameToken.Span);
            occurrences.Add(new SourceSemanticOccurrence(declaration.Parameters[index].NameToken.Span, parameterSymbol, IsDeclaration: true));
            AddTypeOccurrence(declaration.Parameters[index].TypeToken, declaration, function, occurrences);
        }

        AddTypeOccurrence(declaration.ReturnType, declaration, function, occurrences);
    }

    private static void AddTypeOccurrence(
        SyntaxToken token,
        SyntaxNode declaration,
        BoundFunction function,
        ICollection<SourceSemanticOccurrence> occurrences)
    {
        if (token.IsSourceBacked && FunctionDeclarationBinder.TryBindReturnType(token, out BoundType type))
        {
            string name = type switch
            {
                BoundType.Int32 => "int32",
                BoundType.Bool => "bool",
                BoundType.Void => "void",
                _ => throw new ArgumentOutOfRangeException(nameof(token))
            };
            occurrences.Add(new SourceSemanticOccurrence(token.Span,
                new SourceSemanticSymbol(declaration, SourceSemanticSymbolKind.Type, 0, name, type, function, null, null)));
        }
    }

    private static int CountContainingProjects(SourceState source, IReadOnlyList<ProjectContext> projects)
        => projects.Count(project => project.Members.Any(member => ReferenceEquals(member, source.Result.Snapshot)));

    private static void RouteCatalogDiagnostics(IReadOnlyList<SourceState> sources, IReadOnlyList<SushiDiagnostic> diagnostics)
    {
        foreach (SushiDiagnostic diagnostic in diagnostics)
        {
            SourceState? source = sources.FirstOrDefault(item => ReferenceEquals(item.Result.Snapshot, diagnostic.Span.Snapshot));
            if (source is not null && diagnostic.Severity is DiagnosticSeverity.Error
                && Enum.IsDefined((ErrorType)diagnostic.Code))
            {
                source.Reporter.GenerateErrorWithCustomMessage((ErrorType)diagnostic.Code, diagnostic.Message, diagnostic.Span);
            }
        }
    }

    private static bool IsWithinInvalidProject(Uri sourceUri, IReadOnlyList<string> invalidProjectDirectories)
    {
        foreach (string directory in invalidProjectDirectories)
        {
            string relative = Path.GetRelativePath(directory, sourceUri.LocalPath);
            if (!Path.IsPathRooted(relative) && relative != ".."
                && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsExtension(SourceSnapshot snapshot, string extension)
        => Path.GetExtension(snapshot.Uri.LocalPath).Equals(extension, StringComparison.OrdinalIgnoreCase);

    private sealed class SourceState(SourceAnalysisResult result, DiagnosticReporter reporter)
    {
        internal SourceAnalysisResult Result { get; set; } = result;
        internal DiagnosticReporter Reporter { get; } = reporter;
    }

    private static bool IsWithinDirectory(Uri uri, string directory)
    {
        string relative = Path.GetRelativePath(directory, uri.LocalPath);
        return !Path.IsPathRooted(relative) && relative != ".."
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private sealed record ProjectContext(string Directory, Uri ProjectUri, IReadOnlyList<SourceSnapshot> Members);
}

/// <summary>
/// Keeps one document's analysis result paired with diagnostics from that exact snapshot.
/// </summary>
/// <param name="Analysis">
/// The syntax and semantic analysis for the document.
/// </param>
/// <param name="Reporter">
/// Diagnostics whose spans belong to the analysis snapshot.
/// </param>
internal sealed record ProjectDiagnosticResult(AnalysisResult Analysis, IDiagnosticReporter Reporter);
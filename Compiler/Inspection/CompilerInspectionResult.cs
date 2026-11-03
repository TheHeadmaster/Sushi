using System.Collections.ObjectModel;
using Sushi.Diagnostics;
using Sushi.Source;

namespace Sushi.Inspection;

/// <summary>
/// Contains one function representation or source diagnostics, together with its stable analysis inputs.
/// </summary>
public sealed class CompilerInspectionResult
{
    /// <summary>
    /// Gets the completed representation, or null when source errors prevented inspection.
    /// </summary>
    public InspectionDocument? Document { get; }

    /// <summary>
    /// Gets whether inspection produced a complete representation.
    /// </summary>
    public bool Succeeded => this.Document is not null;

    /// <summary>
    /// Gets the requested representation, including when inspection failed.
    /// </summary>
    public InspectionStage Stage { get; }

    /// <summary>
    /// Gets the selected function's compiler symbol on success.
    /// </summary>
    public string? FunctionSymbol => this.Document?.FunctionSymbol;

    /// <summary>
    /// Gets the semantic source name for display without deriving identity from formatted text.
    /// </summary>
    public string? FunctionName { get; }

    /// <summary>
    /// Gets the selected declaration and its authoritative source snapshot when selection succeeded.
    /// </summary>
    public SourceSpan? Declaration { get; }

    /// <summary>
    /// Gets the project configuration, source snapshots, and callable definition map.
    /// </summary>
    public CompilerInspectionContext Context { get; }

    /// <summary>
    /// Gets diagnostics with spans in their originating project or source snapshots.
    /// </summary>
    public IReadOnlyList<SushiDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Gets the selected function's local declaration tokens from the source snapshot owned by this result.
    /// </summary>
    public IReadOnlyDictionary<int, SourceSpan> LocalDefinitions { get; }

    /// <summary>
    /// Creates a result after analysis, retaining no partial generated document.
    /// </summary>
    internal CompilerInspectionResult(InspectionStage stage, CompilerInspectionContext context,
        IReadOnlyList<SushiDiagnostic> diagnostics, SourceSpan? declaration, InspectionDocument? document, string? functionName = null,
        IReadOnlyDictionary<int, SourceSpan>? localDefinitions = null)
    {
        this.Stage = stage;
        this.Context = context;
        this.Diagnostics = Array.AsReadOnly<SushiDiagnostic>([.. diagnostics]);
        this.Declaration = declaration;
        this.Document = document;
        this.FunctionName = functionName;
        Dictionary<int, SourceSpan> definitions = document is null || localDefinitions is null
            ? []
            : localDefinitions.ToDictionary(item => item.Key, item => item.Value);
        if (document is not null && (declaration is not SourceSpan selectedDeclaration
            || !context.Functions.TryGetValue(document.FunctionSymbol, out SourceSpan retainedDeclaration)
            || !ReferenceEquals(retainedDeclaration.Snapshot, selectedDeclaration.Snapshot)
            || retainedDeclaration.Start != selectedDeclaration.Start || retainedDeclaration.End != selectedDeclaration.End
            || !InspectionLocalDefinitions.TryCapture(definitions, selectedDeclaration, document, document.FunctionSymbol, out _)))
        {
            throw new ArgumentException("Local source definitions must match the selected function's generated local identities.", nameof(localDefinitions));
        }

        this.LocalDefinitions = new ReadOnlyDictionary<int, SourceSpan>(definitions);
    }
}

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
    /// Creates a result after analysis, retaining no partial generated document.
    /// </summary>
    internal CompilerInspectionResult(InspectionStage stage, CompilerInspectionContext context,
        IReadOnlyList<SushiDiagnostic> diagnostics, SourceSpan? declaration, InspectionDocument? document)
    {
        this.Stage = stage;
        this.Context = context;
        this.Diagnostics = Array.AsReadOnly<SushiDiagnostic>([.. diagnostics]);
        this.Declaration = declaration;
        this.Document = document;
    }
}
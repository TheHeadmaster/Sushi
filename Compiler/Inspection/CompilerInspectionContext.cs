using System.Collections.ObjectModel;
using Sushi.Source;

namespace Sushi.Inspection;

/// <summary>
/// Retains the exact source and configuration inputs of one inspection without a mutable global registry.
/// Snapshot objects, rather than version numbers alone, preserve authoritative content identity.
/// </summary>
public sealed class CompilerInspectionContext
{
    /// <summary>
    /// Gets the resolved project file identity.
    /// </summary>
    public string ProjectPath { get; }

    /// <summary>
    /// Gets the authoritative project configuration bytes used by this inspection.
    /// </summary>
    public SourceSnapshot ProjectSnapshot { get; }

    /// <summary>
    /// Gets the analyzed source snapshots in deterministic project discovery order.
    /// </summary>
    public IReadOnlyList<SourceSnapshot> Sources { get; }

    /// <summary>
    /// Gets compiler-generated callable symbols mapped to their defining UTF-8 declaration spans.
    /// Referenced function representations are generated only on a future explicit request.
    /// </summary>
    public IReadOnlyDictionary<string, SourceSpan> Functions { get; }

    /// <summary>
    /// Freezes the collections describing the completed analysis context.
    /// </summary>
    internal CompilerInspectionContext(string projectPath, SourceSnapshot projectSnapshot,
        IReadOnlyList<SourceSnapshot> sources, IDictionary<string, SourceSpan> functions)
    {
        this.ProjectPath = projectPath;
        this.ProjectSnapshot = projectSnapshot;
        this.Sources = Array.AsReadOnly<SourceSnapshot>([.. sources]);
        this.Functions = new ReadOnlyDictionary<string, SourceSpan>(new Dictionary<string, SourceSpan>(functions, StringComparer.Ordinal));
    }
}
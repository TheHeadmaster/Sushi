namespace Sushi.Inspection;

/// <summary>
/// Identifies a function or a symbol owned by that function, independently
/// of how its name is rendered in an inspection document.
/// </summary>
public readonly record struct InspectionSymbolIdentity
{
    /// <summary>
    /// Gets the kind of symbol.
    /// </summary>
    public InspectionSymbolKind Kind { get; }

    /// <summary>
    /// Gets the compiler-generated symbol of the containing or referenced function.
    /// This identity is interpreted within the owning compilation context.
    /// </summary>
    public string FunctionSymbol { get; }

    /// <summary>
    /// Gets the parameter or local index, or null for a function symbol.
    /// </summary>
    public int? Index { get; }

    /// <summary>
    /// Creates a semantic symbol identity.
    /// </summary>
    /// <param name="kind">
    /// The category of the symbol.
    /// </param>
    /// <param name="functionSymbol">
    /// The containing or referenced function's compiler-generated symbol.
    /// </param>
    /// <param name="index">
    /// The function-local declaration index, required for parameters and locals.
    /// </param>
    public InspectionSymbolIdentity(InspectionSymbolKind kind, string functionSymbol, int? index = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(functionSymbol);

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if ((kind is InspectionSymbolKind.Function) != (index is null))
        {
            throw new ArgumentException("Functions must not have an index, and parameters and locals must have one.", nameof(index));
        }

        if (index is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        this.Kind = kind;
        this.FunctionSymbol = functionSymbol;
        this.Index = index;
    }
}
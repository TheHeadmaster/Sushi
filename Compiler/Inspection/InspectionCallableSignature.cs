using Sushi.Semantics;

namespace Sushi.Inspection;

/// <summary>
/// Retains the source-level callable contract for one compiler-generated function identity.
/// </summary>
public sealed record InspectionCallableSignature
{
    /// <summary>
    /// Gets the function identity within its captured compilation context.
    /// </summary>
    public string FunctionSymbol { get; }

    /// <summary>
    /// Gets the source-level function name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the declared source-level accessibility.
    /// </summary>
    public Accessibility Accessibility { get; }

    /// <summary>
    /// Gets the declared return type.
    /// </summary>
    public BoundType ReturnType { get; }

    /// <summary>
    /// Gets the source parameters in declaration order.
    /// </summary>
    public IReadOnlyList<InspectionParameterSignature> Parameters { get; }

    /// <summary>
    /// Creates an immutable description from the callable catalog's validated signature.
    /// </summary>
    /// <param name="functionSymbol">
    /// The compiler-generated identity assigned in this compilation context.
    /// </param>
    /// <param name="function">
    /// The source-derived signature collected for the package function.
    /// </param>
    internal InspectionCallableSignature(string functionSymbol, BoundFunction function)
    {
        ArgumentException.ThrowIfNullOrEmpty(functionSymbol);
        ArgumentNullException.ThrowIfNull(function);

        this.FunctionSymbol = functionSymbol;
        this.Name = function.Name;
        this.Accessibility = function.Accessibility;
        this.ReturnType = function.ReturnType;
        this.Parameters = Array.AsReadOnly<InspectionParameterSignature>([.. function.Parameters.Select(parameter =>
            new InspectionParameterSignature(parameter.Index, parameter.Name, parameter.Type))]);
    }
}

/// <summary>
/// Describes one source-level callable parameter without retaining its mutable syntax or binder object.
/// </summary>
/// <param name="Index">
/// The zero-based position in the callable signature.
/// </param>
/// <param name="Name">
/// The source-level parameter name used for named arguments.
/// </param>
/// <param name="Type">
/// The validated concrete parameter type.
/// </param>
public sealed record InspectionParameterSignature(int Index, string Name, BoundType Type);

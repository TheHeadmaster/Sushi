namespace Sushi.Semantics;

/// <summary>
/// Represents a package-level function with a concrete return contract and ordered parameters.
/// </summary>
public sealed record BoundFunction
{
    /// <summary>
    /// Gets the function's source-level accessibility.
    /// </summary>
    public Accessibility Accessibility { get; }

    /// <summary>
    /// Gets its decoded semantic name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the concrete return type.
    /// </summary>
    public BoundType ReturnType { get; }

    /// <summary>
    /// Gets the ordered parameters, indexed from zero.
    /// </summary>
    public IReadOnlyList<BoundParameter> Parameters { get; }

    /// <summary>
    /// Gets the bound body.
    /// </summary>
    public BoundBlock Body { get; }

    /// <summary>
    /// Creates a parameterless function compatible with the original MVP model.
    /// </summary>
    /// <param name="accessibility">
    /// The source-level accessibility.
    /// </param>
    /// <param name="name">
    /// The semantic name.
    /// </param>
    /// <param name="returnType">
    /// The concrete return type.
    /// </param>
    /// <param name="body">
    /// The bound function body.
    /// </param>
    public BoundFunction(Accessibility accessibility, string name, BoundType returnType, BoundBlock body)
        : this(accessibility, name, returnType, [], body)
    {
    }

    /// <summary>
    /// Creates a function with ordered, concretely typed parameters.
    /// </summary>
    /// <param name="accessibility">
    /// The source-level accessibility.
    /// </param>
    /// <param name="name">
    /// The semantic name.
    /// </param>
    /// <param name="returnType">
    /// The concrete return type.
    /// </param>
    /// <param name="parameters">
    /// The parameters in declaration order.
    /// </param>
    /// <param name="body">
    /// The bound function body.
    /// </param>
    public BoundFunction(Accessibility accessibility, string name, BoundType returnType, IReadOnlyList<BoundParameter> parameters, BoundBlock body)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(body);

        BoundParameter[] parameterArray = [.. parameters];
        HashSet<string> names = new(StringComparer.Ordinal);

        for (int i = 0; i < parameterArray.Length; i++)
        {
            BoundParameter parameter = parameterArray[i] ?? throw new ArgumentException("Function parameters cannot be null.", nameof(parameters));

            if (parameter.Index != i || !names.Add(parameter.Name))
            {
                throw new ArgumentException("Function parameters must have unique names and contiguous, zero-based indices.", nameof(parameters));
            }
        }

        this.Accessibility = accessibility;
        this.Name = name;
        this.ReturnType = returnType;
        this.Parameters = parameterArray;
        this.Body = body;
    }
}
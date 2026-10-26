namespace Sushi.Intermediate;

/// <summary>
/// Represents a package-level function in structured Sushi IR.
/// </summary>
public sealed record IRFunction
{
    /// <summary>
    /// Gets the retained Sushi accessibility.
    /// </summary>
    public IRAccessibility Accessibility { get; }

    /// <summary>
    /// Gets the semantic function name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the concrete return type.
    /// </summary>
    public IRType ReturnType { get; }

    /// <summary>
    /// Gets the ordered, typed parameters of the function.
    /// </summary>
    public IReadOnlyList<IRParameter> Parameters { get; }

    /// <summary>
    /// Gets the structured function body.
    /// </summary>
    public IRBlock Body { get; }

    /// <summary>
    /// Creates a parameterless structured IR function.
    /// </summary>
    /// <param name="accessibility">
    /// The source-level accessibility.
    /// </param>
    /// <param name="name">
    /// The semantic function name.
    /// </param>
    /// <param name="returnType">
    /// The concrete return type.
    /// </param>
    /// <param name="body">
    /// The function body.
    /// </param>
    public IRFunction(IRAccessibility accessibility, string name, IRType returnType, IRBlock body)
        : this(accessibility, name, returnType, [], body)
    {
    }

    /// <summary>
    /// Creates a structured function with a concrete typed parameter signature.
    /// </summary>
    /// <param name="accessibility">
    /// The source-level accessibility.
    /// </param>
    /// <param name="name">
    /// The semantic function name.
    /// </param>
    /// <param name="returnType">
    /// The concrete return type.
    /// </param>
    /// <param name="parameters">
    /// The parameters in declaration order.
    /// </param>
    /// <param name="body">
    /// The function body.
    /// </param>
    public IRFunction(IRAccessibility accessibility, string name, IRType returnType, IReadOnlyList<IRParameter> parameters, IRBlock body)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(body);

        IRParameter[] parameterArray = [.. parameters];

        for (int i = 0; i < parameterArray.Length; i++)
        {
            if (parameterArray[i] is null || parameterArray[i].Index != i)
            {
                throw new ArgumentException("IR parameter indices must be contiguous and start at zero.", nameof(parameters));
            }
        }

        this.Accessibility = accessibility;
        this.Name = name;
        this.ReturnType = returnType;
        this.Parameters = parameterArray;
        this.Body = body;
    }
}
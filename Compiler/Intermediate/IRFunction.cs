namespace Sushi.Intermediate;

/// <summary>
/// Represents a package-level function in structured Sushi IR.
/// </summary>
public sealed record IRFunction
{
    /// <summary>
    /// Gets the function's retained Sushi accessibility.
    /// </summary>
    public IRAccessibility Accessibility { get; }

    /// <summary>
    /// Gets the semantic function name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the function's concrete return type.
    /// </summary>
    public IRType ReturnType { get; }

    /// <summary>
    /// Gets the structured function body.
    /// </summary>
    public IRBlock Body { get; }

    /// <summary>
    /// Creates a structured IR function.
    /// </summary>
    /// <param name="accessibility">
    /// The source-level accessibility retained for later lowering and backend decisions.
    /// </param>
    /// <param name="name">
    /// The semantic function name.
    /// </param>
    /// <param name="returnType">
    /// The concrete return type.
    /// </param>
    /// <param name="body">
    /// The structured function body.
    /// </param>
    public IRFunction(IRAccessibility accessibility, string name, IRType returnType, IRBlock body)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        ArgumentNullException.ThrowIfNull(body);

        this.Accessibility = accessibility;
        this.Name = name;
        this.ReturnType = returnType;
        this.Body = body;
    }
}
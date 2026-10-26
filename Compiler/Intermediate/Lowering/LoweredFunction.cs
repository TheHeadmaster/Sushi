namespace Sushi.Intermediate.Lowering;

/// <summary>
/// Represents a lowered package-level function with explicit basic blocks and a typed parameter signature.
/// </summary>
public sealed record LoweredFunction
{
    /// <summary>
    /// Gets source-level accessibility.
    /// </summary>
    public IRAccessibility Accessibility { get; }

    /// <summary>
    /// Gets the semantic name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the concrete return type.
    /// </summary>
    public IRType ReturnType { get; }

    /// <summary>
    /// Gets the ordered function parameters.
    /// </summary>
    public IReadOnlyList<IRParameter> Parameters { get; }

    /// <summary>
    /// Gets basic blocks in deterministic order.
    /// </summary>
    public IReadOnlyList<LoweredBasicBlock> Blocks { get; }

    /// <summary>
    /// Gets the entry basic block.
    /// </summary>
    public LoweredBasicBlock EntryBlock => this.Blocks[0];

    /// <summary>
    /// Creates a lowered function with no parameters.
    /// </summary>
    /// <param name="accessibility">
    /// Source-level accessibility.
    /// </param>
    /// <param name="name">
    /// The semantic name.
    /// </param>
    /// <param name="returnType">
    /// The return type.
    /// </param>
    /// <param name="blocks">
    /// The ordered basic blocks.
    /// </param>
    public LoweredFunction(IRAccessibility accessibility, string name, IRType returnType, IReadOnlyList<LoweredBasicBlock> blocks)
        : this(accessibility, name, returnType, [], blocks)
    {
    }

    /// <summary>
    /// Creates a lowered function with ordered, typed parameters.
    /// </summary>
    /// <param name="accessibility">
    /// Source-level accessibility.
    /// </param>
    /// <param name="name">
    /// The semantic name.
    /// </param>
    /// <param name="returnType">
    /// The return type.
    /// </param>
    /// <param name="parameters">
    /// The ordered parameters.
    /// </param>
    /// <param name="blocks">
    /// The ordered basic blocks.
    /// </param>
    public LoweredFunction(IRAccessibility accessibility, string name, IRType returnType, IReadOnlyList<IRParameter> parameters, IReadOnlyList<LoweredBasicBlock> blocks)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(blocks);

        IRParameter[] parameterArray = [.. parameters];
        LoweredBasicBlock[] blockArray = [.. blocks];

        if (blockArray.Length == 0 || blockArray.Any(block => block is null))
        {
            throw new ArgumentException("A lowered function requires nonnull basic blocks, including an entry block.", nameof(blocks));
        }

        if (blockArray.Select(block => block.Name).Distinct(StringComparer.Ordinal).Count() != blockArray.Length)
        {
            throw new ArgumentException("Basic-block names must be unique within a lowered function.", nameof(blocks));
        }

        for (int i = 0; i < parameterArray.Length; i++)
        {
            if (parameterArray[i] is null || parameterArray[i].Index != i)
            {
                throw new ArgumentException("Lowered parameter indices must be contiguous and start at zero.", nameof(parameters));
            }
        }

        this.Accessibility = accessibility;
        this.Name = name;
        this.ReturnType = returnType;
        this.Parameters = parameterArray;
        this.Blocks = blockArray;
    }
}
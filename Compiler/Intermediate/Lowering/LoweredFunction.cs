namespace Sushi.Intermediate.Lowering;

/// <summary>
/// Represents a package-level function after structured Sushi IR has been lowered into explicit basic-block control flow.
/// </summary>
public sealed record LoweredFunction
{
    /// <summary>
    /// Gets the retained Sushi accessibility used by later backend visibility and linkage decisions.
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
    /// Gets the function's basic blocks in deterministic order. The first block is the entry block.
    /// </summary>
    public IReadOnlyList<LoweredBasicBlock> Blocks { get; }

    /// <summary>
    /// Gets the function's entry block.
    /// </summary>
    public LoweredBasicBlock EntryBlock => this.Blocks[0];

    /// <summary>
    /// Creates a lowered function.
    /// </summary>
    /// <param name="accessibility">
    /// The retained source-level accessibility.
    /// </param>
    /// <param name="name">
    /// The semantic function name.
    /// </param>
    /// <param name="returnType">
    /// The concrete return type.
    /// </param>
    /// <param name="blocks">
    /// The function's basic blocks in deterministic order, beginning with its entry block.
    /// </param>
    public LoweredFunction(IRAccessibility accessibility, string name, IRType returnType, IReadOnlyList<LoweredBasicBlock> blocks)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(blocks);

        LoweredBasicBlock[] blockArray = [.. blocks];

        if (blockArray.Length == 0)
        {
            throw new ArgumentException("A lowered function requires an entry basic block.", nameof(blocks));
        }

        if (blockArray.Select(block => block.Name).Distinct(StringComparer.Ordinal).Count() != blockArray.Length)
        {
            throw new ArgumentException("Basic-block names must be unique within a lowered function.", nameof(blocks));
        }

        this.Accessibility = accessibility;
        this.Name = name;
        this.ReturnType = returnType;
        this.Blocks = blockArray;
    }
}
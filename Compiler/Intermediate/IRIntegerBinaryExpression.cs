namespace Sushi.Intermediate;

/// <summary>
/// Preserves an integer binary operation, its operand order, and its concrete result type.
/// </summary>
public sealed record IRIntegerBinaryExpression : IRExpression
{
    /// <summary>
    /// Gets the resolved binary integer operation.
    /// </summary>
    public IRIntegerBinaryOperator Operator { get; }

    /// <summary>
    /// Gets the first integer operand.
    /// </summary>
    public IRExpression Left { get; }

    /// <summary>
    /// Gets the second integer operand or independently typed shift count.
    /// </summary>
    public IRExpression Right { get; }

    /// <summary>
    /// Creates a concrete binary integer IR expression.
    /// </summary>
    /// <param name="operation">
    /// The resolved binary operation.
    /// </param>
    /// <param name="left">
    /// The concrete int32 left operand.
    /// </param>
    /// <param name="right">
    /// The concrete int32 right operand.
    /// </param>
    public IRIntegerBinaryExpression(IRIntegerBinaryOperator operation, IRExpression left, IRExpression right) : base(IRType.Int32)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        if (left.Type is not IRType.Int32 || right.Type is not IRType.Int32)
        {
            throw new ArgumentException("Integer binary IR expressions require int32 operands.");
        }

        this.Operator = operation;
        this.Left = left;
        this.Right = right;
    }
}
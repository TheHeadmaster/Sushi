namespace Sushi.Intermediate;

/// <summary>
/// Preserves a concrete unary integer operation before runtime lowering.
/// </summary>
public sealed record IRIntegerUnaryExpression : IRExpression
{
    /// <summary>
    /// Gets the integer operation.
    /// </summary>
    public IRIntegerUnaryOperator Operator { get; }

    /// <summary>
    /// Gets the concrete integer operand.
    /// </summary>
    public IRExpression Operand { get; }

    /// <summary>
    /// Creates a typed unary integer IR expression.
    /// </summary>
    /// <param name="operation">
    /// The resolved integer operation.
    /// </param>
    /// <param name="operand">
    /// The concrete int32 operand.
    /// </param>
    public IRIntegerUnaryExpression(IRIntegerUnaryOperator operation, IRExpression operand) : base(IRType.Int32)
    {
        ArgumentNullException.ThrowIfNull(operand);

        if (!Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        if (operand.Type is not IRType.Int32)
        {
            throw new ArgumentException("Integer unary IR expressions require an int32 operand.", nameof(operand));
        }

        this.Operator = operation;
        this.Operand = operand;
    }
}
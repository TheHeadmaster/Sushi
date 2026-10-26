namespace Sushi.Intermediate.Lowering;

/// <summary>
/// Lowers structured Sushi IR into explicit basic-block control flow,
/// folding supported constant operations when their results are known.
/// </summary>
public sealed class IRFunctionLowerer
{
    /// <summary>
    /// Lowers a structured IR function into its control-flow representation.
    /// </summary>
    /// <param name="function">
    /// The structured function to lower.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels lowering.
    /// </param>
    /// <returns>
    /// The equivalent lowered function.
    /// </returns>
    public static LoweredFunction Lower(IRFunction function, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(function);

        cancellationToken.ThrowIfCancellationRequested();

        LoweredBasicBlock entryBlock = LowerEntryBlock(function.Body, cancellationToken);

        return new LoweredFunction(function.Accessibility, function.Name, function.ReturnType, function.Parameters, [entryBlock]);
    }

    private static LoweredBasicBlock LowerEntryBlock(IRBlock block, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (block.Statements.Count != 1 || block.Statements[0] is not IRReturnStatement returnStatement)
        {
            throw new NotSupportedException("The current lowering pass supports exactly one return statement in a function body.");
        }

        LoweredValue value = LowerExpression(returnStatement.Expression, cancellationToken);

        return new LoweredBasicBlock("entry", [], new LoweredReturnTerminator(value));
    }

    private static LoweredValue LowerExpression(IRExpression expression, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return expression switch
        {
            IRIntegerConstant integerConstant => new LoweredIntegerConstant(integerConstant.Value),
            IRBooleanConstant booleanConstant => new LoweredBooleanConstant(booleanConstant.Value),
            IRParameterReferenceExpression reference => new LoweredParameterValue(reference.Index, reference.Type),
            IRBooleanNotExpression negation => new LoweredBooleanConstant(!LowerBooleanValue(negation.Operand, cancellationToken)),
            IRBooleanBinaryExpression binary => LowerBooleanBinary(binary, cancellationToken),
            _ => throw new NotSupportedException($"Structured IR expression type \"{expression.GetType().Name}\" is not supported by lowering.")
        };
    }

    private static LoweredBooleanConstant LowerBooleanBinary(IRBooleanBinaryExpression expression, CancellationToken cancellationToken)
    {
        bool left = LowerBooleanValue(expression.Left, cancellationToken);

        bool result = expression.Operator switch
        {
            IRBooleanBinaryOperator.LogicalAnd => left && LowerBooleanValue(expression.Right, cancellationToken),
            IRBooleanBinaryOperator.LogicalOr => left || LowerBooleanValue(expression.Right, cancellationToken),
            IRBooleanBinaryOperator.EagerAnd => left & LowerBooleanValue(expression.Right, cancellationToken),
            IRBooleanBinaryOperator.EagerOr => left | LowerBooleanValue(expression.Right, cancellationToken),
            IRBooleanBinaryOperator.EagerXor => left ^ LowerBooleanValue(expression.Right, cancellationToken),
            _ => throw new NotSupportedException($"Boolean operator \"{expression.Operator}\" is not supported by lowering.")
        };

        return new LoweredBooleanConstant(result);
    }

    private static bool LowerBooleanValue(IRExpression expression, CancellationToken cancellationToken)
    {
        LoweredValue value = LowerExpression(expression, cancellationToken);

        return value is LoweredBooleanConstant constant
            ? constant.Value
            : throw new NotSupportedException("Runtime-dependent Boolean operations require the runtime Boolean lowering slice.");
    }
}
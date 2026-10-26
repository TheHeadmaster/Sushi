namespace Sushi.Intermediate.Lowering;

/// <summary>
/// Lowers structured Sushi IR into basic-block control flow while folding
/// constant operations and preserving supported runtime calculations.
/// </summary>
public sealed class IRFunctionLowerer
{
    /// <summary>
    /// Converts a structured IR function into its lowered control-flow representation.
    /// </summary>
    /// <param name="function">
    /// The structured function to lower.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels lowering.
    /// </param>
    /// <returns>
    /// The lowered function containing explicit runtime instructions where necessary.
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

        List<LoweredInstruction> instructions = [];
        int nextTemporaryIndex = 0;

        LoweredValue value = LowerExpression(returnStatement.Expression, instructions, ref nextTemporaryIndex, cancellationToken);

        return new LoweredBasicBlock("entry", instructions, new LoweredReturnTerminator(value));
    }

    private static LoweredValue LowerExpression(IRExpression expression, List<LoweredInstruction> instructions, ref int nextTemporaryIndex, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (expression)
        {
            case IRIntegerConstant integerConstant:
                return new LoweredIntegerConstant(integerConstant.Value);

            case IRBooleanConstant booleanConstant:
                return new LoweredBooleanConstant(booleanConstant.Value);

            case IRParameterReferenceExpression reference:
                return new LoweredParameterValue(reference.Index, reference.Type);

            case IRBooleanNotExpression negation:
                return LowerBooleanNegation(negation, instructions, ref nextTemporaryIndex, cancellationToken);

            case IRBooleanBinaryExpression binary:
                return LowerBooleanBinary(binary, instructions, ref nextTemporaryIndex, cancellationToken);

            default:
                throw new NotSupportedException($"Structured IR expression type \"{expression.GetType().Name}\" is not supported by lowering.");
        }
    }

    private static LoweredValue LowerBooleanNegation(IRBooleanNotExpression expression, List<LoweredInstruction> instructions, ref int nextTemporaryIndex, CancellationToken cancellationToken)
    {
        LoweredValue operand = LowerExpression(expression.Operand, instructions, ref nextTemporaryIndex, cancellationToken);

        if (operand is LoweredBooleanConstant constant)
        {
            return new LoweredBooleanConstant(!constant.Value);
        }

        LoweredTemporaryValue destination = new(nextTemporaryIndex++, IRType.Bool);
        instructions.Add(new LoweredBooleanNotInstruction(destination, operand));

        return destination;
    }

    private static LoweredValue LowerBooleanBinary(IRBooleanBinaryExpression expression, List<LoweredInstruction> instructions, ref int nextTemporaryIndex, CancellationToken cancellationToken)
    {
        return expression.Operator switch
        {
            IRBooleanBinaryOperator.LogicalAnd or IRBooleanBinaryOperator.LogicalOr
                => LowerLogicalBooleanBinary(expression, instructions, ref nextTemporaryIndex, cancellationToken),

            IRBooleanBinaryOperator.EagerAnd or IRBooleanBinaryOperator.EagerOr or IRBooleanBinaryOperator.EagerXor
                => LowerEagerBooleanBinary(expression, instructions, ref nextTemporaryIndex, cancellationToken),

            _ => throw new NotSupportedException($"Boolean operator \"{expression.Operator}\" is not supported by lowering.")
        };
    }

    private static LoweredValue LowerLogicalBooleanBinary(IRBooleanBinaryExpression expression, List<LoweredInstruction> instructions, ref int nextTemporaryIndex, CancellationToken cancellationToken)
    {
        LoweredValue left = LowerExpression(expression.Left, instructions, ref nextTemporaryIndex, cancellationToken);

        if (left is not LoweredBooleanConstant constant)
        {
            throw new NotSupportedException("Runtime-dependent logical and/or operations require conditional control-flow lowering.");
        }

        if (expression.Operator is IRBooleanBinaryOperator.LogicalAnd && !constant.Value)
        {
            return new LoweredBooleanConstant(false);
        }

        if (expression.Operator is IRBooleanBinaryOperator.LogicalOr && constant.Value)
        {
            return new LoweredBooleanConstant(true);
        }

        return LowerExpression(expression.Right, instructions, ref nextTemporaryIndex, cancellationToken);
    }

    private static LoweredValue LowerEagerBooleanBinary(IRBooleanBinaryExpression expression, List<LoweredInstruction> instructions, ref int nextTemporaryIndex, CancellationToken cancellationToken)
    {
        LoweredValue left = LowerExpression(expression.Left, instructions, ref nextTemporaryIndex, cancellationToken);
        LoweredValue right = LowerExpression(expression.Right, instructions, ref nextTemporaryIndex, cancellationToken);

        if (left is LoweredBooleanConstant leftConstant && right is LoweredBooleanConstant rightConstant)
        {
            bool result = expression.Operator switch
            {
                IRBooleanBinaryOperator.EagerAnd => leftConstant.Value & rightConstant.Value,
                IRBooleanBinaryOperator.EagerOr => leftConstant.Value | rightConstant.Value,
                IRBooleanBinaryOperator.EagerXor => leftConstant.Value ^ rightConstant.Value,
                _ => throw new InvalidOperationException("Unexpected eager Boolean operator.")
            };

            return new LoweredBooleanConstant(result);
        }

        LoweredTemporaryValue destination = new(nextTemporaryIndex++, IRType.Bool);
        instructions.Add(new LoweredBooleanBinaryInstruction(destination, expression.Operator, left, right));

        return destination;
    }
}
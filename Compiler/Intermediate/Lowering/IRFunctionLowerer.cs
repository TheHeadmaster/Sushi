namespace Sushi.Intermediate.Lowering;

/// <summary>
/// Lowers structured Sushi IR into explicit basic-block control flow.
/// </summary>
public sealed class IRFunctionLowerer
{
    /// <summary>
    /// Lowers a structured IR function into its control-flow representation.
    /// </summary>
    /// <param name="function">
    /// The structured Sushi IR function to lower.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel lowering.
    /// </param>
    /// <returns>
    /// The equivalent lowered function.
    /// </returns>
    public static LoweredFunction Lower(IRFunction function, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(function);

        cancellationToken.ThrowIfCancellationRequested();

        LoweredBasicBlock entryBlock = LowerEntryBlock(function.Body, cancellationToken);

        return new LoweredFunction(function.Accessibility, function.Name, function.ReturnType, [entryBlock]);
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
            _ => throw new NotSupportedException($"Structured IR expression type \"{expression.GetType().Name}\" is not supported by lowering.")
        };
    }
}
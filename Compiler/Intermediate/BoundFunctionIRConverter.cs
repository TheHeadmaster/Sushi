using Sushi.Semantics;

namespace Sushi.Intermediate;

/// <summary>
/// Converts semantically bound package-level functions into structured Sushi IR.
/// </summary>
public sealed class BoundFunctionIRConverter
{
    /// <summary>
    /// Converts a bound function into structured Sushi IR.
    /// </summary>
    /// <param name="function">
    /// The semantically bound function to convert.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel conversion.
    /// </param>
    /// <returns>
    /// The equivalent structured Sushi IR function.
    /// </returns>
    public static IRFunction Convert(BoundFunction function, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(function);

        cancellationToken.ThrowIfCancellationRequested();

        return new IRFunction(
            ConvertAccessibility(function.Accessibility),
            function.Name,
            ConvertType(function.ReturnType),
            [.. function.Parameters.Select(parameter => new IRParameter(parameter.Index, parameter.Name, ConvertType(parameter.Type)))],
            ConvertBlock(function.Body, cancellationToken));
    }

    private static IRBlock ConvertBlock(BoundBlock block, CancellationToken cancellationToken)
    {
        List<IRStatement> statements = [with(block.Statements.Count)];

        foreach (BoundStatement statement in block.Statements)
        {
            cancellationToken.ThrowIfCancellationRequested();

            statements.Add(ConvertStatement(statement, cancellationToken));
        }

        return new IRBlock(statements);
    }

    private static IRStatement ConvertStatement(BoundStatement statement, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return statement switch
        {
            BoundReturnStatement returnStatement => new IRReturnStatement(ConvertExpression(returnStatement.Expression, cancellationToken)),
            _ => throw new NotSupportedException($"Bound statement type \"{statement.GetType().Name}\" is not supported by structured Sushi IR.")
        };
    }

    private static IRExpression ConvertExpression(BoundExpression expression, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return expression switch
        {
            BoundIntegerLiteralExpression integerLiteral => new IRIntegerConstant(integerLiteral.Value),
            BoundBooleanConstantExpression booleanConstant => new IRBooleanConstant(booleanConstant.Value),
            BoundParameterReferenceExpression reference => new IRParameterReferenceExpression(reference.Parameter.Index, ConvertType(reference.Type)),
            BoundBooleanNotExpression negation => new IRBooleanNotExpression(ConvertExpression(negation.Operand, cancellationToken)),
            BoundBooleanBinaryExpression binary => new IRBooleanBinaryExpression(
                ConvertBooleanOperator(binary.Operator),
                ConvertExpression(binary.Left, cancellationToken),
                ConvertExpression(binary.Right, cancellationToken)),
            BoundIntegerUnaryExpression unary => new IRIntegerUnaryExpression(
                ConvertIntegerUnaryOperator(unary.Operator),
                ConvertExpression(unary.Operand, cancellationToken)),
            BoundIntegerBinaryExpression integerBinary => new IRIntegerBinaryExpression(
                ConvertIntegerBinaryOperator(integerBinary.Operator),
                ConvertExpression(integerBinary.Left, cancellationToken),
                ConvertExpression(integerBinary.Right, cancellationToken)),
            BoundIntegerComparisonExpression comparison => new IRIntegerComparisonExpression(
                ConvertIntegerComparisonOperator(comparison.Operator),
                ConvertExpression(comparison.Left, cancellationToken),
                ConvertExpression(comparison.Right, cancellationToken)),
            BoundIntegerRangeComparisonExpression range => new IRIntegerRangeComparisonExpression(
                ConvertExpression(range.Operand, cancellationToken), range.Result),
            _ => throw new NotSupportedException($"Bound expression type \"{expression.GetType().Name}\" is not supported by structured Sushi IR.")
        };
    }

    private static IRBooleanBinaryOperator ConvertBooleanOperator(BooleanBinaryOperator operation)
        => operation switch
        {
            BooleanBinaryOperator.LogicalAnd => IRBooleanBinaryOperator.LogicalAnd,
            BooleanBinaryOperator.LogicalOr => IRBooleanBinaryOperator.LogicalOr,
            BooleanBinaryOperator.EagerAnd => IRBooleanBinaryOperator.EagerAnd,
            BooleanBinaryOperator.EagerOr => IRBooleanBinaryOperator.EagerOr,
            BooleanBinaryOperator.EagerXor => IRBooleanBinaryOperator.EagerXor,
            _ => throw new NotSupportedException($"Boolean operator \"{operation}\" is not supported by structured Sushi IR.")
        };

    private static IRIntegerUnaryOperator ConvertIntegerUnaryOperator(IntegerUnaryOperator operation)
        => operation switch
        {
            IntegerUnaryOperator.Plus => IRIntegerUnaryOperator.Plus,
            IntegerUnaryOperator.Negate => IRIntegerUnaryOperator.Negate,
            IntegerUnaryOperator.Complement => IRIntegerUnaryOperator.Complement,
            _ => throw new NotSupportedException($"Integer unary operator \"{operation}\" is not supported by structured Sushi IR.")
        };

    private static IRIntegerBinaryOperator ConvertIntegerBinaryOperator(IntegerBinaryOperator operation)
        => operation switch
        {
            IntegerBinaryOperator.Add => IRIntegerBinaryOperator.Add,
            IntegerBinaryOperator.Subtract => IRIntegerBinaryOperator.Subtract,
            IntegerBinaryOperator.Multiply => IRIntegerBinaryOperator.Multiply,
            IntegerBinaryOperator.Divide => IRIntegerBinaryOperator.Divide,
            IntegerBinaryOperator.Remainder => IRIntegerBinaryOperator.Remainder,
            IntegerBinaryOperator.BitwiseAnd => IRIntegerBinaryOperator.BitwiseAnd,
            IntegerBinaryOperator.BitwiseOr => IRIntegerBinaryOperator.BitwiseOr,
            IntegerBinaryOperator.BitwiseXor => IRIntegerBinaryOperator.BitwiseXor,
            IntegerBinaryOperator.ShiftLeft => IRIntegerBinaryOperator.ShiftLeft,
            IntegerBinaryOperator.ShiftRight => IRIntegerBinaryOperator.ShiftRight,
            _ => throw new NotSupportedException($"Integer binary operator \"{operation}\" is not supported by structured Sushi IR.")
        };

    private static IRIntegerComparisonOperator ConvertIntegerComparisonOperator(IntegerComparisonOperator operation)
        => operation switch
        {
            IntegerComparisonOperator.Equal => IRIntegerComparisonOperator.Equal,
            IntegerComparisonOperator.NotEqual => IRIntegerComparisonOperator.NotEqual,
            IntegerComparisonOperator.LessThan => IRIntegerComparisonOperator.LessThan,
            IntegerComparisonOperator.LessThanOrEqual => IRIntegerComparisonOperator.LessThanOrEqual,
            IntegerComparisonOperator.GreaterThan => IRIntegerComparisonOperator.GreaterThan,
            IntegerComparisonOperator.GreaterThanOrEqual => IRIntegerComparisonOperator.GreaterThanOrEqual,
            _ => throw new NotSupportedException($"Integer comparison \"{operation}\" is not supported by structured Sushi IR.")
        };

    private static IRType ConvertType(BoundType type)
        => type switch
        {
            BoundType.Int32 => IRType.Int32,
            BoundType.Bool => IRType.Bool,
            _ => throw new NotSupportedException($"Bound type \"{type}\" is not supported by structured Sushi IR.")
        };

    private static IRAccessibility ConvertAccessibility(Accessibility accessibility)
        => accessibility switch
        {
            Accessibility.Public => IRAccessibility.Public,
            Accessibility.Internal => IRAccessibility.Internal,
            Accessibility.Package => IRAccessibility.Package,
            Accessibility.Protected => IRAccessibility.Protected,
            Accessibility.Private => IRAccessibility.Private,
            _ => throw new NotSupportedException($"Accessibility \"{accessibility}\" is not supported by structured Sushi IR.")
        };
}
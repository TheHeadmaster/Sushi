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
            _ => throw new NotSupportedException($"Bound expression type \"{expression.GetType().Name}\" is not supported by structured Sushi IR.")
        };
    }

    private static IRType ConvertType(BoundIntegerType type)
        => type switch
        {
            BoundIntegerType.Int32 => IRType.Int32,
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
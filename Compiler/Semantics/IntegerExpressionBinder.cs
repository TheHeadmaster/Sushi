using System.Numerics;
using Sushi.Diagnostics;
using Sushi.Parsing.Syntax;

namespace Sushi.Semantics;

/// <summary>
/// Binds concrete int32 expressions containing runtime parameters while preserving
/// exact constant evaluation for subexpressions independent of runtime values.
/// </summary>
internal static class IntegerExpressionBinder
{
    private const int Int32Width = 32;

    /// <summary>
    /// Binds an integer expression using the enclosing function's parameter declarations.
    /// </summary>
    /// <param name="expression">
    /// The parsed integer expression.
    /// </param>
    /// <param name="parameters">
    /// The enclosing function's parameters in declaration order.
    /// </param>
    /// <param name="diagnosticReporter">
    /// Receives semantic and integer-constant diagnostics.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels recursive binding.
    /// </param>
    /// <returns>
    /// The typed expression, or null when binding cannot succeed.
    /// </returns>
    public static BoundExpression? Bind(ExpressionSyntax expression, IReadOnlyList<BoundParameter> parameters, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);

        return BindExpression(expression, parameters, diagnosticReporter, cancellationToken);
    }

    private static BoundExpression? BindExpression(ExpressionSyntax expression, IReadOnlyList<BoundParameter> parameters, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!ContainsRuntimeIdentifier(expression))
        {
            return IntegerConstantExpressionBinder.Bind(expression, diagnosticReporter, cancellationToken);
        }

        switch (expression)
        {
            case IdentifierExpressionSyntax identifier:
                return BindParameterReference(identifier, parameters, diagnosticReporter);

            case ParenthesizedExpressionSyntax grouped when grouped.OpenParenthesisToken.IsSourceBacked && grouped.CloseParenthesisToken.IsSourceBacked:
                return BindExpression(grouped.Expression, parameters, diagnosticReporter, cancellationToken);

            case ParenthesizedExpressionSyntax:
            case IntegerLiteralExpressionSyntax { LiteralToken.IsMissing: true }:
                return null;

            case UnaryExpressionSyntax unary when TryGetUnaryOperator(unary.OperatorToken, out IntegerUnaryOperator unaryOperation):
                return BindUnary(unary, unaryOperation, parameters, diagnosticReporter, cancellationToken);

            case BinaryExpressionSyntax binary when TryGetBinaryOperator(binary.OperatorToken, out IntegerBinaryOperator binaryOperation):
                return BindBinary(binary, binaryOperation, parameters, diagnosticReporter, cancellationToken);

            default:
                diagnosticReporter.GenerateError(ErrorType.UnsupportedMVPExpression, expression.Span);
                return null;
        }
    }

    private static BoundExpression? BindParameterReference(IdentifierExpressionSyntax expression, IReadOnlyList<BoundParameter> parameters, IDiagnosticReporter diagnosticReporter)
    {
        string? name = IdentifierBinder.Bind(expression.IdentifierToken);
        BoundParameter? parameter = name is null ? null : parameters.FirstOrDefault(candidate => StringComparer.Ordinal.Equals(candidate.Name, name));

        if (parameter is null)
        {
            diagnosticReporter.GenerateError(ErrorType.UnknownIdentifier, expression.Span);
            return null;
        }

        if (parameter.Type is not BoundType.Int32)
        {
            diagnosticReporter.GenerateError(ErrorType.UnsupportedMVPExpression, expression.Span);
            return null;
        }

        return new BoundParameterReferenceExpression(parameter);
    }

    private static BoundExpression? BindUnary(UnaryExpressionSyntax expression, IntegerUnaryOperator operation, IReadOnlyList<BoundParameter> parameters, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        BoundExpression? operand = BindExpression(expression.Operand, parameters, diagnosticReporter, cancellationToken);

        return operand is null ? null : new BoundIntegerUnaryExpression(operation, operand);
    }

    private static BoundExpression? BindBinary(BinaryExpressionSyntax expression, IntegerBinaryOperator operation, IReadOnlyList<BoundParameter> parameters, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        BoundExpression? left = BindExpression(expression.Left, parameters, diagnosticReporter, cancellationToken);

        BoundExpression? right = operation is IntegerBinaryOperator.ShiftLeft or IntegerBinaryOperator.ShiftRight
            ? BindShiftCount(expression.Right, parameters, diagnosticReporter, cancellationToken)
            : BindExpression(expression.Right, parameters, diagnosticReporter, cancellationToken);

        if (left is null || right is null)
        {
            return null;
        }

        if (operation is IntegerBinaryOperator.Divide or IntegerBinaryOperator.Remainder
            && right is BoundIntegerLiteralExpression { Value: 0 })
        {
            diagnosticReporter.GenerateError(ErrorType.IntegerDivisionByZero, expression.OperatorToken.Span);
            return null;
        }

        return new BoundIntegerBinaryExpression(operation, left, right);
    }

    private static BoundExpression? BindShiftCount(ExpressionSyntax expression, IReadOnlyList<BoundParameter> parameters, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        if (ContainsRuntimeIdentifier(expression))
        {
            return BindExpression(expression, parameters, diagnosticReporter, cancellationToken);
        }

        if (!IntegerConstantExpressionBinder.TryEvaluateUntyped(expression, diagnosticReporter, cancellationToken, out BigInteger count))
        {
            return null;
        }

        if (count < BigInteger.Zero || count >= Int32Width)
        {
            diagnosticReporter.GenerateError(ErrorType.InvalidIntegerShiftCount, expression.Span);
            return null;
        }

        return new BoundIntegerLiteralExpression((int)count);
    }

    private static bool ContainsRuntimeIdentifier(ExpressionSyntax expression)
        => expression switch
        {
            IdentifierExpressionSyntax => true,
            ParenthesizedExpressionSyntax grouped => ContainsRuntimeIdentifier(grouped.Expression),
            UnaryExpressionSyntax unary => ContainsRuntimeIdentifier(unary.Operand),
            BinaryExpressionSyntax binary => ContainsRuntimeIdentifier(binary.Left) || ContainsRuntimeIdentifier(binary.Right),
            _ => false
        };

    private static bool TryGetUnaryOperator(SyntaxToken token, out IntegerUnaryOperator operation)
    {
        operation = default;

        if (token.Type is not SyntaxType.OperatorToken || !token.IsSourceBacked || token.Span.Length != 1)
        {
            return false;
        }

        byte spelling = token.Span.Snapshot.Bytes.Span[token.Span.Start];

        switch (spelling)
        {
            case (byte)'+':
                operation = IntegerUnaryOperator.Plus;
                return true;

            case (byte)'-':
                operation = IntegerUnaryOperator.Negate;
                return true;

            case (byte)'~':
                operation = IntegerUnaryOperator.Complement;
                return true;

            default:
                return false;
        }
    }

    private static bool TryGetBinaryOperator(SyntaxToken token, out IntegerBinaryOperator operation)
    {
        operation = default;

        if (token.Type is not SyntaxType.OperatorToken || !token.IsSourceBacked)
        {
            return false;
        }

        ReadOnlySpan<byte> spelling = token.Span.Snapshot.Bytes.Span[token.Span.Start..token.Span.End];

        if (spelling.SequenceEqual("<<"u8))
        {
            operation = IntegerBinaryOperator.ShiftLeft;
            return true;
        }

        if (spelling.SequenceEqual(">>"u8))
        {
            operation = IntegerBinaryOperator.ShiftRight;
            return true;
        }

        if (spelling.Length != 1)
        {
            return false;
        }

        switch (spelling[0])
        {
            case (byte)'+':
                operation = IntegerBinaryOperator.Add;
                return true;

            case (byte)'-':
                operation = IntegerBinaryOperator.Subtract;
                return true;

            case (byte)'*':
                operation = IntegerBinaryOperator.Multiply;
                return true;

            case (byte)'/':
                operation = IntegerBinaryOperator.Divide;
                return true;

            case (byte)'%':
                operation = IntegerBinaryOperator.Remainder;
                return true;

            case (byte)'&':
                operation = IntegerBinaryOperator.BitwiseAnd;
                return true;

            case (byte)'|':
                operation = IntegerBinaryOperator.BitwiseOr;
                return true;

            case (byte)'^':
                operation = IntegerBinaryOperator.BitwiseXor;
                return true;

            default:
                return false;
        }
    }
}
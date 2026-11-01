using System.Numerics;
using Sushi.Diagnostics;
using Sushi.Parsing.Syntax;

namespace Sushi.Semantics;

/// <summary>
/// Binds concrete int32 expressions containing runtime parameters, locals,
/// calls, and update expressions while preserving exact constant evaluation
/// for independent subexpressions.
/// </summary>
internal static class IntegerExpressionBinder
{
    private const int Int32Width = 32;

    /// <summary>
    /// Binds an integer expression using visible parameters, locals, and package functions.
    /// </summary>
    /// <param name="expression">
    /// The parsed expression.
    /// </param>
    /// <param name="parameters">
    /// The enclosing function's parameters.
    /// </param>
    /// <param name="diagnosticReporter">
    /// Receives semantic and constant-evaluation diagnostics.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels recursive binding.
    /// </param>
    /// <param name="callables">
    /// Optional package functions available to value-producing invocations.
    /// </param>
    /// <param name="locals">
    /// Optional lexical local-binding context.
    /// </param>
    /// <returns>
    /// The typed expression, or null when binding fails.
    /// </returns>
    public static BoundExpression? Bind(
        ExpressionSyntax expression,
        IReadOnlyList<BoundParameter> parameters,
        IDiagnosticReporter diagnosticReporter,
        CancellationToken cancellationToken,
        IReadOnlyList<PackageCallable>? callables = null,
        LocalBindingContext? locals = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);

        return BindExpression(expression, parameters, diagnosticReporter, callables, locals, cancellationToken);
    }

    private static BoundExpression? BindExpression(
        ExpressionSyntax expression,
        IReadOnlyList<BoundParameter> parameters,
        IDiagnosticReporter diagnosticReporter,
        IReadOnlyList<PackageCallable>? callables,
        LocalBindingContext? locals,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!ContainsRuntimeValue(expression))
        {
            return IntegerConstantExpressionBinder.Bind(expression, diagnosticReporter, cancellationToken);
        }

        switch (expression)
        {
            case IdentifierExpressionSyntax identifier:
                return BindParameterReference(identifier, parameters, diagnosticReporter, locals);

            case InvocationExpressionSyntax invocation when callables is not null:
                return PackageInvocationBinder.BindValue(invocation, BoundType.Int32, callables, parameters, diagnosticReporter, cancellationToken, locals);

            case IncrementDecrementExpressionSyntax update:
                return IntegerUpdateBinder.Bind(update, diagnosticReporter, locals);

            case ParenthesizedExpressionSyntax grouped when grouped.OpenParenthesisToken.IsSourceBacked && grouped.CloseParenthesisToken.IsSourceBacked:
                return BindExpression(grouped.Expression, parameters, diagnosticReporter, callables, locals, cancellationToken);

            case ParenthesizedExpressionSyntax:
            case IntegerLiteralExpressionSyntax { LiteralToken.IsMissing: true }:
                return null;

            case UnaryExpressionSyntax unary when TryGetUnaryOperator(unary.OperatorToken, out IntegerUnaryOperator operation):
                return BindUnary(unary, operation, parameters, diagnosticReporter, callables, locals, cancellationToken);

            case BinaryExpressionSyntax binary when TryGetBinaryOperator(binary.OperatorToken, out IntegerBinaryOperator operation):
                return BindBinary(binary, operation, parameters, diagnosticReporter, callables, locals, cancellationToken);

            default:
                diagnosticReporter.GenerateError(ErrorType.UnsupportedMVPExpression, expression.Span);
                return null;
        }
    }

    private static BoundExpression? BindParameterReference(
        IdentifierExpressionSyntax expression,
        IReadOnlyList<BoundParameter> parameters,
        IDiagnosticReporter diagnosticReporter,
        LocalBindingContext? locals)
    {
        string? name = IdentifierBinder.Bind(expression.IdentifierToken);
        BoundExpression? reference = name is null ? null : locals?.Resolve(name);

        if (reference is null && name is not null && locals is null)
        {
            BoundParameter? parameter = parameters.FirstOrDefault(candidate => StringComparer.Ordinal.Equals(candidate.Name, name));
            reference = parameter is null ? null : new BoundParameterReferenceExpression(parameter);
        }

        if (reference is null)
        {
            diagnosticReporter.GenerateError(ErrorType.UnknownIdentifier, expression.Span);
            return null;
        }

        if (reference.Type is not BoundType.Int32)
        {
            diagnosticReporter.GenerateError(ErrorType.UnsupportedMVPExpression, expression.Span);
            return null;
        }

        return reference;
    }

    private static BoundExpression? BindUnary(
        UnaryExpressionSyntax expression,
        IntegerUnaryOperator operation,
        IReadOnlyList<BoundParameter> parameters,
        IDiagnosticReporter diagnosticReporter,
        IReadOnlyList<PackageCallable>? callables,
        LocalBindingContext? locals,
        CancellationToken cancellationToken)
    {
        BoundExpression? operand = BindExpression(expression.Operand, parameters, diagnosticReporter, callables, locals, cancellationToken);
        return operand is null ? null : new BoundIntegerUnaryExpression(operation, operand);
    }

    private static BoundExpression? BindBinary(
        BinaryExpressionSyntax expression,
        IntegerBinaryOperator operation,
        IReadOnlyList<BoundParameter> parameters,
        IDiagnosticReporter diagnosticReporter,
        IReadOnlyList<PackageCallable>? callables,
        LocalBindingContext? locals,        
        CancellationToken cancellationToken)
    {
        BoundExpression? left = BindExpression(expression.Left, parameters, diagnosticReporter, callables, locals, cancellationToken);

        BoundExpression? right = operation is IntegerBinaryOperator.ShiftLeft or IntegerBinaryOperator.ShiftRight
            ? BindShiftCount(expression.Right, parameters, diagnosticReporter, callables, locals, cancellationToken)
            : BindExpression(expression.Right, parameters, diagnosticReporter, callables, locals, cancellationToken);

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

    private static BoundExpression? BindShiftCount(
        ExpressionSyntax expression,
        IReadOnlyList<BoundParameter> parameters,
        IDiagnosticReporter diagnosticReporter,
        IReadOnlyList<PackageCallable>? callables,
        LocalBindingContext? locals,
        CancellationToken cancellationToken)
    {
        if (ContainsRuntimeValue(expression))
        {
            return BindExpression(expression, parameters, diagnosticReporter, callables, locals, cancellationToken);
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

    private static bool ContainsRuntimeValue(ExpressionSyntax expression)
        => expression switch
        {
            IdentifierExpressionSyntax or InvocationExpressionSyntax or IncrementDecrementExpressionSyntax => true,
            ParenthesizedExpressionSyntax grouped => ContainsRuntimeValue(grouped.Expression),
            UnaryExpressionSyntax unary => ContainsRuntimeValue(unary.Operand),
            BinaryExpressionSyntax binary => ContainsRuntimeValue(binary.Left) || ContainsRuntimeValue(binary.Right),
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
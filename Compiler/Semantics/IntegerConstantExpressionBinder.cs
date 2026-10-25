using System.Numerics;
using Sushi.Diagnostics;
using Sushi.Parsing.Syntax;

namespace Sushi.Semantics;

/// <summary>
/// Evaluates supported integer constant expressions exactly and binds their
/// final mathematical value to the executable MVP's int32 return type.
/// </summary>
internal static class IntegerConstantExpressionBinder
{
    /// <summary>
    /// Binds an integer constant expression in an int32 result context.
    /// Intermediate untyped constants retain their exact mathematical values.
    /// </summary>
    /// <param name="expression">
    /// The parsed expression to evaluate.
    /// </param>
    /// <param name="diagnosticReporter">
    /// Receives unsupported-expression, zero-divisor, and range diagnostics.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel recursive constant evaluation.
    /// </param>
    /// <returns>
    /// The concrete int32 constant, or null when binding fails.
    /// </returns>
    public static BoundIntegerLiteralExpression? Bind(ExpressionSyntax expression, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);

        if (!TryEvaluateExact(expression, diagnosticReporter, cancellationToken, out BigInteger value))
        {
            return null;
        }

#pragma warning disable IDE0078 // Use pattern matching
        if (value < int.MinValue || value > int.MaxValue)
        {
            diagnosticReporter.GenerateError(ErrorType.IntegerConstantOutOfRange, expression.Span);
            return null;
        }
#pragma warning restore IDE0078 // Use pattern matching

        return new BoundIntegerLiteralExpression((int)value);
    }

    private static bool TryEvaluateExact(ExpressionSyntax expression, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken, out BigInteger value)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (expression)
        {
            case IntegerLiteralExpressionSyntax literal:
                return IntegerLiteralBinder.TryBindExactValue(literal, out value);

            case ParenthesizedExpressionSyntax grouped when grouped.OpenParenthesisToken.IsSourceBacked && grouped.CloseParenthesisToken.IsSourceBacked:
                return TryEvaluateExact(grouped.Expression, diagnosticReporter, cancellationToken, out value);

            case ParenthesizedExpressionSyntax:
                value = default;
                return false;

            case UnaryExpressionSyntax unary:
                return TryEvaluateUnary(unary, diagnosticReporter, cancellationToken, out value);

            case BinaryExpressionSyntax binary:
                return TryEvaluateBinary(binary, diagnosticReporter, cancellationToken, out value);

            default:
                diagnosticReporter.GenerateError(ErrorType.UnsupportedMVPExpression, expression.Span);
                value = default;
                return false;
        }
    }

    private static bool TryEvaluateUnary(UnaryExpressionSyntax expression, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken, out BigInteger value)
    {
        value = default;

        if (!TryGetOperator(expression.OperatorToken, out byte spelling)
            || spelling is not ((byte)'+' or (byte)'-'))
        {
            diagnosticReporter.GenerateError(ErrorType.UnsupportedMVPExpression, expression.Span);
            return false;
        }

        if (!TryEvaluateExact(expression.Operand, diagnosticReporter, cancellationToken, out BigInteger operand))
        {
            return false;
        }

        value = spelling == (byte)'-' ? -operand : operand;
        return true;
    }

    private static bool TryEvaluateBinary(BinaryExpressionSyntax expression, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken, out BigInteger value)
    {
        value = default;

        if (!TryGetOperator(expression.OperatorToken, out byte spelling)
            || spelling is not ((byte)'+' or (byte)'-' or (byte)'*' or (byte)'/' or (byte)'%'))
        {
            diagnosticReporter.GenerateError(ErrorType.UnsupportedMVPExpression, expression.Span);
            return false;
        }

        if (!TryEvaluateExact(expression.Left, diagnosticReporter, cancellationToken, out BigInteger left)
            || !TryEvaluateExact(expression.Right, diagnosticReporter, cancellationToken, out BigInteger right))
        {
            return false;
        }

        if (right.IsZero && spelling is (byte)'/' or (byte)'%')
        {
            diagnosticReporter.GenerateError(ErrorType.IntegerDivisionByZero, expression.OperatorToken.Span);
            return false;
        }

        value = spelling switch
        {
            (byte)'+' => left + right,
            (byte)'-' => left - right,
            (byte)'*' => left * right,
            (byte)'/' => left / right,
            (byte)'%' => left % right,
            _ => throw new InvalidOperationException("Unexpected arithmetic operator.")
        };

        return true;
    }

    private static bool TryGetOperator(SyntaxToken token, out byte spelling)
    {
        if (token.Type is SyntaxType.OperatorToken && token.IsSourceBacked && token.Span.Length == 1)
        {
            spelling = token.Span.Snapshot.Bytes.Span[token.Span.Start];
            return true;
        }

        spelling = default;
        return false;
    }
}
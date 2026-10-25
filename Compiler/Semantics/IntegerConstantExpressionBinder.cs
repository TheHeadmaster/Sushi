using System.Numerics;
using Sushi.Diagnostics;
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.Semantics;

/// <summary>
/// Evaluates supported integer constant expressions, retaining exact arithmetic
/// while applying contextual int32 rules to concrete bitwise and shift operations.
/// </summary>
internal static class IntegerConstantExpressionBinder
{
    private const int Int32Width = 32;

    /// <summary>
    /// Binds an integer constant expression against the executable MVP's int32 return contract.
    /// </summary>
    /// <param name="expression">The parsed integer expression.</param>
    /// <param name="diagnosticReporter">Receives constant-evaluation and range diagnostics.</param>
    /// <param name="cancellationToken">Cancels recursive evaluation.</param>
    /// <returns>The concrete int32 result, or null when evaluation fails.</returns>
    public static BoundIntegerLiteralExpression? Bind(ExpressionSyntax expression, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);

        if (!TryEvaluate(expression, requiresInt32: true, diagnosticReporter, cancellationToken, out BigInteger value))
        {
            return null;
        }

        if (!RequireInt32(value, expression.Span, diagnosticReporter))
        {
            return null;
        }

        return new BoundIntegerLiteralExpression((int)value);
    }

    private static bool TryEvaluate(ExpressionSyntax expression, bool requiresInt32, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken, out BigInteger value)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (expression)
        {
            case IntegerLiteralExpressionSyntax literal:
                return IntegerLiteralBinder.TryBindExactValue(literal, out value);

            case ParenthesizedExpressionSyntax grouped when grouped.OpenParenthesisToken.IsSourceBacked && grouped.CloseParenthesisToken.IsSourceBacked:
                return TryEvaluate(grouped.Expression, requiresInt32, diagnosticReporter, cancellationToken, out value);

            case ParenthesizedExpressionSyntax:
                value = default;
                return false;

            case UnaryExpressionSyntax unary:
                return TryEvaluateUnary(unary, requiresInt32, diagnosticReporter, cancellationToken, out value);

            case BinaryExpressionSyntax binary:
                return TryEvaluateBinary(binary, requiresInt32, diagnosticReporter, cancellationToken, out value);

            default:
                diagnosticReporter.GenerateError(ErrorType.UnsupportedMVPExpression, expression.Span);
                value = default;
                return false;
        }
    }

    private static bool TryEvaluateUnary(UnaryExpressionSyntax expression, bool requiresInt32, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken, out BigInteger value)
    {
        value = default;

        if (!TryGetSingleByteOperator(expression.OperatorToken, out byte spelling)
            || spelling is not ((byte)'+' or (byte)'-' or (byte)'~'))
        {
            diagnosticReporter.GenerateError(ErrorType.UnsupportedMVPExpression, expression.Span);
            return false;
        }

        if (!TryEvaluate(expression.Operand, requiresInt32, diagnosticReporter, cancellationToken, out BigInteger operand))
        {
            return false;
        }

        if (spelling == (byte)'~' && requiresInt32 && !RequireInt32(operand, expression.Operand.Span, diagnosticReporter))
        {
            return false;
        }

        value = spelling switch
        {
            (byte)'+' => operand,
            (byte)'-' => -operand,
            (byte)'~' => ~operand,
            _ => throw new InvalidOperationException("Unexpected unary integer operator.")
        };

        return true;
    }

    private static bool TryEvaluateBinary(BinaryExpressionSyntax expression, bool requiresInt32, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken, out BigInteger value)
    {
        value = default;

        SyntaxToken token = expression.OperatorToken;

        if (token.Type is not SyntaxType.OperatorToken || !token.IsSourceBacked)
        {
            diagnosticReporter.GenerateError(ErrorType.UnsupportedMVPExpression, expression.Span);
            return false;
        }

        ReadOnlySpan<byte> spelling = token.Span.Snapshot.Bytes.Span[token.Span.Start..token.Span.End];

        if (spelling.SequenceEqual("<<"u8))
        {
            return TryEvaluateShift(expression, leftShift: true, requiresInt32, diagnosticReporter, cancellationToken, out value);
        }

        if (spelling.SequenceEqual(">>"u8))
        {
            return TryEvaluateShift(expression, leftShift: false, requiresInt32, diagnosticReporter, cancellationToken, out value);
        }

        if (spelling.Length != 1 || spelling[0] is not ((byte)'+' or (byte)'-' or (byte)'*' or (byte)'/' or (byte)'%' or (byte)'&' or (byte)'|' or (byte)'^'))
        {
            diagnosticReporter.GenerateError(ErrorType.UnsupportedMVPExpression, expression.Span);
            return false;
        }

        byte operation = spelling[0];

        if (!TryEvaluate(expression.Left, requiresInt32, diagnosticReporter, cancellationToken, out BigInteger left)
            || !TryEvaluate(expression.Right, requiresInt32, diagnosticReporter, cancellationToken, out BigInteger right))
        {
            return false;
        }

        if (operation is (byte)'&' or (byte)'|' or (byte)'^'
            && requiresInt32
            && (!RequireInt32(left, expression.Left.Span, diagnosticReporter)
                || !RequireInt32(right, expression.Right.Span, diagnosticReporter)))
        {
            return false;
        }

        if (right.IsZero && operation is (byte)'/' or (byte)'%')
        {
            diagnosticReporter.GenerateError(ErrorType.IntegerDivisionByZero, token.Span);
            return false;
        }

        value = operation switch
        {
            (byte)'+' => left + right,
            (byte)'-' => left - right,
            (byte)'*' => left * right,
            (byte)'/' => left / right,
            (byte)'%' => left % right,
            (byte)'&' => left & right,
            (byte)'|' => left | right,
            (byte)'^' => left ^ right,
            _ => throw new InvalidOperationException("Unexpected binary integer operator.")
        };

        return true;
    }

    private static bool TryEvaluateShift(BinaryExpressionSyntax expression, bool leftShift, bool requiresInt32, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken, out BigInteger value)
    {
        value = default;

        if (!TryEvaluate(expression.Left, requiresInt32, diagnosticReporter, cancellationToken, out BigInteger left)
            || !TryEvaluate(expression.Right, requiresInt32: false, diagnosticReporter, cancellationToken, out BigInteger count))
        {
            return false;
        }

        if (requiresInt32 && !RequireInt32(left, expression.Left.Span, diagnosticReporter))
        {
            return false;
        }

        if (count.Sign < 0 || (requiresInt32 && count >= Int32Width))
        {
            diagnosticReporter.GenerateError(ErrorType.InvalidIntegerShiftCount, expression.Right.Span);
            return false;
        }

        if (count > int.MaxValue)
        {
            if (leftShift && left.IsZero)
            {
                value = BigInteger.Zero;
                return true;
            }

            if (!leftShift)
            {
                value = left.Sign < 0 ? BigInteger.MinusOne : BigInteger.Zero;
                return true;
            }

            diagnosticReporter.GenerateError(ErrorType.UnsupportedMVPExpression, expression.Span);
            return false;
        }

        int shiftCount = (int)count;
        value = leftShift ? left << shiftCount : left >> shiftCount;

        return !requiresInt32 || !leftShift || RequireInt32(value, expression.Span, diagnosticReporter);
    }

    private static bool RequireInt32(BigInteger value, SourceSpan span, IDiagnosticReporter diagnosticReporter)
    {
#pragma warning disable IDE0078 // Use pattern matching
        if (value < int.MinValue || value > int.MaxValue)
        {
            diagnosticReporter.GenerateError(ErrorType.IntegerConstantOutOfRange, span);
            return false;
        }
#pragma warning restore IDE0078 // Use pattern matching

        return true;
    }

    private static bool TryGetSingleByteOperator(SyntaxToken token, out byte spelling)
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
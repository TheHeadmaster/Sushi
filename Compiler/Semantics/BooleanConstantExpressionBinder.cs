using System.Numerics;
using System.Text;
using Sushi.Diagnostics;
using Sushi.Parsing.Syntax;

namespace Sushi.Semantics;

/// <summary>
/// Evaluates supported Boolean constant expressions, including comparisons
/// of exact integer constants, without introducing implicit integer conversions.
/// </summary>
internal static class BooleanConstantExpressionBinder
{
    /// <summary>
    /// Binds a Boolean constant expression in a bool result context.
    /// </summary>
    /// <param name="expression">
    /// The expression to evaluate.
    /// </param>
    /// <param name="diagnosticReporter">
    /// Receives diagnostics for invalid or unsupported expressions.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels recursive evaluation.
    /// </param>
    /// <returns>
    /// The concrete Boolean constant, or null when binding fails.
    /// </returns>
    public static BoundBooleanConstantExpression? Bind(ExpressionSyntax expression, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);

        if (ContainsInvalidComparisonChain(expression))
        {
            return null;
        }

        return TryEvaluate(expression, diagnosticReporter, cancellationToken, out bool value)
            ? new BoundBooleanConstantExpression(value)
            : null;
    }

    private static bool TryEvaluate(ExpressionSyntax expression, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken, out bool value)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (expression)
        {
            case BooleanLiteralExpressionSyntax literal:
                value = IsTrueLiteral(literal);
                return true;

            case ParenthesizedExpressionSyntax grouped when grouped.OpenParenthesisToken.IsSourceBacked && grouped.CloseParenthesisToken.IsSourceBacked:
                return TryEvaluate(grouped.Expression, diagnosticReporter, cancellationToken, out value);

            case ParenthesizedExpressionSyntax:
            case IntegerLiteralExpressionSyntax { LiteralToken.IsMissing: true }:
                value = default;
                return false;

            case UnaryExpressionSyntax unary when unary.OperatorToken.Type is SyntaxType.NotKeyword:
                if (!TryEvaluate(unary.Operand, diagnosticReporter, cancellationToken, out bool operand))
                {
                    value = default;
                    return false;
                }

                value = !operand;
                return true;

            case BinaryExpressionSyntax comparison when TryGetIntegerComparisonOperator(comparison.OperatorToken, out string operation):
                return TryEvaluateIntegerComparison(comparison, operation, diagnosticReporter, cancellationToken, out value);

            default:
                diagnosticReporter.GenerateError(ErrorType.UnsupportedMVPExpression, expression.Span);
                value = default;
                return false;
        }
    }

    private static bool TryEvaluateIntegerComparison(BinaryExpressionSyntax expression, string operation, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken, out bool value)
    {
        value = default;

        if (!IntegerConstantExpressionBinder.TryEvaluateUntyped(expression.Left, diagnosticReporter, cancellationToken, out BigInteger left)
            || !IntegerConstantExpressionBinder.TryEvaluateUntyped(expression.Right, diagnosticReporter, cancellationToken, out BigInteger right))
        {
            return false;
        }

        value = operation switch
        {
            "==" => left == right,
            "!=" => left != right,
            "<" => left < right,
            "<=" => left <= right,
            ">" => left > right,
            ">=" => left >= right,
            _ => throw new InvalidOperationException("Unexpected integer comparison operator.")
        };

        return true;
    }

    private static bool TryGetIntegerComparisonOperator(SyntaxToken token, out string operation)
    {
        operation = string.Empty;

        if (token.Type is not SyntaxType.OperatorToken || !token.IsSourceBacked)
        {
            return false;
        }

        Source.SourceSpan span = token.Span;
        operation = Encoding.UTF8.GetString(span.Snapshot.Bytes.Span[span.Start..span.End]);

        return operation is "==" or "!=" or "<" or "<=" or ">" or ">=";
    }

    private static bool ContainsInvalidComparisonChain(ExpressionSyntax expression)
        => expression switch
        {
            InvalidComparisonChainSyntax => true,
            ParenthesizedExpressionSyntax grouped => ContainsInvalidComparisonChain(grouped.Expression),
            UnaryExpressionSyntax unary => ContainsInvalidComparisonChain(unary.Operand),
            BinaryExpressionSyntax binary => ContainsInvalidComparisonChain(binary.Left) || ContainsInvalidComparisonChain(binary.Right),
            _ => false
        };

    private static bool IsTrueLiteral(BooleanLiteralExpressionSyntax expression)
    {
        SyntaxToken token = expression.LiteralToken;
        return token.Span.Snapshot.Bytes.Span[token.Span.Start..token.Span.End].SequenceEqual("true"u8);
    }
}
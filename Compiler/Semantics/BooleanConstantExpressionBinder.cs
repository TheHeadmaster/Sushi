using System.Numerics;
using System.Text;
using Sushi.Diagnostics;
using Sushi.Parsing.Syntax;

namespace Sushi.Semantics;

/// <summary>
/// Validates and folds supported Boolean constant expressions, preserving the
/// distinction between short-circuiting logical operators and eager Boolean operators.
/// </summary>
internal static class BooleanConstantExpressionBinder
{
    private enum BooleanBinaryOperator
    {
        LogicalAnd,
        LogicalOr,
        EagerAnd,
        EagerOr,
        EagerXor
    }

    /// <summary>
    /// Binds a Boolean constant expression in a bool result context.
    /// Both operands of a binary expression remain subject to static validation,
    /// regardless of whether the result can be determined from the left operand.
    /// </summary>
    /// <param name="expression">
    /// The expression to bind.
    /// </param>
    /// <param name="diagnosticReporter">
    /// Receives semantic and constant-evaluation diagnostics.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels recursive binding.
    /// </param>
    /// <returns>
    /// The concrete Boolean constant, or null when binding fails.
    /// </returns>
    public static BoundBooleanConstantExpression? Bind(ExpressionSyntax expression, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);

        cancellationToken.ThrowIfCancellationRequested();

        if (ContainsInvalidComparisonChain(expression))
        {
            return null;
        }

        Dictionary<BinaryExpressionSyntax, bool> comparisonValues = [];

        if (!TryValidate(expression, comparisonValues, diagnosticReporter, cancellationToken))
        {
            return null;
        }

        return new BoundBooleanConstantExpression(Fold(expression, comparisonValues, cancellationToken));
    }

    private static bool TryValidate(ExpressionSyntax expression, Dictionary<BinaryExpressionSyntax, bool> comparisonValues, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (expression)
        {
            case BooleanLiteralExpressionSyntax:
                return true;

            case ParenthesizedExpressionSyntax grouped when grouped.OpenParenthesisToken.IsSourceBacked && grouped.CloseParenthesisToken.IsSourceBacked:
                return TryValidate(grouped.Expression, comparisonValues, diagnosticReporter, cancellationToken);

            case ParenthesizedExpressionSyntax:
            case IntegerLiteralExpressionSyntax { LiteralToken.IsMissing: true }:
                return false;

            case UnaryExpressionSyntax unary when unary.OperatorToken.Type is SyntaxType.NotKeyword:
                return TryValidate(unary.Operand, comparisonValues, diagnosticReporter, cancellationToken);

            case BinaryExpressionSyntax binary when TryGetBooleanBinaryOperator(binary.OperatorToken, out _):
                bool leftValid = TryValidate(binary.Left, comparisonValues, diagnosticReporter, cancellationToken);
                bool rightValid = TryValidate(binary.Right, comparisonValues, diagnosticReporter, cancellationToken);
                return leftValid && rightValid;

            case BinaryExpressionSyntax comparison when TryGetIntegerComparisonOperator(comparison.OperatorToken, out string operation):
                if (!TryEvaluateIntegerComparison(comparison, operation, diagnosticReporter, cancellationToken, out bool result))
                {
                    return false;
                }

                comparisonValues.Add(comparison, result);
                return true;

            default:
                diagnosticReporter.GenerateError(ErrorType.UnsupportedMVPExpression, expression.Span);
                return false;
        }
    }

    private static bool Fold(ExpressionSyntax expression, Dictionary<BinaryExpressionSyntax, bool> comparisonValues, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return expression switch
        {
            BooleanLiteralExpressionSyntax literal => IsTrueLiteral(literal),
            ParenthesizedExpressionSyntax grouped => Fold(grouped.Expression, comparisonValues, cancellationToken),
            UnaryExpressionSyntax unary when unary.OperatorToken.Type is SyntaxType.NotKeyword => !Fold(unary.Operand, comparisonValues, cancellationToken),
            BinaryExpressionSyntax binary => FoldBinary(binary, comparisonValues, cancellationToken),
            _ => throw new InvalidOperationException("A Boolean expression passed validation without a supported folding operation.")
        };
    }

    private static bool FoldBinary(BinaryExpressionSyntax expression, Dictionary<BinaryExpressionSyntax, bool> comparisonValues, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryGetBooleanBinaryOperator(expression.OperatorToken, out BooleanBinaryOperator operation))
        {
            return comparisonValues[expression];
        }

        return operation switch
        {
            BooleanBinaryOperator.LogicalAnd => Fold(expression.Left, comparisonValues, cancellationToken) && Fold(expression.Right, comparisonValues, cancellationToken),
            BooleanBinaryOperator.LogicalOr => Fold(expression.Left, comparisonValues, cancellationToken) || Fold(expression.Right, comparisonValues, cancellationToken),
            BooleanBinaryOperator.EagerAnd => Fold(expression.Left, comparisonValues, cancellationToken) & Fold(expression.Right, comparisonValues, cancellationToken),
            BooleanBinaryOperator.EagerOr => Fold(expression.Left, comparisonValues, cancellationToken) | Fold(expression.Right, comparisonValues, cancellationToken),
            BooleanBinaryOperator.EagerXor => Fold(expression.Left, comparisonValues, cancellationToken) ^ Fold(expression.Right, comparisonValues, cancellationToken),
            _ => throw new InvalidOperationException("Unexpected Boolean binary operator.")
        };
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

    private static bool TryGetBooleanBinaryOperator(SyntaxToken token, out BooleanBinaryOperator operation)
    {
        operation = default;

        if (!token.IsSourceBacked)
        {
            return false;
        }

        if (token.Type is SyntaxType.AndKeyword)
        {
            operation = BooleanBinaryOperator.LogicalAnd;
            return true;
        }

        if (token.Type is SyntaxType.OrKeyword)
        {
            operation = BooleanBinaryOperator.LogicalOr;
            return true;
        }

        if (token.Type is not SyntaxType.OperatorToken || token.Span.Length != 1)
        {
            return false;
        }

        byte spelling = token.Span.Snapshot.Bytes.Span[token.Span.Start];

        switch (spelling)
        {
            case (byte)'&':
                operation = BooleanBinaryOperator.EagerAnd;
                return true;

            case (byte)'|':
                operation = BooleanBinaryOperator.EagerOr;
                return true;

            case (byte)'^':
                operation = BooleanBinaryOperator.EagerXor;
                return true;

            default:
                return false;
        }
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
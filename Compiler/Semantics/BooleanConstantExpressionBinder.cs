using Sushi.Diagnostics;
using Sushi.Parsing.Syntax;

namespace Sushi.Semantics;

/// <summary>
/// Evaluates supported Boolean constant expressions without introducing
/// implicit integer conversions or runtime Boolean evaluation.
/// </summary>
internal static class BooleanConstantExpressionBinder
{
    /// <summary>
    /// Binds a Boolean constant expression in a bool result context.
    /// </summary>
    /// <param name="expression">
    /// The parsed expression to evaluate.
    /// </param>
    /// <param name="diagnosticReporter">
    /// Receives diagnostics for expression forms outside the executable MVP.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel recursive evaluation.
    /// </param>
    /// <returns>
    /// The bound Boolean constant, or null when binding fails.
    /// </returns>
    public static BoundBooleanConstantExpression? Bind(ExpressionSyntax expression, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);

        return TryEvaluate(expression, diagnosticReporter, cancellationToken, out bool value) ? new BoundBooleanConstantExpression(value) : null;
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

            default:
                diagnosticReporter.GenerateError(ErrorType.UnsupportedMVPExpression, expression.Span);
                value = default;
                return false;
        }
    }

    private static bool IsTrueLiteral(BooleanLiteralExpressionSyntax expression)
    {
        SyntaxToken token = expression.LiteralToken;
        return token.Span.Snapshot.Bytes.Span[token.Span.Start..token.Span.End].SequenceEqual("true"u8);
    }
}
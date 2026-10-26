using System.Numerics;
using System.Text;
using Sushi.Diagnostics;
using Sushi.Parsing.Syntax;

namespace Sushi.Semantics;

/// <summary>
/// Binds Boolean expressions into typed operations while evaluating integer
/// comparisons under Sushi's exact-untyped integer semantics.
/// </summary>
internal static class BooleanExpressionBinder
{
    /// <summary>
    /// Binds a Boolean expression against the enclosing Boolean result context.
    /// All syntactically present operands are statically validated, including
    /// operands that would be skipped by runtime short-circuit evaluation.
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
    /// The typed expression, or null when binding fails.
    /// </returns>
    public static BoundExpression? Bind(ExpressionSyntax expression, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);

        cancellationToken.ThrowIfCancellationRequested();

        return Bind(expression, [], diagnosticReporter, cancellationToken);
    }

    /// <summary>
    /// Binds an expression with the named parameters of its enclosing function.
    /// </summary>
    /// <param name="expression">
    /// The Boolean expression.
    /// </param>
    /// <param name="parameters">
    /// The declared parameters in positional order.
    /// </param>
    /// <param name="diagnosticReporter">
    /// Receives semantic diagnostics.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels recursive binding.
    /// </param>
    /// <returns>
    /// The typed Boolean expression, or null after a diagnostic.
    /// </returns>
    internal static BoundExpression? Bind(ExpressionSyntax expression, IReadOnlyList<BoundParameter> parameters, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);
        cancellationToken.ThrowIfCancellationRequested();

        return ContainsInvalidComparisonChain(expression)
            ? null
            : BindExpression(expression, parameters, diagnosticReporter, cancellationToken);
    }

    private static BoundExpression? BindExpression(ExpressionSyntax expression, IReadOnlyList<BoundParameter> parameters, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (expression)
        {
            case BooleanLiteralExpressionSyntax literal:
                return new BoundBooleanConstantExpression(IsTrueLiteral(literal));

            case IdentifierExpressionSyntax identifier:
                return BindParameterReference(identifier, parameters, diagnosticReporter);

            case ParenthesizedExpressionSyntax grouped when grouped.OpenParenthesisToken.IsSourceBacked && grouped.CloseParenthesisToken.IsSourceBacked:
                return BindExpression(grouped.Expression, parameters, diagnosticReporter, cancellationToken);

            case ParenthesizedExpressionSyntax:
            case IntegerLiteralExpressionSyntax { LiteralToken.IsMissing: true }:
                return null;

            case UnaryExpressionSyntax unary when unary.OperatorToken.Type is SyntaxType.NotKeyword:
                return BindNegation(unary, parameters, diagnosticReporter, cancellationToken);

            case BinaryExpressionSyntax binary when TryGetBooleanBinaryOperator(binary.OperatorToken, out BooleanBinaryOperator operation):
                return BindBooleanBinary(binary, operation, parameters, diagnosticReporter, cancellationToken);

            case BinaryExpressionSyntax comparison when TryGetIntegerComparisonOperator(comparison.OperatorToken, out string comparisonOperator):
                return TryEvaluateIntegerComparison(comparison, comparisonOperator, diagnosticReporter, cancellationToken, out bool result)
                    ? new BoundBooleanConstantExpression(result)
                    : null;

            default:
                diagnosticReporter.GenerateError(ErrorType.UnsupportedMVPExpression, expression.Span);
                return null;
        }
    }

    private static BoundExpression? BindParameterReference(IdentifierExpressionSyntax expression, IReadOnlyList<BoundParameter> parameters, IDiagnosticReporter diagnosticReporter)
    {
        string? name = IdentifierBinder.Bind(expression.IdentifierToken);
        BoundParameter? parameter = name is null ? null : parameters.FirstOrDefault(candidate => StringComparer.Ordinal.Equals(candidate.Name, name));

        if (parameter is not null)
        {
            return new BoundParameterReferenceExpression(parameter);
        }

        diagnosticReporter.GenerateError(ErrorType.UnknownIdentifier, expression.Span);
        return null;
    }

    private static BoundExpression? BindNegation(UnaryExpressionSyntax expression, IReadOnlyList<BoundParameter> parameters, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        BoundExpression? operand = BindExpression(expression.Operand, parameters, diagnosticReporter, cancellationToken);
        return operand is null ? null : new BoundBooleanNotExpression(operand);
    }

    private static BoundExpression? BindBooleanBinary(BinaryExpressionSyntax expression, BooleanBinaryOperator operation, IReadOnlyList<BoundParameter> parameters, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        BoundExpression? left = BindExpression(expression.Left, parameters, diagnosticReporter, cancellationToken);
        BoundExpression? right = BindExpression(expression.Right, parameters, diagnosticReporter, cancellationToken);

        return left is null || right is null ? null : new BoundBooleanBinaryExpression(operation, left, right);
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
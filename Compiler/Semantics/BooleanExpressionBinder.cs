using System.Numerics;
using System.Text;
using Sushi.Diagnostics;
using Sushi.Parsing.Syntax;

namespace Sushi.Semantics;

/// <summary>
/// Binds Boolean expressions, preserving exact integer comparisons, runtime
/// comparisons, short-circuit evaluation, and references to initialized locals.
/// </summary>
internal static class BooleanExpressionBinder
{
    /// <summary>
    /// Binds a Boolean expression without an enclosing parameter or call scope.
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
    /// The Boolean expression, or null when binding fails.
    /// </returns>
    public static BoundExpression? Bind(ExpressionSyntax expression, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);
        cancellationToken.ThrowIfCancellationRequested();

        return Bind(expression, [], diagnosticReporter, cancellationToken);
    }

    /// <summary>
    /// Binds a Boolean expression using enclosing parameters, locals, and
    /// package-call contexts while validating syntactically present operands.
    /// </summary>
    /// <param name="expression">
    /// The parsed Boolean expression.
    /// </param>
    /// <param name="parameters">
    /// Parameters of the enclosing callable.
    /// </param>
    /// <param name="diagnosticReporter">
    /// Receives binding diagnostics.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels recursive binding.
    /// </param>
    /// <param name="callables">
    /// Optional package functions available to direct calls.
    /// </param>
    /// <param name="locals">
    /// Optional local-binding context shared with the enclosing function.
    /// </param>
    /// <returns>
    /// The typed expression, or null on failure.
    /// </returns>
    internal static BoundExpression? Bind(
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
        cancellationToken.ThrowIfCancellationRequested();

        return ContainsInvalidComparisonChain(expression)
            ? null
            : BindExpression(expression, parameters, diagnosticReporter, callables, locals, cancellationToken);
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

        switch (expression)
        {
            case BooleanLiteralExpressionSyntax literal:
                return new BoundBooleanConstantExpression(IsTrueLiteral(literal));

            case IdentifierExpressionSyntax identifier:
                return BindParameterReference(identifier, parameters, diagnosticReporter, locals);

            case InvocationExpressionSyntax invocation when callables is not null:
                return PackageInvocationBinder.BindValue(invocation, BoundType.Bool, callables, parameters, diagnosticReporter, cancellationToken, locals);

            case ParenthesizedExpressionSyntax grouped when grouped.OpenParenthesisToken.IsSourceBacked && grouped.CloseParenthesisToken.IsSourceBacked:
                return BindExpression(grouped.Expression, parameters, diagnosticReporter, callables, locals, cancellationToken);

            case ParenthesizedExpressionSyntax:
            case IntegerLiteralExpressionSyntax { LiteralToken.IsMissing: true }:
                return null;

            case UnaryExpressionSyntax unary when unary.OperatorToken.Type is SyntaxType.NotKeyword:
                return BindNegation(unary, parameters, diagnosticReporter, callables, locals, cancellationToken);

            case BinaryExpressionSyntax binary when TryGetBooleanBinaryOperator(binary.OperatorToken, out BooleanBinaryOperator operation):
                return BindBooleanBinary(binary, operation, parameters, diagnosticReporter, callables, locals, cancellationToken);

            case BinaryExpressionSyntax comparison when TryGetIntegerComparisonOperator(comparison.OperatorToken, out IntegerComparisonOperator comparisonOperator):
                return BindIntegerComparison(comparison, comparisonOperator, parameters, diagnosticReporter, callables, locals, cancellationToken);

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
        BoundExpression? reference = name is null ? null : locals?.Resolve(name, expression.IdentifierToken.Span);

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

        if (reference.Type is not BoundType.Bool)
        {
            diagnosticReporter.GenerateError(ErrorType.UnsupportedMVPExpression, expression.Span);
            return null;
        }

        return reference;
    }

    private static BoundExpression? BindNegation(
        UnaryExpressionSyntax expression,
        IReadOnlyList<BoundParameter> parameters,
        IDiagnosticReporter diagnosticReporter,
        IReadOnlyList<PackageCallable>? callables,
        LocalBindingContext? locals,
        CancellationToken cancellationToken)
    {
        BoundExpression? operand = BindExpression(expression.Operand, parameters, diagnosticReporter, callables, locals, cancellationToken);
        return operand is null ? null : new BoundBooleanNotExpression(operand);
    }

    private static BoundExpression? BindBooleanBinary(
        BinaryExpressionSyntax expression,
        BooleanBinaryOperator operation,
        IReadOnlyList<BoundParameter> parameters,
        IDiagnosticReporter diagnosticReporter,
        IReadOnlyList<PackageCallable>? callables,
        LocalBindingContext? locals,
        CancellationToken cancellationToken)
    {
        BoundExpression? left = BindExpression(expression.Left, parameters, diagnosticReporter, callables, locals, cancellationToken);
        BoundExpression? right = BindExpression(expression.Right, parameters, diagnosticReporter, callables, locals, cancellationToken);

        return left is null || right is null ? null : new BoundBooleanBinaryExpression(operation, left, right);
    }

    private static BoundExpression? BindIntegerComparison(
        BinaryExpressionSyntax expression,
        IntegerComparisonOperator operation,
        IReadOnlyList<BoundParameter> parameters,
        IDiagnosticReporter diagnosticReporter,
        IReadOnlyList<PackageCallable>? callables,
        LocalBindingContext? locals,
        CancellationToken cancellationToken)
    {
        bool runtimeLeft = ContainsRuntimeValue(expression.Left);
        bool runtimeRight = ContainsRuntimeValue(expression.Right);

        if (!runtimeLeft && !runtimeRight)
        {
            return TryEvaluateIntegerComparison(expression, operation, diagnosticReporter, cancellationToken, out bool result)
                ? new BoundBooleanConstantExpression(result)
                : null;
        }

        if (runtimeLeft && runtimeRight)
        {
            BoundExpression? left = IntegerExpressionBinder.Bind(expression.Left, parameters, diagnosticReporter, cancellationToken, callables, locals);
            BoundExpression? right = IntegerExpressionBinder.Bind(expression.Right, parameters, diagnosticReporter, cancellationToken, callables, locals);

            return left is null || right is null ? null : new BoundIntegerComparisonExpression(operation, left, right);
        }

        return BindComparisonWithExactConstant(expression, operation, runtimeLeft, parameters, diagnosticReporter, callables, locals, cancellationToken);
    }

    private static BoundExpression? BindComparisonWithExactConstant(
        BinaryExpressionSyntax expression,
        IntegerComparisonOperator operation,
        bool runtimeLeft,
        IReadOnlyList<BoundParameter> parameters,
        IDiagnosticReporter diagnosticReporter,
        IReadOnlyList<PackageCallable>? callables,
        LocalBindingContext? locals,
        CancellationToken cancellationToken)
    {
        ExpressionSyntax runtimeSyntax = runtimeLeft ? expression.Left : expression.Right;
        ExpressionSyntax exactSyntax = runtimeLeft ? expression.Right : expression.Left;

        BoundExpression? operand = IntegerExpressionBinder.Bind(runtimeSyntax, parameters, diagnosticReporter, cancellationToken, callables, locals);
        bool validConstant = IntegerConstantExpressionBinder.TryEvaluateUntyped(exactSyntax, diagnosticReporter, cancellationToken, out BigInteger exact);

        if (operand is null || !validConstant)
        {
            return null;
        }

#pragma warning disable IDE0078 // Use pattern matching
        if (exact >= int.MinValue && exact <= int.MaxValue)
        {
            BoundIntegerLiteralExpression constant = new((int)exact);

            return runtimeLeft
                ? new BoundIntegerComparisonExpression(operation, operand, constant)
                : new BoundIntegerComparisonExpression(operation, constant, operand);
        }
#pragma warning restore IDE0078 // Use pattern matching

        // Out-of-range exact constants have a predetermined comparison result,
        // but the runtime operand must still execute before producing that result.
        BigInteger boundary = exact > int.MaxValue ? int.MaxValue : int.MinValue;
        bool result = runtimeLeft
            ? EvaluateIntegerComparison(operation, boundary, exact)
            : EvaluateIntegerComparison(operation, exact, boundary);

        return new BoundIntegerRangeComparisonExpression(operand, result);
    }

    private static bool TryEvaluateIntegerComparison(
        BinaryExpressionSyntax expression,
        IntegerComparisonOperator operation,
        IDiagnosticReporter diagnosticReporter,
        CancellationToken cancellationToken,
        out bool value)
    {
        value = default;

        if (!IntegerConstantExpressionBinder.TryEvaluateUntyped(expression.Left, diagnosticReporter, cancellationToken, out BigInteger left)
            || !IntegerConstantExpressionBinder.TryEvaluateUntyped(expression.Right, diagnosticReporter, cancellationToken, out BigInteger right))
        {
            return false;
        }

        value = EvaluateIntegerComparison(operation, left, right);
        return true;
    }

    private static bool EvaluateIntegerComparison(IntegerComparisonOperator operation, BigInteger left, BigInteger right)
        => operation switch
        {
            IntegerComparisonOperator.Equal => left == right,
            IntegerComparisonOperator.NotEqual => left != right,
            IntegerComparisonOperator.LessThan => left < right,
            IntegerComparisonOperator.LessThanOrEqual => left <= right,
            IntegerComparisonOperator.GreaterThan => left > right,
            IntegerComparisonOperator.GreaterThanOrEqual => left >= right,
            _ => throw new InvalidOperationException("Unexpected integer comparison operator.")
        };

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

        if (token.Type is not SyntaxType.OperatorToken || !token.IsSourceBacked || token.Span.Length != 1)
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

    private static bool TryGetIntegerComparisonOperator(SyntaxToken token, out IntegerComparisonOperator operation)
    {
        operation = default;

        if (token.Type is not SyntaxType.OperatorToken || !token.IsSourceBacked)
        {
            return false;
        }

        Source.SourceSpan span = token.Span;
        string spelling = Encoding.UTF8.GetString(span.Snapshot.Bytes.Span[span.Start..span.End]);

        switch (spelling)
        {
            case "==":
                operation = IntegerComparisonOperator.Equal;
                return true;

            case "!=":
                operation = IntegerComparisonOperator.NotEqual;
                return true;

            case "<":
                operation = IntegerComparisonOperator.LessThan;
                return true;

            case "<=":
                operation = IntegerComparisonOperator.LessThanOrEqual;
                return true;

            case ">":
                operation = IntegerComparisonOperator.GreaterThan;
                return true;

            case ">=":
                operation = IntegerComparisonOperator.GreaterThanOrEqual;
                return true;

            default:
                return false;
        }
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

    private static bool ContainsInvalidComparisonChain(ExpressionSyntax expression)
        => expression switch
        {
            InvalidComparisonChainSyntax => true,
            ParenthesizedExpressionSyntax grouped => ContainsInvalidComparisonChain(grouped.Expression),
            UnaryExpressionSyntax unary => ContainsInvalidComparisonChain(unary.Operand),
            IncrementDecrementExpressionSyntax update => ContainsInvalidComparisonChain(update.Operand),
            BinaryExpressionSyntax binary => ContainsInvalidComparisonChain(binary.Left) || ContainsInvalidComparisonChain(binary.Right),
            InvocationExpressionSyntax invocation => invocation.Arguments.Any(argument => ContainsInvalidComparisonChain(argument.Expression)),
            _ => false
        };

    private static bool IsTrueLiteral(BooleanLiteralExpressionSyntax expression)
    {
        SyntaxToken token = expression.LiteralToken;
        return token.Span.Snapshot.Bytes.Span[token.Span.Start..token.Span.End].SequenceEqual("true"u8);
    }
}
using Sushi.Diagnostics;
using Sushi.Parsing.Syntax;

namespace Sushi.Semantics;

/// <summary>
/// Resolves direct package-function invocations without introducing incomplete
/// overload-ranking rules. Named arguments bind statically while retaining
/// their original evaluation order.
/// </summary>
internal static class PackageInvocationBinder
{
    /// <summary>
    /// Binds an invocation used as a standalone statement, requiring a void result.
    /// </summary>
    /// <param name="statement">
    /// The complete expression statement.
    /// </param>
    /// <param name="callables">
    /// Candidate package-level functions in the current resolution scope.
    /// </param>
    /// <param name="parameters">
    /// Parameters available in the caller.
    /// </param>
    /// <param name="diagnosticReporter">
    /// Receives binding errors.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels recursive binding.
    /// </param>
    /// <param name="locals">
    /// Optional lexical bindings visible at the invocation site.
    /// </param>
    /// <returns>
    /// A void call statement, or null following a binding failure.
    /// </returns>
    public static BoundVoidCallStatement? Bind(
        ExpressionStatementSyntax statement,
        IReadOnlyList<PackageCallable> callables,
        IReadOnlyList<BoundParameter> parameters,
        IDiagnosticReporter diagnosticReporter,
        CancellationToken cancellationToken,
        LocalBindingContext? locals = null)
    {
        ArgumentNullException.ThrowIfNull(statement);

        if (!statement.SemicolonToken.IsSourceBacked || statement.Expression is not InvocationExpressionSyntax invocation)
        {
            ReportError(statement, "Only a void invocation may appear as an expression statement in the current subset.", diagnosticReporter);
            return null;
        }

        ResolvedCall? resolved = Resolve(invocation, callables, parameters, diagnosticReporter, locals, cancellationToken);

        if (resolved is null)
        {
            return null;
        }

        if (resolved.Callable.Function.ReturnType is not BoundType.Void)
        {
            ReportError(statement, "A value-producing invocation cannot stand alone as a statement.", diagnosticReporter);
            return null;
        }

        return new BoundVoidCallStatement(
            resolved.Callable.Symbol,
            [.. resolved.Callable.Function.Parameters.Select(parameter => parameter.Type)],
            resolved.Arguments);
    }

    /// <summary>
    /// Binds an invocation in a context requiring a concrete result type.
    /// The expected type validates the selected callable but does not select
    /// between competing overloads.
    /// </summary>
    /// <param name="invocation">
    /// The parsed invocation expression.
    /// </param>
    /// <param name="expectedType">
    /// The required result type of the enclosing expression.
    /// </param>
    /// <param name="callables">
    /// Candidate package-level functions.
    /// </param>
    /// <param name="parameters">
    /// Parameters available within the caller.
    /// </param>
    /// <param name="diagnosticReporter">
    /// Receives binding errors.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels recursive binding.
    /// </param>
    /// <param name="locals">
    /// Optional lexical bindings visible at the invocation site.
    /// </param>
    /// <returns>
    /// The typed call expression, or null when resolution fails.
    /// </returns>
    public static BoundCallExpression? BindValue(
        InvocationExpressionSyntax invocation,
        BoundType expectedType,
        IReadOnlyList<PackageCallable> callables,
        IReadOnlyList<BoundParameter> parameters,
        IDiagnosticReporter diagnosticReporter,
        CancellationToken cancellationToken,
        LocalBindingContext? locals = null)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        ResolvedCall? resolved = Resolve(invocation, callables, parameters, diagnosticReporter, locals, cancellationToken);

        if (resolved is null)
        {
            return null;
        }

        BoundType actualType = resolved.Callable.Function.ReturnType;

        if (actualType is BoundType.Void || actualType != expectedType)
        {
            ReportError(invocation, "The selected callable does not produce the required value type.", diagnosticReporter);
            return null;
        }

        return new BoundCallExpression(
            resolved.Callable.Symbol,
            actualType,
            [.. resolved.Callable.Function.Parameters.Select(parameter => parameter.Type)],
            resolved.Arguments);
    }

    private static ResolvedCall? Resolve(
        InvocationExpressionSyntax invocation,
        IReadOnlyList<PackageCallable> callables,
        IReadOnlyList<BoundParameter> parameters,
        IDiagnosticReporter diagnosticReporter,
        LocalBindingContext? locals,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(callables);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);
        cancellationToken.ThrowIfCancellationRequested();

        if (invocation.Target is not IdentifierExpressionSyntax target
            || !invocation.OpenParenthesisToken.IsSourceBacked
            || !invocation.CloseParenthesisToken.IsSourceBacked
            || invocation.ArgumentSeparators.Any(separator => !separator.IsSourceBacked))
        {
            ReportError(invocation, "The invocation contains unsupported or recovered target syntax.", diagnosticReporter);
            return null;
        }

        string? name = IdentifierBinder.Bind(target.IdentifierToken);

        if (name is null)
        {
            ReportError(invocation, "The invocation target must be a source-backed identifier.", diagnosticReporter);
            return null;
        }

        PackageCallable[] candidates = [.. callables.Where(candidate => StringComparer.Ordinal.Equals(candidate.Name, name))];

        if (candidates.Length == 0)
        {
            ReportError(invocation, $"Package function \"{name}\" is not available in this scope.", diagnosticReporter);
            return null;
        }

        if (candidates.Length > 1)
        {
            ReportError(invocation, $"Function \"{name}\" requires overload resolution, which is not supported by this slice.", diagnosticReporter);
            return null;
        }

        PackageCallable selected = candidates[0];
        BoundParameter[] targetParameters = [.. selected.Function.Parameters];
        bool[] supplied = new bool[targetParameters.Length];
        List<BoundCallArgument> arguments = [];

        bool valid = true;
        bool namedSeen = false;
        int nextPositional = 0;

        foreach (CallArgumentSyntax argument in invocation.Arguments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int parameterIndex;

            if (argument.IsNamed)
            {
                namedSeen = true;
                string? label = argument.Name is SyntaxToken token ? IdentifierBinder.Bind(token) : null;
                parameterIndex = Array.FindIndex(targetParameters, candidate => StringComparer.Ordinal.Equals(candidate.Name, label));
            }
            else
            {
                parameterIndex = nextPositional++;

                if (namedSeen)
                {
                    ReportError(argument, "Positional arguments cannot follow named arguments.", diagnosticReporter);
                    valid = false;
                    continue;
                }
            }

            if (parameterIndex < 0 || parameterIndex >= targetParameters.Length)
            {
                ReportError(argument, "The argument does not identify a parameter of the selected callable.", diagnosticReporter);
                valid = false;
                continue;
            }

            if (supplied[parameterIndex])
            {
                ReportError(argument, "A callable parameter cannot be supplied more than once.", diagnosticReporter);
                valid = false;
                continue;
            }

            supplied[parameterIndex] = true;

            BoundType parameterType = targetParameters[parameterIndex].Type;
            BoundExpression? boundValue = parameterType switch
            {
                BoundType.Int32 => IntegerExpressionBinder.Bind(argument.Expression, parameters, diagnosticReporter, cancellationToken, callables, locals),
                BoundType.Bool => BooleanExpressionBinder.Bind(argument.Expression, parameters, diagnosticReporter, cancellationToken, callables, locals),
                _ => null
            };

            if (boundValue is null)
            {
                valid = false;
                continue;
            }

            if (boundValue.Type != parameterType)
            {
                ReportError(argument, "The argument type differs from the selected parameter type.", diagnosticReporter);
                valid = false;
                continue;
            }

            arguments.Add(new BoundCallArgument(parameterIndex, boundValue));
        }

        if (supplied.Any(value => !value))
        {
            ReportError(invocation, "Every required parameter must be supplied in the current invocation subset.", diagnosticReporter);
            valid = false;
        }

        if (!valid)
        {
            return null;
        }

        if (locals is not null)
        {
            locals.RecordCallableOccurrence(target.IdentifierToken.Span, selected);
            foreach (CallArgumentSyntax argument in invocation.Arguments)
            {
                if (argument.Name is not SyntaxToken label)
                {
                    continue;
                }

                string? parameterName = IdentifierBinder.Bind(label);
                BoundParameter? parameter = targetParameters.FirstOrDefault(item => StringComparer.Ordinal.Equals(item.Name, parameterName));
                if (parameter is not null)
                {
                    locals.RecordParameterOccurrence(label.Span, selected, parameter);
                }
            }
        }

        return new ResolvedCall(selected, arguments);
    }

    private static void ReportError(SyntaxNode node, string message, IDiagnosticReporter diagnosticReporter)
        => diagnosticReporter.GenerateErrorWithCustomMessage(ErrorType.UnsupportedMVPExpression, message, node.Span);

    private sealed record ResolvedCall(PackageCallable Callable, IReadOnlyList<BoundCallArgument> Arguments);
}
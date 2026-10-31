using Sushi.Diagnostics;
using Sushi.Parsing.Syntax;

namespace Sushi.Semantics;

/// <summary>
/// Resolves direct package-level void invocation statements against the callable
/// declarations available to the current executable root.
/// </summary>
internal static class PackageInvocationBinder
{
    /// <summary>
    /// Binds a direct invocation statement without introducing overload-ranking
    /// rules or relaxing the language's mandatory argument contracts.
    /// </summary>
    /// <param name="statement">
    /// The parsed expression statement.
    /// </param>
    /// <param name="callables">
    /// The package functions visible to this restricted compilation context.
    /// </param>
    /// <param name="parameters">
    /// The enclosing callable's own parameters.
    /// </param>
    /// <param name="diagnosticReporter">
    /// Receives static diagnostics.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels binding.
    /// </param>
    /// <returns>
    /// The bound call, or null when the invocation is unsupported or invalid.
    /// </returns>
    public static BoundVoidCallStatement? Bind(
        ExpressionStatementSyntax statement,
        IReadOnlyList<PackageCallable> callables,
        IReadOnlyList<BoundParameter> parameters,
        IDiagnosticReporter diagnosticReporter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(statement);
        ArgumentNullException.ThrowIfNull(callables);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);
        cancellationToken.ThrowIfCancellationRequested();

        if (!statement.SemicolonToken.IsSourceBacked
            || statement.Expression is not InvocationExpressionSyntax invocation
            || invocation.Target is not IdentifierExpressionSyntax target
            || !invocation.OpenParenthesisToken.IsSourceBacked
            || !invocation.CloseParenthesisToken.IsSourceBacked)
        {
            return ReportUnsupported(statement, "The current executable subset supports direct package-function invocation statements.");
        }

        string? name = IdentifierBinder.Bind(target.IdentifierToken);

        if (name is null)
        {
            return ReportUnsupported(statement, "The invocation target must be a source-backed identifier.");
        }

        PackageCallable[] candidates = [.. callables.Where(candidate => StringComparer.Ordinal.Equals(candidate.Name, name))];

        if (candidates.Length == 0)
        {
            return ReportUnsupported(statement, $"Package function \"{name}\" is not available in this invocation context.");
        }

        if (candidates.Length != 1)
        {
            return ReportUnsupported(statement, $"Function \"{name}\" requires overload resolution, which is outside the current executable subset.");
        }

        PackageCallable selected = candidates[0];

        if (selected.Function.ReturnType is not BoundType.Void)
        {
            return ReportUnsupported(statement, $"Invocation of \"{name}\" produces a value and cannot stand alone as a statement.");
        }

        BoundParameter[] targetParameters = [.. selected.Function.Parameters];
        bool[] supplied = new bool[targetParameters.Length];
        List<BoundCallArgument> arguments = [];
        bool namedSeen = false;
        bool valid = true;
        int nextPositional = 0;

        foreach (CallArgumentSyntax argument in invocation.Arguments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int parameterIndex;

            if (argument.IsNamed)
            {
                namedSeen = true;
                string? label = argument.Name is SyntaxToken token ? IdentifierBinder.Bind(token) : null;
                parameterIndex = Array.FindIndex(targetParameters, parameter => StringComparer.Ordinal.Equals(parameter.Name, label));
            }
            else
            {
                parameterIndex = nextPositional++;
                
                if (namedSeen)
                {
                    ReportArgumentError(argument, "Positional arguments cannot follow named arguments.", diagnosticReporter);
                    valid = false;
                    continue;
                }
            }

            if (parameterIndex < 0 || parameterIndex >= targetParameters.Length)
            {
                ReportArgumentError(argument, "The argument does not identify a parameter of the selected callable.", diagnosticReporter);
                valid = false;
                continue;
            }

            if (supplied[parameterIndex])
            {
                ReportArgumentError(argument, "A callable parameter cannot be supplied more than once.", diagnosticReporter);
                valid = false;
                continue;
            }

            supplied[parameterIndex] = true;
            BoundType requiredType = targetParameters[parameterIndex].Type;

            BoundExpression? expression = requiredType switch
            {
                BoundType.Int32 => IntegerExpressionBinder.Bind(argument.Expression, parameters, diagnosticReporter, cancellationToken),
                BoundType.Bool => BooleanExpressionBinder.Bind(argument.Expression, parameters, diagnosticReporter, cancellationToken),
                _ => null
            };

            if (expression is null || expression.Type != requiredType)
            {
                if (expression is not null)
                {
                    ReportArgumentError(argument, "The argument type does not match its declared parameter type.", diagnosticReporter);
                }

                valid = false;
                continue;
            }

            arguments.Add(new BoundCallArgument(parameterIndex, expression));
        }

        if (supplied.Any(isSupplied => !isSupplied))
        {
            diagnosticReporter.GenerateErrorWithCustomMessage(
                ErrorType.UnsupportedMVPExpression,
                $"Invocation of \"{name}\" must supply every required parameter in the current subset.",
                invocation.Span);

            valid = false;
        }

        return valid
            ? new BoundVoidCallStatement(selected.Symbol, [.. targetParameters.Select(parameter => parameter.Type)], arguments)
            : null;

        BoundVoidCallStatement? ReportUnsupported(ExpressionStatementSyntax invalid, string message)
        {
            diagnosticReporter.GenerateErrorWithCustomMessage(ErrorType.UnsupportedMVPExpression, message, invalid.Span);
            return null;
        }
    }

    private static void ReportArgumentError(CallArgumentSyntax argument, string message, IDiagnosticReporter diagnosticReporter)
        => diagnosticReporter.GenerateErrorWithCustomMessage(ErrorType.UnsupportedMVPExpression, message, argument.Span);
}
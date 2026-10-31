using Sushi.Diagnostics;
using Sushi.Parsing.Syntax;

namespace Sushi.Semantics;

/// <summary>
/// Binds the currently supported package-level function semantics and reusable callable bodies.
/// </summary>
public sealed class FunctionDeclarationBinder
{
    /// <summary>
    /// Binds a function declaration when it belongs to the currently supported executable semantic subset.
    /// </summary>
    /// <param name="declaration">
    /// The declaration to bind.
    /// </param>
    /// <param name="diagnosticReporter">
    /// The diagnostic reporter used to accumulate diagnostics.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token used to cancel binding.
    /// </param>
    /// <returns>
    /// The bound function declaration.
    /// </returns>
    public static FunctionBindResult Bind(FunctionDeclarationSyntax declaration, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryBindAccessibility(declaration.AccessModifier, out Accessibility accessibility)
            || !TryBindReturnType(declaration.ReturnType, out BoundType returnType)
            || !declaration.OpenParenthesisToken.IsSourceBacked
            || !declaration.CloseParenthesisToken.IsSourceBacked
            || !declaration.Body.OpenBraceToken.IsSourceBacked
            || !declaration.Body.CloseBraceToken.IsSourceBacked)
        {
            return new FunctionBindResult(null);
        }

        string? name = IdentifierBinder.Bind(declaration.Name);

        if (name is null)
        {
            return new FunctionBindResult(null);
        }

        IReadOnlyList<BoundParameter>? parameters = BindParameters(declaration.Parameters, diagnosticReporter, cancellationToken);

        if (parameters is null)
        {
            return new FunctionBindResult(null);
        }

        BoundBlock? body = BindBody(declaration.Body, returnType, parameters, diagnosticReporter, cancellationToken);

        return body is null
            ? new FunctionBindResult(null)
            : new FunctionBindResult(new BoundFunction(accessibility, name, returnType, parameters, body));
    }

    private static IReadOnlyList<BoundParameter>? BindParameters(IReadOnlyList<ParameterSyntax> declarations, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        List<BoundParameter> parameters = [];
        HashSet<string> names = new(StringComparer.Ordinal);
        bool valid = true;

        foreach (ParameterSyntax declaration in declarations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!declaration.TypeToken.IsSourceBacked || !declaration.NameToken.IsSourceBacked)
            {
                valid = false;
                continue;
            }

            string? name = IdentifierBinder.Bind(declaration.NameToken);

            if (!TryBindReturnType(declaration.TypeToken, out BoundType type) || type is BoundType.Void)
            {
                diagnosticReporter.GenerateError(ErrorType.UnsupportedMVPExpression, declaration.TypeToken.Span);
                valid = false;
                continue;
            }

            if (name is null)
            {
                valid = false;
                continue;
            }

            if (!names.Add(name))
            {
                diagnosticReporter.GenerateError(ErrorType.DuplicateParameter, declaration.NameToken.Span);
                valid = false;
                continue;
            }

            parameters.Add(new BoundParameter(parameters.Count, name, type));
        }

        return valid ? parameters : null;
    }

    /// <summary>
    /// Binds every statement in a callable body, including branches and
    /// syntactically present statements that cannot be reached during execution.
    /// </summary>
    /// <param name="block">
    /// The parsed statement block.
    /// </param>
    /// <param name="returnType">
    /// The enclosing callable's declared return type.
    /// </param>
    /// <param name="parameters">
    /// The named parameters available to expressions.
    /// </param>
    /// <param name="diagnosticReporter">
    /// Receives static semantic diagnostics.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels binding.
    /// </param>
    /// <returns>
    /// The bound block, or null when one or more statements are invalid.
    /// </returns>
    internal static BoundBlock? BindBody(BlockSyntax block, BoundType returnType, IReadOnlyList<BoundParameter> parameters, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);

        List<BoundStatement> statements = [];
        bool valid = true;

        foreach (StatementSyntax statement in block.Statements)
        {
            cancellationToken.ThrowIfCancellationRequested();

            BoundStatement? boundStatement = statement switch
            {
                ReturnStatementSyntax returnStatement => BindReturnStatement(returnStatement, returnType, parameters, diagnosticReporter, cancellationToken),
                IfStatementSyntax conditional => BindConditionalStatement(conditional, returnType, parameters, diagnosticReporter, cancellationToken),
                ExpressionStatementSyntax expressionStatement => ReportUnsupportedExpressionStatement(expressionStatement, diagnosticReporter),
                _ => null
            };

            if (boundStatement is null)
            {
                valid = false;
                continue;
            }

            statements.Add(boundStatement);
        }

        return valid ? new BoundBlock(statements) : null;
    }

    private static BoundStatement? ReportUnsupportedExpressionStatement(ExpressionStatementSyntax statement, IDiagnosticReporter diagnosticReporter)
    {
        diagnosticReporter.GenerateError(ErrorType.UnsupportedMVPExpression, statement.Span);
        return null;
    }

    private static BoundIfStatement? BindConditionalStatement(
        IfStatementSyntax statement,
        BoundType returnType,
        IReadOnlyList<BoundParameter> parameters,
        IDiagnosticReporter diagnosticReporter,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!statement.IfKeyword.IsSourceBacked
            || !statement.OpenParenthesisToken.IsSourceBacked
            || !statement.CloseParenthesisToken.IsSourceBacked
            || !statement.ThenBlock.OpenBraceToken.IsSourceBacked
            || !statement.ThenBlock.CloseBraceToken.IsSourceBacked)
        {
            return null;
        }

        if (returnType is not BoundType.Void)
        {
            diagnosticReporter.GenerateError(ErrorType.UnsupportedMVPExpression, statement.Span);
            return null;
        }

        BoundExpression? condition = BooleanExpressionBinder.Bind(statement.Condition, parameters, diagnosticReporter, cancellationToken);
        BoundBlock? thenBody = BindBody(statement.ThenBlock, returnType, parameters, diagnosticReporter, cancellationToken);

        BoundBlock? elseBody = null;
        bool validElse = true;

        if (statement.ElseKeyword is SyntaxToken elseKeyword && !elseKeyword.IsSourceBacked)
        {
            validElse = false;
        }
        else if (statement.ElseBlock is BlockSyntax elseBlock)
        {
            if (!elseBlock.OpenBraceToken.IsSourceBacked || !elseBlock.CloseBraceToken.IsSourceBacked)
            {
                validElse = false;
            }
            else
            {
                elseBody = BindBody(elseBlock, returnType, parameters, diagnosticReporter, cancellationToken);
                validElse = elseBody is not null;
            }
        }
        else if (statement.ElseIf is IfStatementSyntax elseIf)
        {
            BoundIfStatement? alternative = BindConditionalStatement(elseIf, returnType, parameters, diagnosticReporter, cancellationToken);

            if (alternative is null)
            {
                validElse = false;
            }
            else
            {
                elseBody = new BoundBlock([alternative]);
            }
        }

        return condition is null || thenBody is null || !validElse
            ? null
            : new BoundIfStatement(condition, thenBody, elseBody);
    }

    private static BoundReturnStatement? BindReturnStatement(ReturnStatementSyntax statement, BoundType returnType, IReadOnlyList<BoundParameter> parameters, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(diagnosticReporter);

        if (!statement.ReturnKeyword.IsSourceBacked || !statement.SemicolonToken.IsSourceBacked)
        {
            return null;
        }

        if (returnType is BoundType.Void)
        {
            if (statement.Expression is not null)
            {
                diagnosticReporter.GenerateError(ErrorType.ReturnValueInVoidFunction, statement.Expression.Span);
                return null;
            }

            return new BoundReturnStatement(null);
        }

        if (statement.Expression is null)
        {
            diagnosticReporter.GenerateError(ErrorType.MissingReturnValue, statement.SemicolonToken.Span);
            return null;
        }

        BoundExpression? expression = returnType switch
        {
            BoundType.Int32 => IntegerExpressionBinder.Bind(statement.Expression, parameters, diagnosticReporter, cancellationToken),
            BoundType.Bool => BooleanExpressionBinder.Bind(statement.Expression, parameters, diagnosticReporter, cancellationToken),
            _ => throw new NotSupportedException($"Return type \"{returnType}\" is not supported by the executable MVP.")
        };

        return expression is null ? null : new BoundReturnStatement(expression);
    }

    private static bool TryBindReturnType(SyntaxToken token, out BoundType type)
    {
        if (token.Type is SyntaxType.VoidKeyword && token.IsSourceBacked)
        {
            type = BoundType.Void;
            return true;
        }

        switch (IdentifierBinder.Bind(token))
        {
            case "int32":
                type = BoundType.Int32;
                return true;

            case "bool":
                type = BoundType.Bool;
                return true;

            default:
                type = default;
                return false;
        }
    }

    private static bool TryBindAccessibility(SyntaxToken token, out Accessibility accessibility)
    {
        if (!token.IsSourceBacked)
        {
            accessibility = default;
            return false;
        }

        ReadOnlySpan<byte> spelling = token.Span.Snapshot.Bytes.Span[token.Span.Start..token.Span.End];

        if (spelling.SequenceEqual("public"u8))
        {
            accessibility = Accessibility.Public;
            return true;
        }

        if (spelling.SequenceEqual("internal"u8))
        {
            accessibility = Accessibility.Internal;
            return true;
        }

        if (spelling.SequenceEqual("package"u8))
        {
            accessibility = Accessibility.Package;
            return true;
        }

        if (spelling.SequenceEqual("protected"u8))
        {
            accessibility = Accessibility.Protected;
            return true;
        }

        if (spelling.SequenceEqual("private"u8))
        {
            accessibility = Accessibility.Private;
            return true;
        }

        accessibility = default;
        return false;
    }
}
using Sushi.Diagnostics;
using Sushi.Parsing.Syntax;

namespace Sushi.Semantics;

/// <summary>
/// Binds supported package-level function declarations, their signatures,
/// and their statement bodies.
/// </summary>
public sealed class FunctionDeclarationBinder
{
    /// <summary>
    /// Binds a complete function using the standalone source-analysis rules.
    /// </summary>
    /// <param name="declaration">
    /// The parsed function declaration.
    /// </param>
    /// <param name="diagnosticReporter">
    /// Receives static diagnostics.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels binding.
    /// </param>
    /// <returns>
    /// The bound function, or null when binding fails.
    /// </returns>
    public static FunctionBindResult Bind(FunctionDeclarationSyntax declaration, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
        => Bind(declaration, diagnosticReporter, cancellationToken, null);

    /// <summary>
    /// Binds a complete function with a project-resolved package-call context.
    /// </summary>
    /// <param name="declaration">
    /// The parsed function declaration.
    /// </param>
    /// <param name="diagnosticReporter">
    /// Receives static diagnostics.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels binding.
    /// </param>
    /// <param name="callables">
    /// Optional package functions eligible for direct invocation.
    /// </param>
    /// <returns>
    /// The complete bound function, or null on failure.
    /// </returns>
    internal static FunctionBindResult Bind(
        FunctionDeclarationSyntax declaration,
        IDiagnosticReporter diagnosticReporter,
        CancellationToken cancellationToken,
        IReadOnlyList<PackageCallable>? callables)
    {
        FunctionBindResult signatureResult = BindSignature(declaration, diagnosticReporter, cancellationToken);
        BoundFunction? signature = signatureResult.Function;

        if (signature is null)
        {
            return new FunctionBindResult(null);
        }

        BoundBlock? body = BindBody(declaration.Body, signature.ReturnType, signature.Parameters, diagnosticReporter, cancellationToken, callables);

        if (body is null)
        {
            return new FunctionBindResult(null);
        }

        if (signature.ReturnType is not BoundType.Void && CanCompleteNormally(body))
        {
            diagnosticReporter.GenerateError(ErrorType.MissingReturnPath, declaration.Body.CloseBraceToken.Span);
            return new FunctionBindResult(null);
        }

        return new FunctionBindResult(new BoundFunction(signature.Accessibility, signature.Name, signature.ReturnType, signature.Parameters, body));
    }

    /// <summary>
    /// Binds the declaration contract while deliberately leaving the body
    /// unbound. The placeholder body is replaced by the full project binding pass.
    /// </summary>
    /// <param name="declaration">
    /// The parsed function declaration.
    /// </param>
    /// <param name="diagnosticReporter">
    /// Receives signature diagnostics.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels binding.
    /// </param>
    /// <returns>
    /// A signature-bearing function with an empty placeholder body, or null on failure.
    /// </returns>
    internal static FunctionBindResult BindSignature(FunctionDeclarationSyntax declaration, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
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

        return parameters is null
            ? new FunctionBindResult(null)
            : new FunctionBindResult(new BoundFunction(accessibility, name, returnType, parameters, new BoundBlock([])));
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
    /// Binds every statement, including unreachable statements, within a lexical
    /// scope. Local declarations and assignments share the enclosing function's bindings.
    /// </summary>
    /// <param name="block">
    /// The parsed statement block.
    /// </param>
    /// <param name="returnType">
    /// The enclosing function's declared return type.
    /// </param>
    /// <param name="parameters">
    /// The enclosing function's parameters.
    /// </param>
    /// <param name="diagnosticReporter">
    /// Receives static diagnostics.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels binding.
    /// </param>
    /// <param name="callables">
    /// Optional package functions visible to the current body.
    /// </param>
    /// <param name="locals">
    /// The enclosing lexical context, or null for a new function body.
    /// </param>
    /// <returns>
    /// The bound block, or null if any statement fails validation.
    /// </returns>
    internal static BoundBlock? BindBody(
        BlockSyntax block,
        BoundType returnType,
        IReadOnlyList<BoundParameter> parameters,
        IDiagnosticReporter diagnosticReporter,
        CancellationToken cancellationToken,
        IReadOnlyList<PackageCallable>? callables = null,
        LocalBindingContext? locals = null)
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);

        LocalBindingContext context = locals ?? new LocalBindingContext(parameters);
        context.EnterScope();

        try
        {
            List<BoundStatement> statements = [];
            bool valid = true;

            foreach (StatementSyntax statement in block.Statements)
            {
                cancellationToken.ThrowIfCancellationRequested();

                BoundStatement? boundStatement = statement switch
                {
                    ReturnStatementSyntax result => BindReturnStatement(result, returnType, parameters, diagnosticReporter, cancellationToken, callables, context),
                    IfStatementSyntax conditional => BindConditionalStatement(conditional, returnType, parameters, diagnosticReporter, cancellationToken, callables, context),
                    ExpressionStatementSyntax expression => BindExpressionStatement(expression, parameters, callables, diagnosticReporter, cancellationToken, context),
                    LocalDeclarationStatementSyntax declaration => BindLocalDeclaration(declaration, parameters, callables, diagnosticReporter, cancellationToken, context),
                    LocalAssignmentStatementSyntax assignment => BindLocalAssignment(assignment, parameters, callables, diagnosticReporter, cancellationToken, context),
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
        finally
        {
            context.ExitScope();
        }
    }

    private static BoundLocalDeclarationStatement? BindLocalDeclaration(
        LocalDeclarationStatementSyntax declaration,
        IReadOnlyList<BoundParameter> parameters,
        IReadOnlyList<PackageCallable>? callables,
        IDiagnosticReporter diagnosticReporter,
        CancellationToken cancellationToken,
        LocalBindingContext locals)
    {
        if (!declaration.TypeToken.IsSourceBacked
            || !declaration.NameToken.IsSourceBacked
            || !declaration.EqualsToken.IsSourceBacked
            || !declaration.SemicolonToken.IsSourceBacked)
        {
            return null;
        }

        string? typeName = IdentifierBinder.Bind(declaration.TypeToken);
        string? name = IdentifierBinder.Bind(declaration.NameToken);

        BoundType? type = typeName switch
        {
            "int32" => BoundType.Int32,
            "bool" => BoundType.Bool,
            _ => null
        };

        if (type is null || name is null || IsReservedLocalName(name))
        {
            diagnosticReporter.GenerateError(ErrorType.UnsupportedMVPExpression, declaration.Span);
            return null;
        }

        BoundExpression? initializer = BindValue(declaration.Initializer, type.Value, parameters, callables, diagnosticReporter, cancellationToken, locals);

        if (initializer is null)
        {
            return null;
        }

        if (!locals.TryDeclare(name, type.Value, out BoundLocal? local))
        {
            diagnosticReporter.GenerateError(ErrorType.DuplicateLocal, declaration.NameToken.Span);
            return null;
        }

        return new BoundLocalDeclarationStatement(local!, initializer);
    }

    private static BoundLocalAssignmentStatement? BindLocalAssignment(
        LocalAssignmentStatementSyntax assignment,
        IReadOnlyList<BoundParameter> parameters,
        IReadOnlyList<PackageCallable>? callables,
        IDiagnosticReporter diagnosticReporter,
        CancellationToken cancellationToken,
        LocalBindingContext locals)
    {
        if (!assignment.NameToken.IsSourceBacked
            || !assignment.EqualsToken.IsSourceBacked
            || !assignment.SemicolonToken.IsSourceBacked)
        {
            return null;
        }

        string? name = IdentifierBinder.Bind(assignment.NameToken);
        BoundExpression? target = name is null ? null : locals.Resolve(name);

        if (target is null)
        {
            diagnosticReporter.GenerateError(ErrorType.UnknownIdentifier, assignment.NameToken.Span);
            return null;
        }

        if (target is not BoundLocalReferenceExpression local)
        {
            diagnosticReporter.GenerateErrorWithCustomMessage(
                ErrorType.UnsupportedMVPExpression,
                "Only initialized local variables may be assignment targets in the current executable subset.",
                assignment.NameToken.Span);
            return null;
        }

        BoundExpression? value = BindValue(assignment.Value, local.Type, parameters, callables, diagnosticReporter, cancellationToken, locals);
        return value is null ? null : new BoundLocalAssignmentStatement(local.Local, value);
    }

    private static BoundExpression? BindValue(
        ExpressionSyntax expression,
        BoundType expectedType,
        IReadOnlyList<BoundParameter> parameters,
        IReadOnlyList<PackageCallable>? callables,
        IDiagnosticReporter diagnosticReporter,
        CancellationToken cancellationToken,
        LocalBindingContext locals)
        => expectedType switch
        {
            BoundType.Int32 => IntegerExpressionBinder.Bind(expression, parameters, diagnosticReporter, cancellationToken, callables, locals),
            BoundType.Bool => BooleanExpressionBinder.Bind(expression, parameters, diagnosticReporter, cancellationToken, callables, locals),
            _ => throw new NotSupportedException($"Value type \"{expectedType}\" is not supported by this executable subset.")
        };

    private static bool IsReservedLocalName(string name)
        => name.All(character => character == '_')
            || name is "bool" or "char" or "string"
                or "int8" or "uint8" or "int16" or "uint16"
                or "int32" or "uint32" or "int64" or "uint64"
                or "int128" or "uint128"
                or "float16" or "float32" or "float64" or "decimal128";

    private static BoundStatement? BindExpressionStatement(
        ExpressionStatementSyntax statement,
        IReadOnlyList<BoundParameter> parameters,
        IReadOnlyList<PackageCallable>? callables,
        IDiagnosticReporter diagnosticReporter,
        CancellationToken cancellationToken,
        LocalBindingContext locals)
    {
        if (callables is not null)
        {
            return PackageInvocationBinder.Bind(statement, callables, parameters, diagnosticReporter, cancellationToken, locals);
        }

        diagnosticReporter.GenerateError(ErrorType.UnsupportedMVPExpression, statement.Span);
        return null;
    }

    private static BoundIfStatement? BindConditionalStatement(
        IfStatementSyntax statement,
        BoundType returnType,
        IReadOnlyList<BoundParameter> parameters,
        IDiagnosticReporter diagnosticReporter,
        CancellationToken cancellationToken,
        IReadOnlyList<PackageCallable>? callables,
        LocalBindingContext locals)
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

        BoundExpression? condition = BooleanExpressionBinder.Bind(statement.Condition, parameters, diagnosticReporter, cancellationToken, callables, locals);
        BoundBlock? thenBody = BindBody(statement.ThenBlock, returnType, parameters, diagnosticReporter, cancellationToken, callables, locals);

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
                elseBody = BindBody(elseBlock, returnType, parameters, diagnosticReporter, cancellationToken, callables, locals);
                validElse = elseBody is not null;
            }
        }
        else if (statement.ElseIf is IfStatementSyntax elseIf)
        {
            BoundIfStatement? alternative = BindConditionalStatement(elseIf, returnType, parameters, diagnosticReporter, cancellationToken, callables, locals);

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

    private static BoundReturnStatement? BindReturnStatement(
        ReturnStatementSyntax statement,
        BoundType returnType,
        IReadOnlyList<BoundParameter> parameters,
        IDiagnosticReporter diagnosticReporter,
        CancellationToken cancellationToken,
        IReadOnlyList<PackageCallable>? callables,
        LocalBindingContext locals)
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

        BoundExpression? expression = BindValue(statement.Expression, returnType, parameters, callables, diagnosticReporter, cancellationToken, locals);
        return expression is null ? null : new BoundReturnStatement(expression);
    }

    private static bool CanCompleteNormally(BoundBlock block)
    {
        foreach (BoundStatement statement in block.Statements)
        {
            if (!CanCompleteNormally(statement))
            {
                return false;
            }
        }

        return true;
    }

    private static bool CanCompleteNormally(BoundStatement statement)
        => statement switch
        {
            BoundReturnStatement => false,
            BoundIfStatement conditional => CanConditionalCompleteNormally(conditional),
            _ => true
        };

    private static bool CanConditionalCompleteNormally(BoundIfStatement conditional)
    {
        if (conditional.Condition is BoundBooleanConstantExpression constant)
        {
            return constant.Value
                ? CanCompleteNormally(conditional.Then)
                : conditional.Else is null || CanCompleteNormally(conditional.Else);
        }

        return CanCompleteNormally(conditional.Then)
            || conditional.Else is null
            || CanCompleteNormally(conditional.Else);
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
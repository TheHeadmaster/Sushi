using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text;
using Sushi.Diagnostics;
using Sushi.Documentation;
using Sushi.Parsing.Syntax;
using Sushi.Source;

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
    public static FunctionBindResult Bind([NotNull] FunctionDeclarationSyntax declaration, [NotNull] IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
        => Bind(declaration, diagnosticReporter, null, cancellationToken);

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
        IReadOnlyList<PackageCallable>? callables,
        CancellationToken cancellationToken,
        DocumentationBlock? documentation = null,
        bool captureSourceSemanticOccurrences = false)
    {
        FunctionBindResult signatureResult = BindSignature(declaration, diagnosticReporter, cancellationToken);
        BoundFunction? signature = signatureResult.Function;

        if (signature is null)
        {
            return new FunctionBindResult(null);
        }

        LocalBindingContext locals = captureSourceSemanticOccurrences
            ? new LocalBindingContext(signature.Parameters, declaration, signature, documentation)
            : new LocalBindingContext(signature.Parameters);
        BoundBlock? body = BindBody(declaration.Body, signature.ReturnType, signature.Parameters, diagnosticReporter,
            cancellationToken, callables, locals);

        if (body is null)
        {
            return new FunctionBindResult(null);
        }

        if (signature.ReturnType is not BoundType.Void && CanCompleteNormally(body))
        {
            diagnosticReporter.GenerateError(ErrorType.MissingReturnPath, declaration.Body.CloseBraceToken.Span);
            return new FunctionBindResult(null);
        }

        return new FunctionBindResult(new BoundFunction(signature.Accessibility, signature.Name, signature.ReturnType,
            signature.Parameters, body), locals.GetDeclarationSpans(), locals.GetOccurrences());
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
    /// scope. Loop-control statements are validated against active loop nesting.
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
                    ReturnStatementSyntax result => BindReturnStatement(result, returnType, parameters, diagnosticReporter, callables, context, cancellationToken),
                    IfStatementSyntax conditional => BindConditionalStatement(conditional, returnType, parameters, diagnosticReporter, callables, context, cancellationToken),
                    WhileStatementSyntax loop => BindWhileStatement(loop, returnType, parameters, diagnosticReporter, callables, context, cancellationToken),
                    DoWhileStatementSyntax loop => BindDoWhileStatement(loop, returnType, parameters, diagnosticReporter, callables, context, cancellationToken),
                    ForStatementSyntax loop => BindForStatement(loop, returnType, parameters, diagnosticReporter, callables, context, cancellationToken),
                    LoopTransferStatementSyntax transfer => BindLoopTransferStatement(transfer, diagnosticReporter, context),
                    ExpressionStatementSyntax expression => BindExpressionStatement(expression, parameters, callables, diagnosticReporter, context, cancellationToken),
                    LocalDeclarationStatementSyntax declaration => BindLocalDeclaration(declaration, parameters, callables, diagnosticReporter, context, cancellationToken),
                    LocalAssignmentStatementSyntax assignment => BindLocalAssignment(assignment, parameters, callables, diagnosticReporter, context, cancellationToken),
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

    private static BoundWhileStatement? BindWhileStatement(
        WhileStatementSyntax statement,
        BoundType returnType,
        IReadOnlyList<BoundParameter> parameters,
        IDiagnosticReporter diagnosticReporter,
        IReadOnlyList<PackageCallable>? callables,
        LocalBindingContext locals,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!statement.WhileKeyword.IsSourceBacked
            || !statement.OpenParenthesisToken.IsSourceBacked
            || !statement.CloseParenthesisToken.IsSourceBacked
            || !statement.Body.OpenBraceToken.IsSourceBacked
            || !statement.Body.CloseBraceToken.IsSourceBacked)
        {
            return null;
        }

        BoundExpression? condition = BooleanExpressionBinder.Bind(statement.Condition, parameters, diagnosticReporter, cancellationToken, callables, locals);

        locals.EnterLoop();

        try
        {
            BoundBlock? body = BindBody(statement.Body, returnType, parameters, diagnosticReporter, cancellationToken, callables, locals);
            return condition is null || body is null ? null : new BoundWhileStatement(condition, body);
        }
        finally
        {
            locals.ExitLoop();
        }
    }

    private static BoundDoWhileStatement? BindDoWhileStatement(
        DoWhileStatementSyntax statement,
        BoundType returnType,
        IReadOnlyList<BoundParameter> parameters,
        IDiagnosticReporter diagnosticReporter,
        IReadOnlyList<PackageCallable>? callables,
        LocalBindingContext locals,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!statement.DoKeyword.IsSourceBacked
            || !statement.WhileKeyword.IsSourceBacked
            || !statement.OpenParenthesisToken.IsSourceBacked
            || !statement.CloseParenthesisToken.IsSourceBacked
            || !statement.Body.OpenBraceToken.IsSourceBacked
            || !statement.Body.CloseBraceToken.IsSourceBacked)
        {
            return null;
        }

        BoundExpression? condition = BooleanExpressionBinder.Bind(statement.Condition, parameters, diagnosticReporter, cancellationToken, callables, locals);

        locals.EnterLoop();

        try
        {
            BoundBlock? body = BindBody(statement.Body, returnType, parameters, diagnosticReporter, cancellationToken, callables, locals);
            return condition is null || body is null ? null : new BoundDoWhileStatement(body, condition);
        }
        finally
        {
            locals.ExitLoop();
        }
    }

    private static BoundForStatement? BindForStatement(
        ForStatementSyntax statement,
        BoundType returnType,
        IReadOnlyList<BoundParameter> parameters,
        IDiagnosticReporter diagnosticReporter,
        IReadOnlyList<PackageCallable>? callables,
        LocalBindingContext locals,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!statement.ForKeyword.IsSourceBacked
            || !statement.OpenParenthesisToken.IsSourceBacked
            || !statement.InitializationSemicolon.IsSourceBacked
            || !statement.ConditionSemicolon.IsSourceBacked
            || !statement.CloseParenthesisToken.IsSourceBacked
            || !statement.Body.OpenBraceToken.IsSourceBacked
            || !statement.Body.CloseBraceToken.IsSourceBacked)
        {
            return null;
        }

        locals.EnterScope();

        try
        {
            bool valid = true;
            List<BoundStatement> initialization = [];

            foreach (ForHeaderOperationSyntax operation in statement.Initialization)
            {
                cancellationToken.ThrowIfCancellationRequested();

                BoundStatement? bound = BindForHeaderOperation(operation, allowDeclarations: true, parameters, callables, diagnosticReporter, locals, cancellationToken);

                if (bound is null)
                {
                    valid = false;
                }
                else
                {
                    initialization.Add(bound);
                }
            }

            BoundExpression? condition = statement.Condition is null
                ? new BoundBooleanConstantExpression(true)
                : BooleanExpressionBinder.Bind(statement.Condition, parameters, diagnosticReporter, cancellationToken, callables, locals);

            List<BoundStatement> iteration = [];

            foreach (ForHeaderOperationSyntax operation in statement.Iteration)
            {
                cancellationToken.ThrowIfCancellationRequested();

                BoundStatement? bound = BindForHeaderOperation(operation, allowDeclarations: false, parameters, callables, diagnosticReporter, locals, cancellationToken);

                if (bound is null)
                {
                    valid = false;
                }
                else
                {
                    iteration.Add(bound);
                }
            }

            BoundBlock? body;

            locals.EnterLoop();

            try
            {
                body = BindBody(statement.Body, returnType, parameters, diagnosticReporter, cancellationToken, callables, locals);
            }
            finally
            {
                locals.ExitLoop();
            }

            return !valid || condition is null || body is null
                ? null
                : new BoundForStatement(new BoundBlock(initialization), condition, new BoundBlock(iteration), body);
        }
        finally
        {
            locals.ExitScope();
        }
    }

    private static BoundStatement? BindForHeaderOperation(
        ForHeaderOperationSyntax operation,
        bool allowDeclarations,
        IReadOnlyList<BoundParameter> parameters,
        IReadOnlyList<PackageCallable>? callables,
        IDiagnosticReporter diagnosticReporter,
        LocalBindingContext locals,
        CancellationToken cancellationToken)
    {
        if (!operation.NameToken.IsSourceBacked
            || !operation.OperatorToken.IsSourceBacked
            || (operation.TypeToken is SyntaxToken typeToken && !typeToken.IsSourceBacked))
        {
            return null;
        }

        if (operation.IsDeclaration)
        {
            if (!allowDeclarations || operation.Value is null || GetAssignmentOperator(operation.OperatorToken) != "=")
            {
                diagnosticReporter.GenerateError(ErrorType.UnsupportedMVPExpression, operation.Span);
                return null;
            }

            return BindInitializedLocal(operation.TypeToken!.Value, operation.NameToken, operation.Value, operation.Span,
                parameters, callables, diagnosticReporter, locals, cancellationToken);
        }

        if (operation.IsIncrementOrDecrement)
        {
            IncrementDecrementExpressionSyntax syntax = new(
                operation.OperatorToken,
                new IdentifierExpressionSyntax(operation.NameToken),
                operation.IsPrefix);

            BoundIntegerUpdateExpression? update = IntegerUpdateBinder.Bind(syntax, diagnosticReporter, locals);
            return update is null ? null : new BoundIntegerUpdateStatement(update);
        }

        return BindAssignmentOperation(operation.NameToken, operation.OperatorToken, operation.Value!,
            parameters, callables, diagnosticReporter, locals, cancellationToken);
    }

    private static BoundLoopTransferStatement? BindLoopTransferStatement(
        LoopTransferStatementSyntax statement,
        IDiagnosticReporter diagnosticReporter,
        LocalBindingContext locals)
    {
        if (!statement.SemicolonToken.IsSourceBacked || statement.Keywords.Any(keyword => !keyword.IsSourceBacked))
        {
            return null;
        }

        if (statement.Depth > locals.LoopDepth)
        {
            string keyword = statement.IsBreak ? "break" : "continue";

            diagnosticReporter.GenerateErrorWithCustomMessage(
                ErrorType.UnsupportedMVPExpression,
                $"The {keyword} statement targets {statement.Depth} enclosing loop(s), but only {locals.LoopDepth} are available.",
                statement.Span);

            return null;
        }

        return new BoundLoopTransferStatement(statement.IsBreak, statement.Depth);
    }

    private static BoundLocalDeclarationStatement? BindLocalDeclaration(
        LocalDeclarationStatementSyntax declaration,
        IReadOnlyList<BoundParameter> parameters,
        IReadOnlyList<PackageCallable>? callables,
        IDiagnosticReporter diagnosticReporter,
        LocalBindingContext locals,
        CancellationToken cancellationToken)
    {
        if (!declaration.TypeToken.IsSourceBacked
            || !declaration.NameToken.IsSourceBacked
            || !declaration.EqualsToken.IsSourceBacked
            || !declaration.SemicolonToken.IsSourceBacked)
        {
            return null;
        }

        return BindInitializedLocal(declaration.TypeToken, declaration.NameToken, declaration.Initializer, declaration.Span,
            parameters, callables, diagnosticReporter, locals, cancellationToken);
    }

    private static BoundLocalDeclarationStatement? BindInitializedLocal(
        SyntaxToken typeToken,
        SyntaxToken nameToken,
        ExpressionSyntax initializer,
        SourceSpan declarationSpan,
        IReadOnlyList<BoundParameter> parameters,
        IReadOnlyList<PackageCallable>? callables,
        IDiagnosticReporter diagnosticReporter,
        LocalBindingContext locals,
        CancellationToken cancellationToken)
    {
        string? typeName = IdentifierBinder.Bind(typeToken);
        string? name = IdentifierBinder.Bind(nameToken);

        BoundType? type = typeName switch
        {
            "int32" => BoundType.Int32,
            "bool" => BoundType.Bool,
            _ => null
        };

        if (type is null || name is null || IsReservedLocalName(name))
        {
            diagnosticReporter.GenerateError(ErrorType.UnsupportedMVPExpression, declarationSpan);
            return null;
        }

        BoundExpression? value = BindValue(initializer, type.Value, parameters, callables, diagnosticReporter, locals, cancellationToken);

        if (value is null)
        {
            return null;
        }

        if (!locals.TryDeclare(name, type.Value, nameToken.Span, typeToken.Span, out BoundLocal? local))
        {
            diagnosticReporter.GenerateError(ErrorType.DuplicateLocal, nameToken.Span);
            return null;
        }

        return new BoundLocalDeclarationStatement(local!, value);
    }

    private static BoundLocalAssignmentStatement? BindLocalAssignment(
        LocalAssignmentStatementSyntax assignment,
        IReadOnlyList<BoundParameter> parameters,
        IReadOnlyList<PackageCallable>? callables,
        IDiagnosticReporter diagnosticReporter,
        LocalBindingContext locals,
        CancellationToken cancellationToken)
    {
        if (!assignment.NameToken.IsSourceBacked
            || !assignment.OperatorToken.IsSourceBacked
            || !assignment.SemicolonToken.IsSourceBacked)
        {
            return null;
        }

        return BindAssignmentOperation(assignment.NameToken, assignment.OperatorToken, assignment.Value,
            parameters, callables, diagnosticReporter, locals, cancellationToken);
    }

    private static BoundLocalAssignmentStatement? BindAssignmentOperation(
        SyntaxToken nameToken,
        SyntaxToken operatorToken,
        ExpressionSyntax valueSyntax,
        IReadOnlyList<BoundParameter> parameters,
        IReadOnlyList<PackageCallable>? callables,
        IDiagnosticReporter diagnosticReporter,
        LocalBindingContext locals,
        CancellationToken cancellationToken)
    {
        string? name = IdentifierBinder.Bind(nameToken);
        BoundExpression? target = name is null ? null : locals.Resolve(name, nameToken.Span);

        if (target is null)
        {
            diagnosticReporter.GenerateError(ErrorType.UnknownIdentifier, nameToken.Span);
            return null;
        }

        if (target is not BoundLocalReferenceExpression local)
        {
            diagnosticReporter.GenerateErrorWithCustomMessage(
                ErrorType.UnsupportedMVPExpression,
                "Only initialized local variables may be assignment targets in the current executable subset.",
                nameToken.Span);
            return null;
        }

        string operation = GetAssignmentOperator(operatorToken);

        BoundExpression? value = operation == "="
            ? BindValue(valueSyntax, local.Type, parameters, callables, diagnosticReporter, locals, cancellationToken)
            : BindCompoundAssignmentValue(operatorToken, valueSyntax, operation, local, parameters, callables, diagnosticReporter, locals, cancellationToken);

        return value is null ? null : new BoundLocalAssignmentStatement(local.Local, value);
    }

    private static string GetAssignmentOperator(SyntaxToken token)
    {
        SourceSpan span = token.Span;
        return Encoding.UTF8.GetString(span.Snapshot.Bytes.Span[span.Start..span.End]);
    }

    private static BoundExpression? BindCompoundAssignmentValue(
        SyntaxToken operatorToken,
        ExpressionSyntax valueSyntax,
        string spelling,
        BoundLocalReferenceExpression target,
        IReadOnlyList<BoundParameter> parameters,
        IReadOnlyList<PackageCallable>? callables,
        IDiagnosticReporter diagnosticReporter,
        LocalBindingContext locals,
        CancellationToken cancellationToken)
        => target.Type switch
        {
            BoundType.Int32 => BindCompoundIntegerAssignment(operatorToken, valueSyntax, spelling, target, parameters, callables, diagnosticReporter, locals, cancellationToken),
            BoundType.Bool => BindCompoundBooleanAssignment(operatorToken, valueSyntax, spelling, target, parameters, callables, diagnosticReporter, locals, cancellationToken),
            _ => throw new NotSupportedException($"Local type \"{target.Type}\" is not supported by compound assignment.")
        };

    private static BoundExpression? BindCompoundIntegerAssignment(
        SyntaxToken operatorToken,
        ExpressionSyntax valueSyntax,
        string spelling,
        BoundLocalReferenceExpression target,
        IReadOnlyList<BoundParameter> parameters,
        IReadOnlyList<PackageCallable>? callables,
        IDiagnosticReporter diagnosticReporter,
        LocalBindingContext locals,
        CancellationToken cancellationToken)
    {
        IntegerBinaryOperator? operation = spelling switch
        {
            "+=" => IntegerBinaryOperator.Add,
            "-=" => IntegerBinaryOperator.Subtract,
            "*=" => IntegerBinaryOperator.Multiply,
            "/=" => IntegerBinaryOperator.Divide,
            "%=" => IntegerBinaryOperator.Remainder,
            "<<=" => IntegerBinaryOperator.ShiftLeft,
            ">>=" => IntegerBinaryOperator.ShiftRight,
            "&=" => IntegerBinaryOperator.BitwiseAnd,
            "^=" => IntegerBinaryOperator.BitwiseXor,
            "|=" => IntegerBinaryOperator.BitwiseOr,
            _ => null
        };

        if (operation is null)
        {
            diagnosticReporter.GenerateError(ErrorType.UnsupportedMVPExpression, operatorToken.Span);
            return null;
        }

        BoundExpression? right = operation is IntegerBinaryOperator.ShiftLeft or IntegerBinaryOperator.ShiftRight
            ? BindCompoundShiftCount(valueSyntax, parameters, callables, diagnosticReporter, locals, cancellationToken)
            : BindValue(valueSyntax, BoundType.Int32, parameters, callables, diagnosticReporter, locals, cancellationToken);

        if (right is null)
        {
            return null;
        }

        if (operation is IntegerBinaryOperator.Divide or IntegerBinaryOperator.Remainder && right is BoundIntegerLiteralExpression { Value: 0 })
        {
            diagnosticReporter.GenerateError(ErrorType.IntegerDivisionByZero, operatorToken.Span);
            return null;
        }

        return new BoundIntegerBinaryExpression(operation.Value, target, right);
    }

    private static BoundExpression? BindCompoundBooleanAssignment(
        SyntaxToken operatorToken,
        ExpressionSyntax valueSyntax,
        string spelling,
        BoundLocalReferenceExpression target,
        IReadOnlyList<BoundParameter> parameters,
        IReadOnlyList<PackageCallable>? callables,
        IDiagnosticReporter diagnosticReporter,
        LocalBindingContext locals,
        CancellationToken cancellationToken)
    {
        BooleanBinaryOperator? operation = spelling switch
        {
            "&=" => BooleanBinaryOperator.EagerAnd,
            "^=" => BooleanBinaryOperator.EagerXor,
            "|=" => BooleanBinaryOperator.EagerOr,
            _ => null
        };

        if (operation is null)
        {
            diagnosticReporter.GenerateError(ErrorType.UnsupportedMVPExpression, operatorToken.Span);
            return null;
        }

        BoundExpression? right = BindValue(valueSyntax, BoundType.Bool, parameters, callables, diagnosticReporter, locals, cancellationToken);
        return right is null ? null : new BoundBooleanBinaryExpression(operation.Value, target, right);
    }

    private static BoundExpression? BindCompoundShiftCount(
        ExpressionSyntax expression,
        IReadOnlyList<BoundParameter> parameters,
        IReadOnlyList<PackageCallable>? callables,
        IDiagnosticReporter diagnosticReporter,
        LocalBindingContext locals,
        CancellationToken cancellationToken)
    {
        if (ContainsRuntimeIntegerValue(expression))
        {
            return BindValue(expression, BoundType.Int32, parameters, callables, diagnosticReporter, locals, cancellationToken);
        }

        if (!IntegerConstantExpressionBinder.TryEvaluateUntyped(expression, diagnosticReporter, cancellationToken, out BigInteger count))
        {
            return null;
        }

        if (count < BigInteger.Zero || count >= 32)
        {
            diagnosticReporter.GenerateError(ErrorType.InvalidIntegerShiftCount, expression.Span);
            return null;
        }

        return new BoundIntegerLiteralExpression((int)count);
    }

    private static bool ContainsRuntimeIntegerValue(ExpressionSyntax expression)
        => expression switch
        {
            IdentifierExpressionSyntax or InvocationExpressionSyntax or IncrementDecrementExpressionSyntax => true,
            ParenthesizedExpressionSyntax grouped => ContainsRuntimeIntegerValue(grouped.Expression),
            UnaryExpressionSyntax unary => ContainsRuntimeIntegerValue(unary.Operand),
            BinaryExpressionSyntax binary => ContainsRuntimeIntegerValue(binary.Left) || ContainsRuntimeIntegerValue(binary.Right),
            _ => false
        };

    private static BoundExpression? BindValue(
        ExpressionSyntax expression,
        BoundType expectedType,
        IReadOnlyList<BoundParameter> parameters,
        IReadOnlyList<PackageCallable>? callables,
        IDiagnosticReporter diagnosticReporter,
        LocalBindingContext locals,
        CancellationToken cancellationToken)
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
        LocalBindingContext locals,
        CancellationToken cancellationToken)
    {
        if (!statement.SemicolonToken.IsSourceBacked)
        {
            return null;
        }

        IncrementDecrementExpressionSyntax? syntax = GetStandaloneUpdate(statement.Expression);

        if (syntax is not null)
        {
            BoundIntegerUpdateExpression? update = IntegerUpdateBinder.Bind(syntax, diagnosticReporter, locals);
            return update is null ? null : new BoundIntegerUpdateStatement(update);
        }

        if (callables is not null)
        {
            return PackageInvocationBinder.Bind(statement, callables, parameters, diagnosticReporter, cancellationToken, locals);
        }

        diagnosticReporter.GenerateError(ErrorType.UnsupportedMVPExpression, statement.Span);
        return null;
    }

    private static IncrementDecrementExpressionSyntax? GetStandaloneUpdate(ExpressionSyntax expression)
        => expression switch
        {
            IncrementDecrementExpressionSyntax update => update,
            ParenthesizedExpressionSyntax grouped when grouped.OpenParenthesisToken.IsSourceBacked && grouped.CloseParenthesisToken.IsSourceBacked
                => GetStandaloneUpdate(grouped.Expression),
            _ => null
        };

    private static BoundIfStatement? BindConditionalStatement(
        IfStatementSyntax statement,
        BoundType returnType,
        IReadOnlyList<BoundParameter> parameters,
        IDiagnosticReporter diagnosticReporter,
        IReadOnlyList<PackageCallable>? callables,
        LocalBindingContext locals,
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
            BoundIfStatement? alternative = BindConditionalStatement(elseIf, returnType, parameters, diagnosticReporter, callables, locals, cancellationToken);

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
        IReadOnlyList<PackageCallable>? callables,
        LocalBindingContext locals,
        CancellationToken cancellationToken)
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

        BoundExpression? expression = BindValue(statement.Expression, returnType, parameters, callables, diagnosticReporter, locals, cancellationToken);
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
            BoundWhileStatement or BoundDoWhileStatement or BoundForStatement => true,
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

    /// <summary>
    /// Resolves a source type token using the binder's currently supported concrete type rules.
    /// </summary>
    /// <param name="token">The source token declaring the return or parameter type.</param>
    /// <param name="type">The resolved bound type when the token is supported.</param>
    /// <returns>True when the token denotes a currently supported type.</returns>
    internal static bool TryBindReturnType(SyntaxToken token, out BoundType type)
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

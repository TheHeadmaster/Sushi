using Sushi.Diagnostics;
using Sushi.Parsing.Syntax;

namespace Sushi.Semantics;

/// <summary>
/// Binds the currently supported package-level function semantics.
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

        BoundBlock? body = BindBody(declaration.Body, returnType, diagnosticReporter, cancellationToken);

        return body is null
            ? new FunctionBindResult(null)
            : new FunctionBindResult(new BoundFunction(accessibility, name, returnType, body));
    }

    private static BoundBlock? BindBody(BlockSyntax block, BoundType returnType, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        List<BoundStatement> statements = [];

        foreach (StatementSyntax statement in block.Statements)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (statement is not ReturnStatementSyntax returnStatement)
            {
                return null;
            }

            BoundReturnStatement? boundReturn = BindReturnStatement(returnStatement, returnType, diagnosticReporter, cancellationToken);

            if (boundReturn is null)
            {
                return null;
            }

            statements.Add(boundReturn);
        }

        return new BoundBlock(statements);
    }

    private static BoundReturnStatement? BindReturnStatement(ReturnStatementSyntax statement, BoundType returnType, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(diagnosticReporter);

        if (!statement.ReturnKeyword.IsSourceBacked || !statement.SemicolonToken.IsSourceBacked)
        {
            return null;
        }

        BoundExpression? expression = returnType switch
        {
            BoundType.Int32 => IntegerConstantExpressionBinder.Bind(statement.Expression, diagnosticReporter, cancellationToken),
            BoundType.Bool => BooleanConstantExpressionBinder.Bind(statement.Expression, diagnosticReporter, cancellationToken),
            _ => throw new NotSupportedException($"Return type \"{returnType}\" is not supported by the executable MVP.")
        };

        return expression is null ? null : new BoundReturnStatement(expression);
    }

    private static bool TryBindReturnType(SyntaxToken token, out BoundType type)
    {
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
using System.Numerics;
using Sushi.Diagnostics;
using Sushi.Parsing.Syntax;

namespace Sushi.Semantics;

/// <summary>
/// Binds the currently supported package-level function semantics.
/// </summary>
public sealed class FunctionDeclarationBinder
{
    // Placeholder until diagnostic numbering is settled.
    private const string IntegerConstantOutOfRangeCode = "SUSE012";

    /// <summary>
    /// Binds a function declaration when it belongs to the currently supported executable semantic subset.
    /// </summary>
    /// <param name="declaration">
    /// The declaration to bind.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token used to cancel binding.
    /// </param>
    /// <returns>
    /// The bound function declaration.
    /// </returns>
    public FunctionBindResult Bind(FunctionDeclarationSyntax declaration, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(declaration);

        cancellationToken.ThrowIfCancellationRequested();

        List<SushiDiagnostic> diagnostics = [];

        if (!TryBindAccessibility(declaration.AccessModifier, out Accessibility accessibility)
            || !TryBindInt32ReturnType(declaration.ReturnType)
            || !declaration.OpenParenthesisToken.IsSourceBacked
            || !declaration.CloseParenthesisToken.IsSourceBacked
            || !declaration.Body.OpenBraceToken.IsSourceBacked
            || !declaration.Body.CloseBraceToken.IsSourceBacked)
        {
            return new FunctionBindResult(null, diagnostics);
        }

        string? name = IdentifierBinder.Bind(declaration.Name);

        if (name is null)
        {
            return new FunctionBindResult(null, diagnostics);
        }

        BoundBlock? body = this.BindBody(declaration.Body, diagnostics, cancellationToken);

        return body is null
            ? new FunctionBindResult(null, diagnostics)
            : new FunctionBindResult(new BoundFunction(accessibility, name, BoundIntegerType.Int32, body), diagnostics);
    }

    private BoundBlock? BindBody(BlockSyntax block, List<SushiDiagnostic> diagnostics, CancellationToken cancellationToken)
    {
        List<BoundStatement> statements = [];

        foreach (StatementSyntax statement in block.Statements)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (statement is not ReturnStatementSyntax returnStatement)
            {
                return null;
            }

            BoundReturnStatement? boundReturn = this.BindReturnStatement(returnStatement, diagnostics);

            if (boundReturn is null)
            {
                return null;
            }

            statements.Add(boundReturn);
        }

        return new BoundBlock(statements);
    }

    private BoundReturnStatement? BindReturnStatement(ReturnStatementSyntax statement, List<SushiDiagnostic> diagnostics)
    {
        if (!statement.ReturnKeyword.IsSourceBacked
            || !statement.SemicolonToken.IsSourceBacked
            || statement.Expression is not IntegerLiteralExpressionSyntax literal
            || !IntegerLiteralBinder.TryBindExactValue(literal, out BigInteger exactValue))
        {
            return null;
        }

        if (exactValue > int.MaxValue)
        {
            diagnostics.Add(new SushiDiagnostic(IntegerConstantOutOfRangeCode, "Integer constant is not representable as int32.", DiagnosticSeverity.Error, literal.Span));

            return null;
        }

        return new BoundReturnStatement(new BoundIntegerLiteralExpression((int)exactValue));
    }

    private static bool TryBindInt32ReturnType(SyntaxToken token)
        => IdentifierBinder.Bind(token) is "int32";

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
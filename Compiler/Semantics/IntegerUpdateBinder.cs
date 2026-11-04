using Sushi.Diagnostics;
using Sushi.Parsing.Syntax;

namespace Sushi.Semantics;

/// <summary>
/// Validates built-in integer update expressions against readable and writable
/// storage. The current executable subset supports initialized int32 locals.
/// </summary>
internal static class IntegerUpdateBinder
{
    /// <summary>
    /// Binds a prefix or postfix update without evaluating or duplicating
    /// the operand's storage location.
    /// </summary>
    /// <param name="expression">
    /// The parsed increment or decrement expression.
    /// </param>
    /// <param name="diagnosticReporter">
    /// Receives name-resolution or storage-eligibility diagnostics.
    /// </param>
    /// <param name="locals">
    /// The enclosing function's local-binding context, when available.
    /// </param>
    /// <returns>
    /// A typed update expression, or null when the operand is ineligible.
    /// </returns>
    public static BoundIntegerUpdateExpression? Bind(
        IncrementDecrementExpressionSyntax expression,
        IDiagnosticReporter diagnosticReporter,
        LocalBindingContext? locals)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);

        IdentifierExpressionSyntax? identifier = GetStorageIdentifier(expression.Operand);

        if (identifier is null)
        {
            diagnosticReporter.GenerateErrorWithCustomMessage(
                ErrorType.UnsupportedMVPExpression,
                "Increment and decrement require a readable and writable integer location, not an ordinary expression value.",
                expression.Operand.Span);
            return null;
        }

        string? name = IdentifierBinder.Bind(identifier.IdentifierToken);
        BoundExpression? target = name is null ? null : locals?.Resolve(name, identifier.IdentifierToken.Span);

        if (target is null)
        {
            diagnosticReporter.GenerateError(ErrorType.UnknownIdentifier, identifier.Span);
            return null;
        }

        if (target is not BoundLocalReferenceExpression { Type: BoundType.Int32 } local)
        {
            diagnosticReporter.GenerateErrorWithCustomMessage(
                ErrorType.UnsupportedMVPExpression,
                "Increment and decrement currently require a writable int32 local.",
                identifier.Span);
            return null;
        }

        return new BoundIntegerUpdateExpression(local.Local, expression.IsIncrement, expression.IsPrefix);
    }

    private static IdentifierExpressionSyntax? GetStorageIdentifier(ExpressionSyntax expression)
        => expression switch
        {
            IdentifierExpressionSyntax identifier => identifier,
            ParenthesizedExpressionSyntax grouped when grouped.OpenParenthesisToken.IsSourceBacked && grouped.CloseParenthesisToken.IsSourceBacked
                => GetStorageIdentifier(grouped.Expression),
            _ => null
        };
}
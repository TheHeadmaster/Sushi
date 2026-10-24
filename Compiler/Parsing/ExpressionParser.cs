using Sushi.Diagnostics;
using Sushi.Lexing.Tokenization;
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.Parsing;

/// <summary>
/// Parses expressions using the cursor shared with statement and declaration parsing.
/// Currently recognizes integer literals and preserves missing-expression recovery.
/// </summary>
/// <param name="cursor">
/// The cursor shared by the current parsing session.
/// </param>
/// <param name="cancellationToken">
/// The token used to cancel parsing.
/// </param>
internal sealed class ExpressionParser(ParserCursor cursor, CancellationToken cancellationToken)
{
    private readonly ParserCursor cursor = cursor ?? throw new ArgumentNullException(nameof(cursor));
    private readonly CancellationToken cancellationToken = cancellationToken;

    /// <summary>
    /// Parses an expression at the current cursor position. Until more expression
    /// forms are implemented, only an integer literal is recognized.
    /// Unrecognized significant tokens remain unconsumed for the enclosing grammar.
    /// </summary>
    /// <param name="diagnosticReporter">
    /// Receives diagnostics when expression syntax is missing.
    /// </param>
    /// <returns>
    /// An integer-literal expression, potentially containing a missing literal token.
    /// </returns>
    public ExpressionSyntax ParseExpression(IDiagnosticReporter diagnosticReporter)
    {
        ArgumentNullException.ThrowIfNull(diagnosticReporter);
        this.cancellationToken.ThrowIfCancellationRequested();

        if (this.cursor.TryPeek(out LexToken token) && token.Type is LexTokenType.IntegerLiteral)
        {
            this.cursor.TryAdvance(out _);
            return new IntegerLiteralExpressionSyntax(new SyntaxToken(SyntaxType.IntegerLiteralToken, token));
        }

        int position = this.cursor.Position;
        diagnosticReporter.GenerateErrorWithCustomMessage(ErrorType.ExpectedSyntax, "Expected integer literal expression.", new SourceSpan(this.cursor.Snapshot, position, position));
        return new IntegerLiteralExpressionSyntax(SyntaxToken.Missing(SyntaxType.IntegerLiteralToken, this.cursor.Snapshot, position));
    }
}

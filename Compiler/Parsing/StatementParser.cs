
using Sushi.Diagnostics;
using Sushi.Lexing.Tokenization;
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.Parsing;

/// <summary>
/// Parses braced statement blocks using the cursor shared with declaration parsing.
/// Currently supports return statements and delegates expression syntax to ExpressionParser.
/// </summary>
/// <param name="cursor">
/// The cursor shared by the current parsing session.
/// </param>
/// <param name="cancellationToken">
/// The token used to cancel parsing.
/// </param>
internal sealed class StatementParser(ParserCursor cursor, CancellationToken cancellationToken)
{
    private readonly ParserCursor cursor = cursor ?? throw new ArgumentNullException(nameof(cursor));
    private readonly CancellationToken cancellationToken = cancellationToken;
    private readonly ExpressionParser expressions = new(cursor, cancellationToken);

    /// <summary>
    /// Parses a braced statement block at the current cursor position, synthesizing missing braces when necessary.
    /// </summary>
    /// <param name="diagnosticReporter">
    /// Receives diagnostics for expected block syntax.
    /// </param>
    /// <returns>
    /// The parsed block containing the currently supported statements.
    /// </returns>
    public BlockSyntax ParseBlock(IDiagnosticReporter diagnosticReporter)
    {
        ArgumentNullException.ThrowIfNull(diagnosticReporter);

        SyntaxToken openBrace = this.ParsePunctuation((byte)'{', SyntaxType.OpenBraceToken, "Expected \"{\" to begin function body.", diagnosticReporter);
        List<StatementSyntax> statements = [];

        while (this.IsReturnStatementAhead())
        {
            this.cancellationToken.ThrowIfCancellationRequested();
            statements.Add(this.ParseReturnStatement(diagnosticReporter));
        }

        SyntaxToken closeBrace = this.ParsePunctuation((byte)'}', SyntaxType.CloseBraceToken, "Expected \"}\" to end function body.", diagnosticReporter);
        return new BlockSyntax(openBrace, statements, closeBrace);
    }

    private ReturnStatementSyntax ParseReturnStatement(IDiagnosticReporter diagnosticReporter)
    {
        ArgumentNullException.ThrowIfNull(diagnosticReporter);

        SyntaxToken returnKeyword = this.ParseReturnKeyword(diagnosticReporter);

        ExpressionSyntax? expression = this.cursor.TryPeek(out LexToken next) && this.IsPunctuation(next, (byte)';')
            ? null
            : this.expressions.ParseExpression(diagnosticReporter);

        SyntaxToken semicolon = this.ParsePunctuation((byte)';', SyntaxType.SemicolonToken, "Expected \";\" after return statement.", diagnosticReporter);

        return new ReturnStatementSyntax(returnKeyword, expression, semicolon);
    }

    private SyntaxToken ParseReturnKeyword(IDiagnosticReporter diagnosticReporter)
    {
        ArgumentNullException.ThrowIfNull(diagnosticReporter);

        if (this.cursor.TryConsumeKeyword("return"u8, out LexToken token))
        {
            return new SyntaxToken(SyntaxType.ReturnKeyword, token);
        }

        int position = this.cursor.Position;
        this.AccumulateExpectedDiagnostic("Expected \"return\" keyword.", position, diagnosticReporter);
        return SyntaxToken.Missing(SyntaxType.ReturnKeyword, this.cursor.Snapshot, position);
    }

    private bool IsReturnStatementAhead()
        => this.cursor.TryPeek(out LexToken token)
            && token.Type is LexTokenType.Keyword
            && this.cursor.TokenTextEquals(token, "return"u8);

    private bool IsPunctuation(LexToken token, byte punctuation)
        => token.Type is LexTokenType.Punctuation
            && token.Span.Length == 1
            && this.cursor.Snapshot.Bytes.Span[token.Span.Start] == punctuation;

    private SyntaxToken ParsePunctuation(byte punctuation, SyntaxType type, string diagnosticMessage, IDiagnosticReporter diagnosticReporter)
    {
        ArgumentNullException.ThrowIfNull(diagnosticReporter);

        if (this.cursor.TryConsumePunctuation(punctuation, out LexToken token))
        {
            return new SyntaxToken(type, token);
        }

        int position = this.cursor.Position;
        this.AccumulateExpectedDiagnostic(diagnosticMessage, position, diagnosticReporter);
        return SyntaxToken.Missing(type, this.cursor.Snapshot, position);
    }

    private void AccumulateExpectedDiagnostic(string message, int position, IDiagnosticReporter diagnosticReporter)
    {
        ArgumentNullException.ThrowIfNull(diagnosticReporter);
        diagnosticReporter.GenerateErrorWithCustomMessage(ErrorType.ExpectedSyntax, message, new SourceSpan(this.cursor.Snapshot, position, position));
    }
}
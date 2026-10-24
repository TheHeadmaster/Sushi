using Sushi.Diagnostics;
using Sushi.Lexing.Tokenization;
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.Parsing;

/// <summary>
/// Parses Sushi expressions using precedence climbing over a shared lexical cursor.
/// Supports integer and Boolean literals, grouping, prefix unary operators, and binary arithmetic.
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
    /// Parses an expression at the current cursor position, leaving significant
    /// elements outside the recognized expression unconsumed.
    /// </summary>
    /// <param name="diagnosticReporter">
    /// Receives expression-syntax diagnostics.
    /// </param>
    /// <returns>
    /// The parsed expression, including synthetic syntax required for recovery.
    /// </returns>
    public ExpressionSyntax ParseExpression(IDiagnosticReporter diagnosticReporter)
    {
        ArgumentNullException.ThrowIfNull(diagnosticReporter);
        this.cancellationToken.ThrowIfCancellationRequested();

        return this.ParseExpression(BindingPower.Lowest, diagnosticReporter, out _);
    }

    private ExpressionSyntax ParseExpression(BindingPower minimumBindingPower, IDiagnosticReporter diagnosticReporter, out bool incomplete)
    {
        this.cancellationToken.ThrowIfCancellationRequested();

        ExpressionSyntax left = this.ParsePrefixExpression(diagnosticReporter, out incomplete);

        if (incomplete)
        {
            return left;
        }

        while (this.cursor.TryPeek(out LexToken candidate)
            && this.TryGetBinaryBindingPower(candidate, out BindingPower bindingPower)
            && bindingPower >= minimumBindingPower)
        {
            this.cancellationToken.ThrowIfCancellationRequested();
            this.cursor.TryAdvance(out _);

            SyntaxToken operatorToken = new(SyntaxType.OperatorToken, candidate);
            ExpressionSyntax right = this.ParseExpression(bindingPower + 1, diagnosticReporter, out bool rightIncomplete);

            left = new BinaryExpressionSyntax(left, operatorToken, right);

            if (rightIncomplete)
            {
                incomplete = true;
                return left;
            }
        }

        return left;
    }

    private ExpressionSyntax ParsePrefixExpression(IDiagnosticReporter diagnosticReporter, out bool incomplete)
    {
        this.cancellationToken.ThrowIfCancellationRequested();

        if (!this.cursor.TryPeek(out LexToken token) || !this.TryGetPrefixUnarySyntaxType(token, out SyntaxType syntaxType))
        {
            return this.ParsePrimaryExpression(diagnosticReporter, out incomplete);
        }

        this.cursor.TryAdvance(out _);

        SyntaxToken operatorToken = new(syntaxType, token);
        ExpressionSyntax operand = this.ParseExpression(BindingPower.UnaryPrefix, diagnosticReporter, out incomplete);

        return new UnaryExpressionSyntax(operatorToken, operand);
    }

    private ExpressionSyntax ParsePrimaryExpression(IDiagnosticReporter diagnosticReporter, out bool incomplete)
    {
        this.cancellationToken.ThrowIfCancellationRequested();

        if (this.cursor.TryPeek(out LexToken token))
        {
            if (token.Type is LexTokenType.IntegerLiteral)
            {
                this.cursor.TryAdvance(out _);
                incomplete = false;

                return new IntegerLiteralExpressionSyntax(new SyntaxToken(SyntaxType.IntegerLiteralToken, token));
            }

            if (token.Type is LexTokenType.BooleanLiteral)
            {
                this.cursor.TryAdvance(out _);
                incomplete = false;

                return new BooleanLiteralExpressionSyntax(new SyntaxToken(SyntaxType.BooleanLiteralToken, token));
            }
        }

        if (this.cursor.TryConsumePunctuation((byte)'(', out LexToken openingParenthesis))
        {
            SyntaxToken openToken = new(SyntaxType.OpenParenthesisToken, openingParenthesis);
            ExpressionSyntax expression = this.ParseExpression(BindingPower.Lowest, diagnosticReporter, out bool innerIncomplete);

            SyntaxToken closeToken;

            if (this.cursor.TryConsumePunctuation((byte)')', out LexToken closingParenthesis))
            {
                closeToken = new SyntaxToken(SyntaxType.CloseParenthesisToken, closingParenthesis);
            }
            else
            {
                int position = this.cursor.Position;
                this.ReportExpectedSyntax("Expected \")\" after parenthesized expression.", position, diagnosticReporter);
                closeToken = SyntaxToken.Missing(SyntaxType.CloseParenthesisToken, this.cursor.Snapshot, position);
            }

            incomplete = innerIncomplete || closeToken.IsMissing;
            return new ParenthesizedExpressionSyntax(openToken, expression, closeToken);
        }

        int missingPosition = this.cursor.Position;

        this.ReportExpectedSyntax("Expected expression.", missingPosition, diagnosticReporter);

        incomplete = true;

        return new IntegerLiteralExpressionSyntax(SyntaxToken.Missing(SyntaxType.IntegerLiteralToken, this.cursor.Snapshot, missingPosition));
    }

    private bool TryGetPrefixUnarySyntaxType(LexToken token, out SyntaxType syntaxType)
    {
        if (token.Type is LexTokenType.Keyword && this.cursor.TokenTextEquals(token, "not"u8))
        {
            syntaxType = SyntaxType.NotKeyword;
            return true;
        }

        if (token.Type is LexTokenType.Operator
            && token.Span.Length == 1
            && this.cursor.Snapshot.Bytes.Span[token.Span.Start] is (byte)'+' or (byte)'-' or (byte)'~')
        {
            syntaxType = SyntaxType.OperatorToken;
            return true;
        }

        syntaxType = default;
        return false;
    }

    private bool TryGetBinaryBindingPower(LexToken token, out BindingPower bindingPower)
    {
        if (token.Type is not LexTokenType.Operator || token.Span.Length != 1)
        {
            bindingPower = default;
            return false;
        }

        byte spelling = this.cursor.Snapshot.Bytes.Span[token.Span.Start];

        bindingPower = spelling switch
        {
            (byte)'+' or (byte)'-' => BindingPower.Additive,
            (byte)'*' or (byte)'/' or (byte)'%' => BindingPower.Multiplicative,
            _ => BindingPower.Lowest
        };

        return bindingPower != BindingPower.Lowest;
    }

    private void ReportExpectedSyntax(string message, int position, IDiagnosticReporter diagnosticReporter)
    {
        diagnosticReporter.GenerateErrorWithCustomMessage(
            ErrorType.ExpectedSyntax,
            message,
            new SourceSpan(this.cursor.Snapshot, position, position));
    }
}
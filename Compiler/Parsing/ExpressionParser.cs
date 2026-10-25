using Sushi.Diagnostics;
using Sushi.Lexing.Tokenization;
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.Parsing;

/// <summary>
/// Parses Sushi expressions using precedence climbing over a shared lexical cursor.
/// Supports integer and Boolean literals, grouping, prefix unary operators,
/// arithmetic, shifts, relational and equality comparisons, and logical operators.
/// Diagnoses non-associative comparison chains while preserving recovered syntax.
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

    private static readonly Dictionary<byte, BindingPower> operatorBindingPowers = new()
    {
        { (byte)'+', BindingPower.Additive },
        { (byte)'-', BindingPower.Additive },
        { (byte)'*', BindingPower.Multiplicative },
        { (byte)'/', BindingPower.Multiplicative },
        { (byte)'%', BindingPower.Multiplicative },
        { (byte)'<', BindingPower.Relational },
        { (byte)'>', BindingPower.Relational }
    };

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
            && this.TryGetBinaryOperator(candidate, out BindingPower bindingPower, out SyntaxType syntaxType)
            && bindingPower >= minimumBindingPower)
        {
            this.cancellationToken.ThrowIfCancellationRequested();

            bool invalidChain = this.IsUnparenthesizedComparisonChain(left, bindingPower);

            this.cursor.TryAdvance(out _);

            SyntaxToken operatorToken = new(syntaxType, candidate);
            ExpressionSyntax right = this.ParseExpression(bindingPower + 1, diagnosticReporter, out bool rightIncomplete);

            if (invalidChain)
            {
                diagnosticReporter.GenerateError(ErrorType.NonAssociativeComparison, candidate.Span);
                left = new InvalidComparisonChainSyntax(left, operatorToken, right);
            }
            else
            {
                left = new BinaryExpressionSyntax(left, operatorToken, right);
            }

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

    private bool TryGetBinaryOperator(LexToken token, out BindingPower bindingPower, out SyntaxType syntaxType)
    {
        if (token.Type is LexTokenType.Keyword)
        {
            if (this.cursor.TokenTextEquals(token, "and"u8))
            {
                bindingPower = BindingPower.LogicalAnd;
                syntaxType = SyntaxType.AndKeyword;
                return true;
            }

            if (this.cursor.TokenTextEquals(token, "or"u8))
            {
                bindingPower = BindingPower.LogicalOr;
                syntaxType = SyntaxType.OrKeyword;
                return true;
            }
        }

        if (token.Type is LexTokenType.Operator)
        {
            ReadOnlySpan<byte> spelling = this.cursor.Snapshot.Bytes.Span[token.Span.Start..token.Span.End];

            bindingPower = GetBindingPowerForSpelling(spelling);

            if (bindingPower != BindingPower.Lowest)
            {
                syntaxType = SyntaxType.OperatorToken;
                return true;
            }
        }

        bindingPower = BindingPower.Lowest;
        syntaxType = default;
        return false;
    }

    private static BindingPower GetBindingPowerForSpelling(ReadOnlySpan<byte> spelling)
    {
        if (spelling.Length > 1)
        {
            return GetMultiCharBindingPower(spelling);
        }

        if (spelling.Length == 1)
        {
            return GetSingleCharBindingPower(spelling[0]);
        }

        return BindingPower.Lowest;
    }

    private static BindingPower GetMultiCharBindingPower(ReadOnlySpan<byte> spelling)
    {
        if (spelling.SequenceEqual("<<"u8) || spelling.SequenceEqual(">>"u8))
        {
            return BindingPower.Shift;
        }

        if (spelling.SequenceEqual("<="u8) || spelling.SequenceEqual(">="u8))
        {
            return BindingPower.Relational;
        }

        if (spelling.SequenceEqual("=="u8) || spelling.SequenceEqual("!="u8))
        {
            return BindingPower.Equality;
        }

        return BindingPower.Lowest;
    }

    private static BindingPower GetSingleCharBindingPower(byte spelling)
    {
        if (operatorBindingPowers.TryGetValue(spelling, out BindingPower bindingPower))
        {
            return bindingPower;
        }

        return BindingPower.Lowest;
    }

    private bool IsUnparenthesizedComparisonChain(ExpressionSyntax left, BindingPower bindingPower)
    {
        if (bindingPower is not (BindingPower.Relational or BindingPower.Equality))
        {
            return false;
        }

        SyntaxToken? previousOperator = left switch
        {
            BinaryExpressionSyntax binary => binary.OperatorToken,
            InvalidComparisonChainSyntax chain => chain.OperatorToken,
            _ => null
        };

        return previousOperator is SyntaxToken previous
            && previous.SourceToken is LexToken previousToken
            && this.TryGetBinaryOperator(previousToken, out BindingPower previousPower, out _)
            && previousPower == bindingPower;
    }

    private void ReportExpectedSyntax(string message, int position, IDiagnosticReporter diagnosticReporter)
    {
        diagnosticReporter.GenerateErrorWithCustomMessage(
            ErrorType.ExpectedSyntax,
            message,
            new SourceSpan(this.cursor.Snapshot, position, position));
    }
}
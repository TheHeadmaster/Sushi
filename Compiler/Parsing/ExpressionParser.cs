using Sushi.Diagnostics;
using Sushi.Lexing.Tokenization;
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.Parsing;

/// <summary>
/// Parses Sushi expressions using precedence climbing over a shared lexical cursor.
/// Supports literals, names, invocation syntax, grouping, prefix/postfix integer
/// updates, unary operators, arithmetic, shifts, bitwise operators, comparisons,
/// and logical operators. Diagnoses non-associative comparison chains.
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
        { (byte)'>', BindingPower.Relational },
        { (byte)'&', BindingPower.BitwiseAnd },
        { (byte)'^', BindingPower.BitwiseXor },
        { (byte)'|', BindingPower.BitwiseOr }
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

            left = invalidChain
                ? new InvalidComparisonChainSyntax(left, operatorToken, right)
                : new BinaryExpressionSyntax(left, operatorToken, right);

            if (invalidChain)
            {
                diagnosticReporter.GenerateError(ErrorType.NonAssociativeComparison, candidate.Span);
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

        if (!this.cursor.TryPeek(out LexToken token))
        {
            return this.ParsePostfixExpression(diagnosticReporter, out incomplete);
        }

        if (this.IsIncrementOrDecrement(token))
        {
            this.cursor.TryAdvance(out _);

            ExpressionSyntax operand = this.ParseExpression(BindingPower.UnaryPrefix, diagnosticReporter, out incomplete);
            return new IncrementDecrementExpressionSyntax(new SyntaxToken(SyntaxType.OperatorToken, token), operand, isPrefix: true);
        }

        if (!this.TryGetPrefixUnarySyntaxType(token, out SyntaxType syntaxType))
        {
            return this.ParsePostfixExpression(diagnosticReporter, out incomplete);
        }

        this.cursor.TryAdvance(out _);

        SyntaxToken operatorToken = new(syntaxType, token);
        ExpressionSyntax unaryOperand = this.ParseExpression(BindingPower.UnaryPrefix, diagnosticReporter, out incomplete);

        return new UnaryExpressionSyntax(operatorToken, unaryOperand);
    }

    private ExpressionSyntax ParsePostfixExpression(IDiagnosticReporter diagnosticReporter, out bool incomplete)
    {
        ExpressionSyntax expression = this.ParsePrimaryExpression(diagnosticReporter, out incomplete);

        while (!incomplete && this.cursor.TryPeek(out LexToken token) && this.IsIncrementOrDecrement(token))
        {
            this.cancellationToken.ThrowIfCancellationRequested();
            this.cursor.TryAdvance(out _);

            expression = new IncrementDecrementExpressionSyntax(new SyntaxToken(SyntaxType.OperatorToken, token), expression, isPrefix: false);
        }

        return expression;
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

        if (this.cursor.TryConsumeIdentifier(out LexToken identifier))
        {
            SyntaxType type = identifier.Type is LexTokenType.EscapedIdentifier ? SyntaxType.EscapedIdentifierToken : SyntaxType.IdentifierToken;
            IdentifierExpressionSyntax name = new(new SyntaxToken(type, identifier));

            if (this.cursor.TryConsumePunctuation((byte)'(', out LexToken openingParenthesis))
            {
                return this.ParseInvocation(name, openingParenthesis, diagnosticReporter, out incomplete);
            }

            incomplete = false;
            return name;
        }

        if (this.cursor.TryConsumePunctuation((byte)'(', out LexToken opening))
        {
            SyntaxToken openToken = new(SyntaxType.OpenParenthesisToken, opening);
            ExpressionSyntax expression = this.ParseExpression(BindingPower.Lowest, diagnosticReporter, out bool innerIncomplete);

            SyntaxToken closeToken;

            if (this.cursor.TryConsumePunctuation((byte)')', out LexToken closing))
            {
                closeToken = new SyntaxToken(SyntaxType.CloseParenthesisToken, closing);
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

    private InvocationExpressionSyntax ParseInvocation(IdentifierExpressionSyntax target, LexToken openingParenthesis, IDiagnosticReporter diagnosticReporter, out bool incomplete)
    {
        SyntaxToken openToken = new(SyntaxType.OpenParenthesisToken, openingParenthesis);
        List<CallArgumentSyntax> arguments = [];
        List<SyntaxToken> separators = [];
        incomplete = false;

        if (!this.IsPunctuationAhead((byte)')'))
        {
            while (true)
            {
                this.cancellationToken.ThrowIfCancellationRequested();

                CallArgumentSyntax argument = this.ParseCallArgument(diagnosticReporter, out bool argumentIncomplete);
                arguments.Add(argument);
                incomplete |= argumentIncomplete;

                if (argumentIncomplete || !this.cursor.TryConsumePunctuation((byte)',', out LexToken comma))
                {
                    break;
                }

                separators.Add(new SyntaxToken(SyntaxType.CommaToken, comma));

                if (this.IsPunctuationAhead((byte)')'))
                {
                    int position = this.cursor.Position;
                    this.ReportExpectedSyntax("Expected expression after invocation argument comma.", position, diagnosticReporter);

                    ExpressionSyntax missing = new IntegerLiteralExpressionSyntax(
                        SyntaxToken.Missing(SyntaxType.IntegerLiteralToken, this.cursor.Snapshot, position));

                    arguments.Add(new CallArgumentSyntax(null, null, missing));
                    incomplete = true;
                    break;
                }
            }
        }

        SyntaxToken closeToken;

        if (this.cursor.TryConsumePunctuation((byte)')', out LexToken closingParenthesis))
        {
            closeToken = new SyntaxToken(SyntaxType.CloseParenthesisToken, closingParenthesis);
        }
        else
        {
            int position = this.cursor.Position;
            this.ReportExpectedSyntax("Expected \")\" after invocation arguments.", position, diagnosticReporter);
            closeToken = SyntaxToken.Missing(SyntaxType.CloseParenthesisToken, this.cursor.Snapshot, position);
        }

        incomplete |= closeToken.IsMissing;

        return new InvocationExpressionSyntax(target, openToken, arguments, separators, closeToken);
    }

    private CallArgumentSyntax ParseCallArgument(IDiagnosticReporter diagnosticReporter, out bool incomplete)
    {
        SyntaxToken? name = null;
        SyntaxToken? colon = null;

        if (this.cursor.TryPeek(out LexToken candidate)
            && candidate.Type is LexTokenType.Identifier or LexTokenType.EscapedIdentifier
            && this.cursor.TryPeek(out LexToken separator, lookahead: 1)
            && this.IsPunctuation(separator, (byte)':'))
        {
            this.cursor.TryAdvance(out LexToken identifier);
            this.cursor.TryConsumePunctuation((byte)':', out LexToken colonToken);

            SyntaxType type = identifier.Type is LexTokenType.EscapedIdentifier ? SyntaxType.EscapedIdentifierToken : SyntaxType.IdentifierToken;
            name = new SyntaxToken(type, identifier);
            colon = new SyntaxToken(SyntaxType.ColonToken, colonToken);
        }

        ExpressionSyntax expression = this.ParseExpression(BindingPower.Lowest, diagnosticReporter, out incomplete);
        return new CallArgumentSyntax(name, colon, expression);
    }

    private bool IsPunctuationAhead(byte punctuation)
        => this.cursor.TryPeek(out LexToken token) && this.IsPunctuation(token, punctuation);

    private bool IsPunctuation(LexToken token, byte punctuation)
        => token.Type is LexTokenType.Punctuation
            && token.Span.Length == 1
            && this.cursor.Snapshot.Bytes.Span[token.Span.Start] == punctuation;

    private bool IsIncrementOrDecrement(LexToken token)
        => token.Type is LexTokenType.Operator
            && (this.cursor.TokenTextEquals(token, "++"u8) || this.cursor.TokenTextEquals(token, "--"u8));

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
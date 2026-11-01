using Sushi.Diagnostics;
using Sushi.Lexing.Tokenization;
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.Parsing;

/// <summary>
/// Parses braced statements, conditionals, all supported ordinary loop forms,
/// loop transfers, returns, initialized locals, assignments, and invocations.
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
    /// Parses a braced block, preserving source-backed and recovered punctuation.
    /// </summary>
    /// <param name="diagnosticReporter">
    /// Receives syntax diagnostics.
    /// </param>
    /// <returns>
    /// The parsed statements in source order.
    /// </returns>
    public BlockSyntax ParseBlock(IDiagnosticReporter diagnosticReporter)
    {
        ArgumentNullException.ThrowIfNull(diagnosticReporter);

        SyntaxToken openBrace = this.ParsePunctuation((byte)'{', SyntaxType.OpenBraceToken, "Expected \"{\" to begin statement block.", diagnosticReporter);
        List<StatementSyntax> statements = [];

        while (this.IsStatementAhead())
        {
            this.cancellationToken.ThrowIfCancellationRequested();
            statements.Add(this.ParseStatement(diagnosticReporter));
        }

        SyntaxToken closeBrace = this.ParsePunctuation((byte)'}', SyntaxType.CloseBraceToken, "Expected \"}\" to end statement block.", diagnosticReporter);
        return new BlockSyntax(openBrace, statements, closeBrace);
    }

    private StatementSyntax ParseStatement(IDiagnosticReporter diagnosticReporter)
    {
        if (this.IsKeywordAhead("if"u8))
        {
            return this.ParseIfStatement(diagnosticReporter);
        }

        if (this.IsKeywordAhead("while"u8))
        {
            return this.ParseWhileStatement(diagnosticReporter);
        }

        if (this.IsKeywordAhead("do"u8))
        {
            return this.ParseDoWhileStatement(diagnosticReporter);
        }

        if (this.IsKeywordAhead("for"u8))
        {
            return this.ParseForStatement(diagnosticReporter);
        }

        if (this.IsKeywordAhead("break"u8))
        {
            return this.ParseLoopTransfer("break"u8, SyntaxType.BreakKeyword, diagnosticReporter);
        }

        if (this.IsKeywordAhead("continue"u8))
        {
            return this.ParseLoopTransfer("continue"u8, SyntaxType.ContinueKeyword, diagnosticReporter);
        }

        if (this.IsKeywordAhead("return"u8))
        {
            return this.ParseReturnStatement(diagnosticReporter);
        }

        if (this.IsLocalDeclarationAhead())
        {
            return this.ParseLocalDeclaration(diagnosticReporter);
        }

        if (this.IsLocalAssignmentAhead())
        {
            return this.ParseLocalAssignment(diagnosticReporter);
        }

        return this.ParseExpressionStatement(diagnosticReporter);
    }

    private WhileStatementSyntax ParseWhileStatement(IDiagnosticReporter diagnosticReporter)
    {
        SyntaxToken whileKeyword = this.ParseKeyword("while"u8, SyntaxType.WhileKeyword, "Expected \"while\" keyword.", diagnosticReporter);
        SyntaxToken openParenthesis = this.ParsePunctuation((byte)'(', SyntaxType.OpenParenthesisToken, "Expected \"(\" after while.", diagnosticReporter);
        ExpressionSyntax condition = this.expressions.ParseExpression(diagnosticReporter);
        SyntaxToken closeParenthesis = this.ParsePunctuation((byte)')', SyntaxType.CloseParenthesisToken, "Expected \")\" after while condition.", diagnosticReporter);
        BlockSyntax body = this.ParseBlock(diagnosticReporter);

        return new WhileStatementSyntax(whileKeyword, openParenthesis, condition, closeParenthesis, body);
    }

    private DoWhileStatementSyntax ParseDoWhileStatement(IDiagnosticReporter diagnosticReporter)
    {
        SyntaxToken doKeyword = this.ParseKeyword("do"u8, SyntaxType.DoKeyword, "Expected \"do\" keyword.", diagnosticReporter);
        BlockSyntax body = this.ParseBlock(diagnosticReporter);
        SyntaxToken whileKeyword = this.ParseKeyword("while"u8, SyntaxType.WhileKeyword, "Expected \"while\" after do block.", diagnosticReporter);
        SyntaxToken openParenthesis = this.ParsePunctuation((byte)'(', SyntaxType.OpenParenthesisToken, "Expected \"(\" after trailing while.", diagnosticReporter);
        ExpressionSyntax condition = this.expressions.ParseExpression(diagnosticReporter);
        SyntaxToken closeParenthesis = this.ParsePunctuation((byte)')', SyntaxType.CloseParenthesisToken, "Expected \")\" after do while condition.", diagnosticReporter);

        return new DoWhileStatementSyntax(doKeyword, body, whileKeyword, openParenthesis, condition, closeParenthesis);
    }

    private ForStatementSyntax ParseForStatement(IDiagnosticReporter diagnosticReporter)
    {
        SyntaxToken forKeyword = this.ParseKeyword("for"u8, SyntaxType.ForKeyword, "Expected \"for\" keyword.", diagnosticReporter);
        SyntaxToken openParenthesis = this.ParsePunctuation((byte)'(', SyntaxType.OpenParenthesisToken, "Expected \"(\" after for.", diagnosticReporter);

        IReadOnlyList<ForHeaderOperationSyntax> initialization = this.ParseForHeaderOperations((byte)';', allowDeclarations: true, diagnosticReporter);
        SyntaxToken initializationSemicolon = this.ParsePunctuation((byte)';', SyntaxType.SemicolonToken, "Expected \";\" after for initialization.", diagnosticReporter);

        ExpressionSyntax? condition = this.IsPunctuationAhead((byte)';') ? null : this.expressions.ParseExpression(diagnosticReporter);
        SyntaxToken conditionSemicolon = this.ParsePunctuation((byte)';', SyntaxType.SemicolonToken, "Expected \";\" after for condition.", diagnosticReporter);

        IReadOnlyList<ForHeaderOperationSyntax> iteration = this.ParseForHeaderOperations((byte)')', allowDeclarations: false, diagnosticReporter);
        SyntaxToken closeParenthesis = this.ParsePunctuation((byte)')', SyntaxType.CloseParenthesisToken, "Expected \")\" after for iteration.", diagnosticReporter);
        BlockSyntax body = this.ParseBlock(diagnosticReporter);

        return new ForStatementSyntax(forKeyword, openParenthesis, initialization, initializationSemicolon, condition, conditionSemicolon, iteration, closeParenthesis, body);
    }

    private IReadOnlyList<ForHeaderOperationSyntax> ParseForHeaderOperations(byte closingPunctuation, bool allowDeclarations, IDiagnosticReporter diagnosticReporter)
    {
        List<ForHeaderOperationSyntax> operations = [];

        if (this.IsPunctuationAhead(closingPunctuation))
        {
            return operations;
        }

        while (true)
        {
            this.cancellationToken.ThrowIfCancellationRequested();

            if (!this.IsForHeaderOperationAhead(allowDeclarations))
            {
                this.AccumulateExpectedDiagnostic("Expected an initialized declaration, assignment, or integer update in for header.", this.cursor.Position, diagnosticReporter);
                this.SkipInvalidForHeaderClause(closingPunctuation);
                break;
            }

            operations.Add(this.ParseForHeaderOperation(allowDeclarations, diagnosticReporter));

            if (!this.cursor.TryConsumePunctuation((byte)',', out _))
            {
                break;
            }

            if (this.IsPunctuationAhead(closingPunctuation))
            {
                this.AccumulateExpectedDiagnostic("Expected a for-header operation after comma.", this.cursor.Position, diagnosticReporter);
                break;
            }
        }

        return operations;
    }

    private ForHeaderOperationSyntax ParseForHeaderOperation(bool allowDeclarations, IDiagnosticReporter diagnosticReporter)
    {
        if (allowDeclarations && this.IsForHeaderDeclarationAhead())
        {
            this.cursor.TryConsumeIdentifier(out LexToken type);
            this.cursor.TryConsumeIdentifier(out LexToken name);
            this.cursor.TryAdvance(out LexToken equals);

            ExpressionSyntax initializer = this.expressions.ParseExpression(diagnosticReporter);

            return new ForHeaderOperationSyntax(
                new SyntaxToken(GetIdentifierSyntaxType(type), type),
                new SyntaxToken(GetIdentifierSyntaxType(name), name),
                new SyntaxToken(SyntaxType.OperatorToken, equals),
                initializer);
        }

        if (this.IsPrefixUpdateAhead())
        {
            this.cursor.TryAdvance(out LexToken operation);
            this.cursor.TryConsumeIdentifier(out LexToken name);

            return new ForHeaderOperationSyntax(
                null,
                new SyntaxToken(GetIdentifierSyntaxType(name), name),
                new SyntaxToken(SyntaxType.OperatorToken, operation),
                null,
                isPrefix: true);
        }

        this.cursor.TryConsumeIdentifier(out LexToken target);
        this.cursor.TryAdvance(out LexToken operatorToken);

        ExpressionSyntax? value = this.IsIncrementOrDecrement(operatorToken)
            ? null
            : this.expressions.ParseExpression(diagnosticReporter);

        return new ForHeaderOperationSyntax(
            null,
            new SyntaxToken(GetIdentifierSyntaxType(target), target),
            new SyntaxToken(SyntaxType.OperatorToken, operatorToken),
            value);
    }

    private bool IsForHeaderOperationAhead(bool allowDeclarations)
        => allowDeclarations && this.IsForHeaderDeclarationAhead()
            || this.IsPrefixUpdateAhead()
            || this.IsPostfixUpdateAhead()
            || this.IsLocalAssignmentAhead();

    private bool IsForHeaderDeclarationAhead()
    {
        if (!this.cursor.TryPeek(out LexToken type)
            || type.Type is not (LexTokenType.Identifier or LexTokenType.EscapedIdentifier)
            || !this.cursor.TryPeek(out LexToken name, lookahead: 1)
            || name.Type is not (LexTokenType.Identifier or LexTokenType.EscapedIdentifier)
            || !this.cursor.TryPeek(out LexToken equals, lookahead: 2))
        {
            return false;
        }

        return equals.Type is LexTokenType.Operator && this.cursor.TokenTextEquals(equals, "="u8);
    }

    private bool IsPrefixUpdateAhead()
        => this.cursor.TryPeek(out LexToken operation)
            && this.IsIncrementOrDecrement(operation)
            && this.cursor.TryPeek(out LexToken name, lookahead: 1)
            && name.Type is LexTokenType.Identifier or LexTokenType.EscapedIdentifier;

    private bool IsPostfixUpdateAhead()
        => this.cursor.TryPeek(out LexToken name)
            && name.Type is LexTokenType.Identifier or LexTokenType.EscapedIdentifier
            && this.cursor.TryPeek(out LexToken operation, lookahead: 1)
            && this.IsIncrementOrDecrement(operation);

    private bool IsIncrementOrDecrement(LexToken operation)
        => operation.Type is LexTokenType.Operator
            && (this.cursor.TokenTextEquals(operation, "++"u8) || this.cursor.TokenTextEquals(operation, "--"u8));

    private void SkipInvalidForHeaderClause(byte closingPunctuation)
    {
        int parenthesisDepth = 0;

        while (this.cursor.TryPeek(out LexToken token))
        {
            this.cancellationToken.ThrowIfCancellationRequested();

            if (this.IsPunctuation(token, (byte)'('))
            {
                parenthesisDepth++;
            }
            else if (this.IsPunctuation(token, (byte)')'))
            {
                if (parenthesisDepth == 0)
                {
                    break;
                }

                parenthesisDepth--;
            }

            if (parenthesisDepth == 0 && this.IsPunctuation(token, closingPunctuation))
            {
                break;
            }

            this.cursor.TryAdvance(out _);
        }
    }

    private LoopTransferStatementSyntax ParseLoopTransfer(ReadOnlySpan<byte> spelling, SyntaxType type, IDiagnosticReporter diagnosticReporter)
    {
        List<SyntaxToken> keywords = [];

        do
        {
            this.cancellationToken.ThrowIfCancellationRequested();

            this.cursor.TryConsumeKeyword(spelling, out LexToken token);
            keywords.Add(new SyntaxToken(type, token));
        }
        while (this.IsKeywordAhead(spelling));

        SyntaxToken semicolon = this.ParsePunctuation((byte)';', SyntaxType.SemicolonToken, "Expected \";\" after loop transfer.", diagnosticReporter);
        return new LoopTransferStatementSyntax(keywords, semicolon);
    }

    private LocalDeclarationStatementSyntax ParseLocalDeclaration(IDiagnosticReporter diagnosticReporter)
    {
        this.cursor.TryConsumeIdentifier(out LexToken type);
        this.cursor.TryConsumeIdentifier(out LexToken name);

        SyntaxToken typeToken = new(GetIdentifierSyntaxType(type), type);
        SyntaxToken nameToken = new(GetIdentifierSyntaxType(name), name);
        SyntaxToken equalsToken;

        if (this.cursor.TryPeek(out LexToken candidate)
            && candidate.Type is LexTokenType.Operator
            && this.cursor.TokenTextEquals(candidate, "="u8))
        {
            this.cursor.TryAdvance(out _);
            equalsToken = new SyntaxToken(SyntaxType.OperatorToken, candidate);
        }
        else
        {
            int position = this.cursor.Position;
            this.AccumulateExpectedDiagnostic("Expected \"=\" in initialized local declaration.", position, diagnosticReporter);
            equalsToken = SyntaxToken.Missing(SyntaxType.OperatorToken, this.cursor.Snapshot, position);
        }

        ExpressionSyntax initializer = this.expressions.ParseExpression(diagnosticReporter);
        SyntaxToken semicolon = this.ParsePunctuation((byte)';', SyntaxType.SemicolonToken, "Expected \";\" after local declaration.", diagnosticReporter);

        return new LocalDeclarationStatementSyntax(typeToken, nameToken, equalsToken, initializer, semicolon);
    }

    private LocalAssignmentStatementSyntax ParseLocalAssignment(IDiagnosticReporter diagnosticReporter)
    {
        this.cursor.TryConsumeIdentifier(out LexToken name);
        this.cursor.TryAdvance(out LexToken assignment);

        SyntaxToken nameToken = new(GetIdentifierSyntaxType(name), name);
        SyntaxToken operatorToken = new(SyntaxType.OperatorToken, assignment);
        ExpressionSyntax value = this.expressions.ParseExpression(diagnosticReporter);
        SyntaxToken semicolon = this.ParsePunctuation((byte)';', SyntaxType.SemicolonToken, "Expected \";\" after assignment.", diagnosticReporter);

        return new LocalAssignmentStatementSyntax(nameToken, operatorToken, value, semicolon);
    }

    private ExpressionStatementSyntax ParseExpressionStatement(IDiagnosticReporter diagnosticReporter)
    {
        ExpressionSyntax expression = this.expressions.ParseExpression(diagnosticReporter);
        SyntaxToken semicolon = this.ParsePunctuation((byte)';', SyntaxType.SemicolonToken, "Expected \";\" after invocation statement.", diagnosticReporter);

        return new ExpressionStatementSyntax(expression, semicolon);
    }

    private IfStatementSyntax ParseIfStatement(IDiagnosticReporter diagnosticReporter)
    {
        SyntaxToken ifKeyword = this.ParseKeyword("if"u8, SyntaxType.IfKeyword, "Expected \"if\" keyword.", diagnosticReporter);
        SyntaxToken openParenthesis = this.ParsePunctuation((byte)'(', SyntaxType.OpenParenthesisToken, "Expected \"(\" after if.", diagnosticReporter);
        ExpressionSyntax condition = this.expressions.ParseExpression(diagnosticReporter);
        SyntaxToken closeParenthesis = this.ParsePunctuation((byte)')', SyntaxType.CloseParenthesisToken, "Expected \")\" after if condition.", diagnosticReporter);
        BlockSyntax thenBlock = this.ParseBlock(diagnosticReporter);

        SyntaxToken? elseKeyword = null;
        BlockSyntax? elseBlock = null;
        IfStatementSyntax? elseIf = null;

        if (this.cursor.TryConsumeKeyword("else"u8, out LexToken elseToken))
        {
            elseKeyword = new SyntaxToken(SyntaxType.ElseKeyword, elseToken);

            if (this.IsKeywordAhead("if"u8))
            {
                elseIf = this.ParseIfStatement(diagnosticReporter);
            }
            else
            {
                elseBlock = this.ParseBlock(diagnosticReporter);
            }
        }

        return new IfStatementSyntax(ifKeyword, openParenthesis, condition, closeParenthesis, thenBlock, elseKeyword, elseBlock, elseIf);
    }

    private ReturnStatementSyntax ParseReturnStatement(IDiagnosticReporter diagnosticReporter)
    {
        ArgumentNullException.ThrowIfNull(diagnosticReporter);

        SyntaxToken returnKeyword = this.ParseKeyword("return"u8, SyntaxType.ReturnKeyword, "Expected \"return\" keyword.", diagnosticReporter);

        ExpressionSyntax? expression = this.cursor.TryPeek(out LexToken next) && this.IsPunctuation(next, (byte)';')
            ? null
            : this.expressions.ParseExpression(diagnosticReporter);

        SyntaxToken semicolon = this.ParsePunctuation((byte)';', SyntaxType.SemicolonToken, "Expected \";\" after return statement.", diagnosticReporter);

        return new ReturnStatementSyntax(returnKeyword, expression, semicolon);
    }

    private SyntaxToken ParseKeyword(ReadOnlySpan<byte> spelling, SyntaxType syntaxType, string diagnosticMessage, IDiagnosticReporter diagnosticReporter)
    {
        if (this.cursor.TryConsumeKeyword(spelling, out LexToken token))
        {
            return new SyntaxToken(syntaxType, token);
        }

        int position = this.cursor.Position;
        this.AccumulateExpectedDiagnostic(diagnosticMessage, position, diagnosticReporter);
        return SyntaxToken.Missing(syntaxType, this.cursor.Snapshot, position);
    }

    private bool IsStatementAhead()
        => this.IsKeywordAhead("return"u8)
            || this.IsKeywordAhead("if"u8)
            || this.IsKeywordAhead("while"u8)
            || this.IsKeywordAhead("do"u8)
            || this.IsKeywordAhead("for"u8)
            || this.IsKeywordAhead("break"u8)
            || this.IsKeywordAhead("continue"u8)
            || this.IsLocalDeclarationAhead()
            || this.IsLocalAssignmentAhead()
            || this.IsInvocationAhead();

    private bool IsLocalDeclarationAhead()
    {
        if (!this.cursor.TryPeek(out LexToken type)
            || type.Type is not (LexTokenType.Identifier or LexTokenType.EscapedIdentifier)
            || !(this.cursor.TokenTextEquals(type, "int32"u8) || this.cursor.TokenTextEquals(type, "bool"u8)))
        {
            return false;
        }

        return this.cursor.TryPeek(out LexToken name, lookahead: 1)
            && name.Type is LexTokenType.Identifier or LexTokenType.EscapedIdentifier;
    }

    private bool IsLocalAssignmentAhead()
    {
        if (!this.cursor.TryPeek(out LexToken name)
            || name.Type is not (LexTokenType.Identifier or LexTokenType.EscapedIdentifier)
            || !this.cursor.TryPeek(out LexToken assignment, lookahead: 1))
        {
            return false;
        }

        return this.IsAssignmentOperator(assignment);
    }

    private bool IsAssignmentOperator(LexToken token)
    {
        if (token.Type is not LexTokenType.Operator)
        {
            return false;
        }

        ReadOnlySpan<byte> spelling = this.cursor.Snapshot.Bytes.Span[token.Span.Start..token.Span.End];

        return spelling.Length switch
        {
            1 => spelling[0] == (byte)'=',
            2 => spelling[1] == (byte)'='
                && spelling[0] is (byte)'+' or (byte)'-' or (byte)'*' or (byte)'/' or (byte)'%' or (byte)'&' or (byte)'^' or (byte)'|',
            3 => spelling[2] == (byte)'='
                && spelling[0] is (byte)'<' or (byte)'>'
                && spelling[1] == spelling[0],
            _ => false
        };
    }

    private bool IsInvocationAhead()
    {
        if (!this.cursor.TryPeek(out LexToken target) || target.Type is not (LexTokenType.Identifier or LexTokenType.EscapedIdentifier))
        {
            return false;
        }

        return this.cursor.TryPeek(out LexToken opening, lookahead: 1)
            && this.IsPunctuation(opening, (byte)'(');
    }

    private bool IsKeywordAhead(ReadOnlySpan<byte> keyword)
        => this.cursor.TryPeek(out LexToken token)
            && token.Type is LexTokenType.Keyword
            && this.cursor.TokenTextEquals(token, keyword);

    private bool IsPunctuationAhead(byte punctuation)
        => this.cursor.TryPeek(out LexToken token) && this.IsPunctuation(token, punctuation);

    private bool IsPunctuation(LexToken token, byte punctuation)
        => token.Type is LexTokenType.Punctuation
            && token.Span.Length == 1
            && this.cursor.Snapshot.Bytes.Span[token.Span.Start] == punctuation;

    private static SyntaxType GetIdentifierSyntaxType(LexToken token)
        => token.Type is LexTokenType.EscapedIdentifier ? SyntaxType.EscapedIdentifierToken : SyntaxType.IdentifierToken;

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
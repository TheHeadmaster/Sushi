using Sushi.Diagnostics;
using Sushi.Lexing;
using Sushi.Lexing.Tokenization;
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.Parsing;

/// <summary>
/// Parses Sushi lexical elements into structured concrete syntax while retaining
/// source boundaries needed by adjacency-sensitive grammar productions.
/// </summary>
public sealed class SourceParser
{
    /// <summary>
    /// Parses supported file-level syntax and identifies the remaining source without attempting recovery across grammar productions not yet implemented.
    /// </summary>
    /// <param name="lexerResult">
    /// The complete lexical result, including trivia and recovery elements.
    /// </param>
    /// <param name="diagnosticReporter">
    /// The diagnostic reporter used to accumulate diagnostics.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel parsing.
    /// </param>
    /// <returns>
    /// A source file syntax containing recognized leading declarations and its unparsed remainder.
    /// </returns>
    public static ParserResult ParseSourceFile(LexerResult lexerResult, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lexerResult);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);
        cancellationToken.ThrowIfCancellationRequested();

        IDiagnosticReporter localDiagnostics = new DiagnosticReporter();
        ParserState state = new(lexerResult, cancellationToken);
        SourceFileSyntax sourceFile = state.ParseSourceFile(localDiagnostics);

        diagnosticReporter.CommitReporter(localDiagnostics);
        return new ParserResult(new ConcreteSyntaxTree(lexerResult, sourceFile));
    }

    /// <summary>
    /// Parses a function declaration beginning at the first significant lexical element.
    /// </summary>
    /// <param name="lexerResult">
    /// The complete lexical result, including trivia and recovery elements.
    /// </param>
    /// <param name="diagnosticReporter">
    /// The diagnostic reporter used to accumulate diagnostics.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel parsing.
    /// </param>
    /// <returns>
    /// The parsed function declaration.
    /// </returns>
    public static ParserResult ParseFunctionDeclaration(LexerResult lexerResult, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lexerResult);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);
        cancellationToken.ThrowIfCancellationRequested();

        IDiagnosticReporter localDiagnostics = new DiagnosticReporter();
        ParserState state = new(lexerResult, cancellationToken);
        FunctionDeclarationSyntax declaration = state.ParseFunctionDeclaration(localDiagnostics);

        diagnosticReporter.CommitReporter(localDiagnostics);
        return new ParserResult(new ConcreteSyntaxTree(lexerResult, declaration));
    }

    /// <summary>
    /// Parses a package declaration beginning at the first significant lexical element.
    /// </summary>
    /// <param name="lexerResult">
    /// The complete lexical result, including trivia and recovery elements.
    /// </param>
    /// <param name="diagnosticReporter">
    /// The diagnostic reporter used to accumulate diagnostics.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel parsing.
    /// </param>
    /// <returns>
    /// The parsed package declaration.
    /// </returns>
    public static ParserResult ParsePackageDeclaration(LexerResult lexerResult, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lexerResult);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);
        cancellationToken.ThrowIfCancellationRequested();

        IDiagnosticReporter localDiagnostics = new DiagnosticReporter();
        ParserState state = new(lexerResult, cancellationToken);
        PackageDeclarationSyntax declaration = state.ParsePackageDeclaration(localDiagnostics);

        diagnosticReporter.CommitReporter(localDiagnostics);
        return new ParserResult(new ConcreteSyntaxTree(lexerResult, declaration));
    }

    /// <summary>
    /// Parses a namespace declaration beginning at the first significant lexical element.
    /// </summary>
    /// <param name="lexerResult">
    /// The complete lexical result, including trivia and recovery elements.
    /// </param>
    /// <param name="diagnosticReporter">
    /// The diagnostic reporter used to accumulate diagnostics.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel parsing.
    /// </param>
    /// <returns>
    /// The parsed namespace declaration.
    /// </returns>
    public static ParserResult ParseNamespaceDeclaration(LexerResult lexerResult, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lexerResult);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);
        cancellationToken.ThrowIfCancellationRequested();

        IDiagnosticReporter localDiagnostics = new DiagnosticReporter();
        ParserState state = new(lexerResult, cancellationToken);
        NamespaceDeclarationSyntax declaration = state.ParseNamespaceDeclaration(localDiagnostics);

        diagnosticReporter.CommitReporter(localDiagnostics);
        return new ParserResult(new ConcreteSyntaxTree(lexerResult, declaration));
    }

    private sealed class ParserState(LexerResult lexerResult, CancellationToken cancellationToken)
    {
        private readonly ParserCursor cursor = new(lexerResult, cancellationToken);
        private readonly CancellationToken cancellationToken = cancellationToken;

        /// <summary>
        /// Parses supported file-level syntax and preserves the remaining source without interpreting unsupported productions.
        /// </summary>
        /// <param name="diagnosticReporter">
        /// The reporter used to accumulate diagnostics.
        /// </param>
        /// <returns>
        /// The parsed source file, including its unparsed remainder when present.
        /// </returns>
        public SourceFileSyntax ParseSourceFile(IDiagnosticReporter diagnosticReporter)
        {
            ArgumentNullException.ThrowIfNull(diagnosticReporter);
            this.cancellationToken.ThrowIfCancellationRequested();

            IDiagnosticReporter localDiagnostics = new DiagnosticReporter();
            List<PackageDeclarationSyntax> packageDeclarations = [];

            while (this.IsPackageDeclarationAhead())
            {
                this.cancellationToken.ThrowIfCancellationRequested();
                packageDeclarations.Add(this.ParsePackageDeclaration(localDiagnostics));
            }

            List<NamespaceDeclarationSyntax> namespaceDeclarations = [];

            while (this.IsNamespaceDeclarationAhead())
            {
                this.cancellationToken.ThrowIfCancellationRequested();
                namespaceDeclarations.Add(this.ParseNamespaceDeclaration(localDiagnostics));
            }

            List<FunctionDeclarationSyntax> functionDeclarations = [];

            while (this.IsFunctionDeclarationAhead())
            {
                this.cancellationToken.ThrowIfCancellationRequested();
                functionDeclarations.Add(this.ParseFunctionDeclaration(localDiagnostics));
            }

            SourceSpan? unparsedContentSpan = this.cursor.TryPeek(out LexToken next)
                ? new SourceSpan(this.cursor.Snapshot, next.Span.Start, this.cursor.Snapshot.SourceLength)
                : null;

            diagnosticReporter.CommitReporter(localDiagnostics);
            return new SourceFileSyntax(this.cursor.Snapshot, packageDeclarations, namespaceDeclarations, functionDeclarations, unparsedContentSpan);
        }

        public FunctionDeclarationSyntax ParseFunctionDeclaration(IDiagnosticReporter diagnosticReporter)
        {
            ArgumentNullException.ThrowIfNull(diagnosticReporter);
            this.cancellationToken.ThrowIfCancellationRequested();

            IDiagnosticReporter localDiagnostics = new DiagnosticReporter();

            SyntaxToken accessModifier = this.ParseAccessModifier(localDiagnostics);
            SyntaxToken returnType = this.ParseReturnType(localDiagnostics);
            SyntaxToken name = this.ParseNameComponent("Expected function name.", skipUnknown: false, localDiagnostics);
            SyntaxToken openParenthesis = this.ParsePunctuation((byte)'(', SyntaxType.OpenParenthesisToken, "Expected \"(\" after function name.", localDiagnostics);
            SyntaxToken closeParenthesis = this.ParsePunctuation((byte)')', SyntaxType.CloseParenthesisToken, "Expected \")\" after function parameter list.", localDiagnostics);
            BlockSyntax body = this.ParseBlock(localDiagnostics);

            diagnosticReporter.CommitReporter(localDiagnostics);
            return new FunctionDeclarationSyntax(accessModifier, returnType, name, openParenthesis, closeParenthesis, body);
        }

        public NamespaceDeclarationSyntax ParseNamespaceDeclaration(IDiagnosticReporter diagnosticReporter)
        {
            ArgumentNullException.ThrowIfNull(diagnosticReporter);
            this.cancellationToken.ThrowIfCancellationRequested();

            IDiagnosticReporter localDiagnostics = new DiagnosticReporter();

            SyntaxToken namespaceKeyword = this.ParseNamespaceKeyword(localDiagnostics);
            QualifiedNameSyntax name = this.ParseQualifiedName("namespace", localDiagnostics);
            SyntaxToken semicolonToken = this.ParseSemicolon("Expected \";\" after namespace declaration.", localDiagnostics);

            diagnosticReporter.CommitReporter(localDiagnostics);
            return new NamespaceDeclarationSyntax(namespaceKeyword, name, semicolonToken);
        }

        public PackageDeclarationSyntax ParsePackageDeclaration(IDiagnosticReporter diagnosticReporter)
        {
            ArgumentNullException.ThrowIfNull(diagnosticReporter);
            this.cancellationToken.ThrowIfCancellationRequested();

            IDiagnosticReporter localDiagnostics = new DiagnosticReporter();

            SyntaxToken packageKeyword = this.ParsePackageKeyword(localDiagnostics);
            QualifiedNameSyntax name = this.ParseQualifiedName("package", localDiagnostics);
            SyntaxToken semicolonToken = this.ParseSemicolon("Expected \";\" after package declaration.", localDiagnostics);

            diagnosticReporter.CommitReporter(localDiagnostics);
            return new PackageDeclarationSyntax(packageKeyword, name, semicolonToken);
        }

        private SyntaxToken ParseAccessModifier(IDiagnosticReporter diagnosticReporter)
        {
            ArgumentNullException.ThrowIfNull(diagnosticReporter);

            if (this.cursor.TryPeek(out LexToken token) && this.IsAccessModifier(token))
            {
                this.cursor.TryAdvance(out _);
                return new SyntaxToken(SyntaxType.AccessModifierKeyword, token);
            }

            int position = this.cursor.Position;
            this.AccumulateExpectedDiagnostic("Expected access modifier.", position, diagnosticReporter);
            return SyntaxToken.Missing(SyntaxType.AccessModifierKeyword, this.cursor.Snapshot, position);
        }

        private SyntaxToken ParseReturnType(IDiagnosticReporter diagnosticReporter)
        {
            ArgumentNullException.ThrowIfNull(diagnosticReporter);

            if (this.cursor.TryPeek(out LexToken token))
            {
                SyntaxType type = token.Type switch
                {
                    LexTokenType.Identifier => SyntaxType.IdentifierToken,
                    LexTokenType.EscapedIdentifier => SyntaxType.EscapedIdentifierToken,
                    LexTokenType.Keyword when this.cursor.TokenTextEquals(token, "void"u8) => SyntaxType.VoidKeyword,
                    _ => default
                };

                if (type != default)
                {
                    this.cursor.TryAdvance(out _);
                    return new SyntaxToken(type, token);
                }
            }

            int position = this.cursor.Position;
            this.AccumulateExpectedDiagnostic("Expected function return type.", position, diagnosticReporter);
            return SyntaxToken.Missing(SyntaxType.IdentifierToken, this.cursor.Snapshot, position);
        }

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

        private BlockSyntax ParseBlock(IDiagnosticReporter diagnosticReporter)
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
            IntegerLiteralExpressionSyntax expression = this.ParseIntegerLiteralExpression(diagnosticReporter);
            SyntaxToken semicolon = this.ParseSemicolon("Expected \";\" after return statement.", diagnosticReporter);

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

        private IntegerLiteralExpressionSyntax ParseIntegerLiteralExpression(IDiagnosticReporter diagnosticReporter)
        {
            ArgumentNullException.ThrowIfNull(diagnosticReporter);

            if (this.cursor.TryPeek(out LexToken token) && token.Type is LexTokenType.IntegerLiteral)
            {
                this.cursor.TryAdvance(out _);
                return new IntegerLiteralExpressionSyntax(new SyntaxToken(SyntaxType.IntegerLiteralToken, token));
            }

            int position = this.cursor.Position;
            this.AccumulateExpectedDiagnostic("Expected integer literal expression.", position, diagnosticReporter);
            return new IntegerLiteralExpressionSyntax(SyntaxToken.Missing(SyntaxType.IntegerLiteralToken, this.cursor.Snapshot, position));
        }

        private SyntaxToken ParseNamespaceKeyword(IDiagnosticReporter diagnosticReporter)
        {
            ArgumentNullException.ThrowIfNull(diagnosticReporter);

            if (this.cursor.TryConsumeKeyword("namespace"u8, out LexToken token))
            {
                return new SyntaxToken(SyntaxType.NamespaceKeyword, token);
            }

            int position = this.cursor.Position;
            this.AccumulateExpectedDiagnostic("Expected \"namespace\" keyword.", position, diagnosticReporter);
            return SyntaxToken.Missing(SyntaxType.NamespaceKeyword, this.cursor.Snapshot, position);
        }

        private SyntaxToken ParsePackageKeyword(IDiagnosticReporter diagnosticReporter)
        {
            ArgumentNullException.ThrowIfNull(diagnosticReporter);

            if (this.cursor.TryConsumeKeyword("package"u8, out LexToken token))
            {
                return new SyntaxToken(SyntaxType.PackageKeyword, token);
            }

            int position = this.cursor.Position;
            this.AccumulateExpectedDiagnostic("Expected \"package\" keyword.", position, diagnosticReporter);
            return SyntaxToken.Missing(SyntaxType.PackageKeyword, this.cursor.Snapshot, position);
        }

        private QualifiedNameSyntax ParseQualifiedName(string declarationKind, IDiagnosticReporter diagnosticReporter)
        {
            ArgumentNullException.ThrowIfNull(diagnosticReporter);

            List<SyntaxToken> segments = [this.ParseNameComponent($"Expected {declarationKind} name.", skipUnknown: false, diagnosticReporter)];
            List<SyntaxToken> separators = [];

            while (this.cursor.TryConsumePunctuation((byte)'.', out LexToken dot, skipUnknown: true))
            {
                SyntaxToken separator = new(SyntaxType.DotToken, dot);

                this.RequireSyntacticAdjacency(segments[^1], separator, $"\".\" must immediately follow the preceding {declarationKind}-name component.", diagnosticReporter);

                SyntaxToken segment = this.ParseNameComponent($"Expected identifier after \".\" in {declarationKind} name.", skipUnknown: true, diagnosticReporter);

                this.RequireSyntacticAdjacency(separator, segment, $"A {declarationKind}-name component must immediately follow \".\".", diagnosticReporter);

                separators.Add(separator);
                segments.Add(segment);
            }

            return new QualifiedNameSyntax(segments, separators);
        }

        private SyntaxToken ParseNameComponent(string diagnosticMessage, bool skipUnknown, IDiagnosticReporter diagnosticReporter)
        {
            ArgumentNullException.ThrowIfNull(diagnosticReporter);

            if (this.cursor.TryConsumeIdentifier(out LexToken token, skipUnknown))
            {
                SyntaxType type = token.Type is LexTokenType.EscapedIdentifier
                    ? SyntaxType.EscapedIdentifierToken
                    : SyntaxType.IdentifierToken;

                return new SyntaxToken(type, token);
            }

            int position = this.cursor.Position;
            this.AccumulateExpectedDiagnostic(diagnosticMessage, position, diagnosticReporter);
            return SyntaxToken.Missing(SyntaxType.IdentifierToken, this.cursor.Snapshot, position);
        }

        private SyntaxToken ParseSemicolon(string diagnosticMessage, IDiagnosticReporter diagnosticReporter)
            => this.ParsePunctuation((byte)';', SyntaxType.SemicolonToken, diagnosticMessage, diagnosticReporter);

        private bool IsFunctionDeclarationAhead()
            => this.cursor.TryPeek(out LexToken accessModifier)
                && this.IsAccessModifier(accessModifier)
                && this.cursor.TryPeek(out LexToken returnType, lookahead: 1)
                && this.IsSupportedReturnType(returnType)
                && this.cursor.TryPeek(out LexToken name, lookahead: 2)
                && name.Type is LexTokenType.Identifier or LexTokenType.EscapedIdentifier
                && this.cursor.TryPeek(out LexToken openParenthesis, lookahead: 3)
                && this.IsPunctuation(openParenthesis, (byte)'(');

        private bool IsAccessModifier(LexToken token)
            => token.Type is LexTokenType.Keyword
                && (this.cursor.TokenTextEquals(token, "public"u8)
                || this.cursor.TokenTextEquals(token, "internal"u8)
                || this.cursor.TokenTextEquals(token, "package"u8)
                || this.cursor.TokenTextEquals(token, "protected"u8)
                || this.cursor.TokenTextEquals(token, "private"u8));

        private bool IsSupportedReturnType(LexToken token)
            => token.Type is LexTokenType.Identifier or LexTokenType.EscapedIdentifier
                || (token.Type is LexTokenType.Keyword && this.cursor.TokenTextEquals(token, "void"u8));

        private bool IsPunctuation(LexToken token, byte punctuation)
            => token.Type is LexTokenType.Punctuation
                && token.Span.Length == 1
                && this.cursor.Snapshot.Bytes.Span[token.Span.Start] == punctuation;

        private bool IsReturnStatementAhead()
            => this.cursor.TryPeek(out LexToken token)
                && token.Type is LexTokenType.Keyword
                && this.cursor.TokenTextEquals(token, "return"u8);

        private bool IsNamespaceDeclarationAhead()
            => this.cursor.TryPeek(out LexToken token)
                && token.Type is LexTokenType.Keyword
                && this.cursor.TokenTextEquals(token, "namespace"u8);

        private bool IsPackageDeclarationAhead()
            => this.cursor.TryPeek(out LexToken token)
                && token.Type is LexTokenType.Keyword
                && this.cursor.TokenTextEquals(token, "package"u8);

        /// <summary>
        /// Diagnoses source-backed material separating components whose grammar requires adjacency.
        /// Missing components have their own diagnostics and do not generate adjacency errors.
        /// </summary>
        /// <param name="left">
        /// The preceding syntax token.
        /// </param>
        /// <param name="right">
        /// The following syntax token.
        /// </param>
        /// <param name="message">
        /// The diagnostic message when the tokens are separated.
        /// </param>
        /// <param name="diagnosticReporter">
        /// The reporter used to accumulate the diagnostic.
        /// </param>
        private void RequireSyntacticAdjacency(SyntaxToken left, SyntaxToken right, string message, IDiagnosticReporter diagnosticReporter)
        {
            ArgumentNullException.ThrowIfNull(diagnosticReporter);

            if (!left.IsSourceBacked || !right.IsSourceBacked)
            {
                return;
            }

            SourceSpan leftSpan = left.Span;
            SourceSpan rightSpan = right.Span;

            if (leftSpan.End == rightSpan.Start)
            {
                return;
            }

            if (leftSpan.End > rightSpan.Start)
            {
                throw new InvalidOperationException("Adjacency participants must occur in source order without overlapping.");
            }

            diagnosticReporter.GenerateErrorWithCustomMessage(
                ErrorType.RequiredSyntacticAdjacency,
                message,
                new SourceSpan(this.cursor.Snapshot, leftSpan.End, rightSpan.Start));
        }

        private void AccumulateExpectedDiagnostic(string message, int position, IDiagnosticReporter diagnosticReporter)
        {
            ArgumentNullException.ThrowIfNull(diagnosticReporter);
            diagnosticReporter.GenerateErrorWithCustomMessage(ErrorType.ExpectedSyntax, message, new SourceSpan(this.cursor.Snapshot, position, position));
        }
    }
}
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
    /// A source file syntax containing recognized leading package declarations and its unparsed remainder.
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
        private readonly LexerResult lexerResult = lexerResult;

        private readonly CancellationToken cancellationToken = cancellationToken;

        private int position;

        /// <summary>
        /// Parses supported file-level syntax and identifies the remaining source without attempting recovery across grammar productions not yet implemented.
        /// </summary>
        /// <param name="diagnosticReporter">
        /// The reporter used to accumulate diagnostics.
        /// </param>
        /// <returns>
        /// A source file containing recognized leading package declarations and its unparsed remainder.
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

            SourceSpan? unparsedContentSpan = this.TryPeekToken(skipUnknown: false, out LexToken next, out _)
                ? new SourceSpan(this.lexerResult.Snapshot, next.Span.Start, this.lexerResult.Snapshot.SourceLength)
                : null;

            diagnosticReporter.CommitReporter(localDiagnostics);

            return new SourceFileSyntax(this.lexerResult.Snapshot, packageDeclarations, namespaceDeclarations, functionDeclarations, unparsedContentSpan);
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

            if (this.TryPeekToken(skipUnknown: false, out LexToken token, out int index) && this.IsAccessModifier(token))
            {
                this.position = index + 1;
                return new SyntaxToken(SyntaxType.AccessModifierKeyword, token);
            }

            int position = this.GetCurrentPosition();

            this.AccumulateExpectedDiagnostic("Expected access modifier.", position, diagnosticReporter);

            return SyntaxToken.Missing(SyntaxType.AccessModifierKeyword, this.lexerResult.Snapshot, position);
        }

        private SyntaxToken ParseReturnType(IDiagnosticReporter diagnosticReporter)
        {
            ArgumentNullException.ThrowIfNull(diagnosticReporter);

            if (this.TryPeekToken(skipUnknown: false, out LexToken token, out int index))
            {
                SyntaxType type = token.Type switch
                {
                    LexTokenType.Identifier => SyntaxType.IdentifierToken,
                    LexTokenType.EscapedIdentifier => SyntaxType.EscapedIdentifierToken,
                    LexTokenType.Keyword when this.TokenTextEquals(token, "void"u8) => SyntaxType.VoidKeyword,
                    _ => default
                };

                if (type != default)
                {
                    this.position = index + 1;

                    return new SyntaxToken(type, token);
                }
            }

            int position = this.GetCurrentPosition();

            this.AccumulateExpectedDiagnostic("Expected function return type.", position, diagnosticReporter);

            return SyntaxToken.Missing(SyntaxType.IdentifierToken, this.lexerResult.Snapshot, position);
        }

        private SyntaxToken ParsePunctuation(byte punctuation, SyntaxType type, string diagnosticMessage, IDiagnosticReporter diagnosticReporter)
        {
            ArgumentNullException.ThrowIfNull(diagnosticReporter);

            if (this.TryConsumePunctuation(punctuation, type, skipUnknown: false, out SyntaxToken token))
            {
                return token;
            }

            int position = this.GetCurrentPosition();

            this.AccumulateExpectedDiagnostic(diagnosticMessage, position, diagnosticReporter);

            return SyntaxToken.Missing(type, this.lexerResult.Snapshot, position);
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

            if (this.TryPeekToken(skipUnknown: false, out LexToken token, out int index) && token.Type is LexTokenType.Keyword && this.TokenTextEquals(token, "return"u8))
            {
                this.position = index + 1;
                return new SyntaxToken(SyntaxType.ReturnKeyword, token);
            }

            int position = this.GetCurrentPosition();

            this.AccumulateExpectedDiagnostic("Expected \"return\" keyword.", position, diagnosticReporter);

            return SyntaxToken.Missing(SyntaxType.ReturnKeyword, this.lexerResult.Snapshot, position);
        }

        private IntegerLiteralExpressionSyntax ParseIntegerLiteralExpression(IDiagnosticReporter diagnosticReporter)
        {
            ArgumentNullException.ThrowIfNull(diagnosticReporter);

            if (this.TryPeekToken(skipUnknown: false, out LexToken token, out int index) && token.Type is LexTokenType.IntegerLiteral)
            {
                this.position = index + 1;
                return new IntegerLiteralExpressionSyntax(new SyntaxToken(SyntaxType.IntegerLiteralToken, token));
            }

            int position = this.GetCurrentPosition();

            this.AccumulateExpectedDiagnostic("Expected integer literal expression.", position, diagnosticReporter);

            return new IntegerLiteralExpressionSyntax(SyntaxToken.Missing(SyntaxType.IntegerLiteralToken, this.lexerResult.Snapshot, position));
        }

        private SyntaxToken ParseNamespaceKeyword(IDiagnosticReporter diagnosticReporter)
        {
            ArgumentNullException.ThrowIfNull(diagnosticReporter);

            if (this.TryPeekToken(skipUnknown: false, out LexToken token, out int index) && token.Type is LexTokenType.Keyword && this.TokenTextEquals(token, "namespace"u8))
            {
                this.position = index + 1;

                return new SyntaxToken(SyntaxType.NamespaceKeyword, token);
            }

            int position = this.GetCurrentPosition();

            this.AccumulateExpectedDiagnostic("Expected \"namespace\" keyword.", position, diagnosticReporter);

            return SyntaxToken.Missing(SyntaxType.NamespaceKeyword, this.lexerResult.Snapshot, position);
        }

        private SyntaxToken ParsePackageKeyword(IDiagnosticReporter diagnosticReporter)
        {
            ArgumentNullException.ThrowIfNull(diagnosticReporter);

            if (this.TryPeekToken(skipUnknown: false, out LexToken token, out int index) && token.Type is LexTokenType.Keyword && this.TokenTextEquals(token, "package"u8))
            {
                this.position = index + 1;

                return new SyntaxToken(SyntaxType.PackageKeyword, token);
            }

            int position = this.GetCurrentPosition();

            this.AccumulateExpectedDiagnostic("Expected \"package\" keyword.", position, diagnosticReporter);

            return SyntaxToken.Missing(SyntaxType.PackageKeyword, this.lexerResult.Snapshot, position);
        }

        private QualifiedNameSyntax ParseQualifiedName(string declarationKind, IDiagnosticReporter diagnosticReporter)
        {
            ArgumentNullException.ThrowIfNull(diagnosticReporter);

            List<SyntaxToken> segments = [this.ParseNameComponent($"Expected {declarationKind} name.", skipUnknown: false, diagnosticReporter)];
            List<SyntaxToken> separators = [];

            while (this.TryConsumePunctuation((byte)'.', SyntaxType.DotToken, skipUnknown: true, out SyntaxToken separator))
            {
                // A source-backed name and dot may be separated only by a diagnosed recovery gap.

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

            if (this.TryPeekToken(skipUnknown, out LexToken token, out int index))
            {
                SyntaxType type = token.Type switch
                {
                    LexTokenType.Identifier => SyntaxType.IdentifierToken,
                    LexTokenType.EscapedIdentifier => SyntaxType.EscapedIdentifierToken,
                    _ => default
                };

                if (type != default)
                {
                    this.position = index + 1;

                    return new SyntaxToken(type, token);
                }
            }

            int position = this.GetCurrentPosition();

            this.AccumulateExpectedDiagnostic(diagnosticMessage, position, diagnosticReporter);

            return SyntaxToken.Missing(SyntaxType.IdentifierToken, this.lexerResult.Snapshot, position);
        }

        private SyntaxToken ParseSemicolon(string diagnosticMessage, IDiagnosticReporter diagnosticReporter)
        {
            ArgumentNullException.ThrowIfNull(diagnosticReporter);

            return this.ParsePunctuation((byte)';', SyntaxType.SemicolonToken, diagnosticMessage, diagnosticReporter);
        }

        /// <summary>
        /// Looks ahead without advancing the parser. Unknown elements are skipped only
        /// when the calling grammar production is attempting a supported recovery.
        /// </summary>
        /// <param name="skipUnknown">
        /// Whether to skip unknown elements.
        /// </param>
        /// <param name="token">
        /// If true is returned, this contains the token found from the lookahead. Otherwise, default.
        /// </param>
        /// <param name="index">
        /// If true is returned, this contains the index that the token was found at. Otherwise, 0.
        /// </param>
        /// <returns>
        /// True if the token was found. False otherwise.
        /// </returns>
        private bool TryPeekToken(bool skipUnknown, out LexToken token, out int index)
        {
            for (index = this.position; index < this.lexerResult.Tokens.Count; index++)
            {
                this.cancellationToken.ThrowIfCancellationRequested();

                LexToken candidate = this.lexerResult.Tokens[index];

                if (IsTrivia(candidate.Type) || (skipUnknown && candidate.Type is LexTokenType.Unknown))
                {
                    continue;
                }

                token = candidate;
                return true;
            }

            token = default;
            return false;
        }

        private bool TryPeekSignificantToken(int offset, out LexToken token)
        {
            int currentOffset = 0;

            for (int index = this.position; index < this.lexerResult.Tokens.Count; index++)
            {
                this.cancellationToken.ThrowIfCancellationRequested();

                LexToken candidate = this.lexerResult.Tokens[index];

                if (IsTrivia(candidate.Type))
                {
                    continue;
                }

                if (currentOffset++ == offset)
                {
                    token = candidate;
                    return true;
                }
            }

            token = default;
            return false;
        }

        private bool IsFunctionDeclarationAhead()
        {
            return this.TryPeekSignificantToken(0, out LexToken accessModifier)
                && this.IsAccessModifier(accessModifier)
                && this.TryPeekSignificantToken(1, out LexToken returnType)
                && this.IsSupportedReturnType(returnType)
                && this.TryPeekSignificantToken(2, out LexToken name)
                && name.Type is LexTokenType.Identifier or LexTokenType.EscapedIdentifier
                && this.TryPeekSignificantToken(3, out LexToken openParenthesis)
                && this.IsPunctuation(openParenthesis, (byte)'(');
        }

        private bool IsAccessModifier(LexToken token)
            => token.Type is LexTokenType.Keyword
                && (this.TokenTextEquals(token, "public"u8)
                || this.TokenTextEquals(token, "internal"u8)
                || this.TokenTextEquals(token, "package"u8)
                || this.TokenTextEquals(token, "protected"u8)
                || this.TokenTextEquals(token, "private"u8));

        private bool IsSupportedReturnType(LexToken token)
            => token.Type is LexTokenType.Identifier
                or LexTokenType.EscapedIdentifier
                || (token.Type is LexTokenType.Keyword
                && this.TokenTextEquals(token, "void"u8));

        private bool IsPunctuation(LexToken token, byte punctuation)
            => token.Type is LexTokenType.Punctuation
                && token.Span.Length == 1
                && this.lexerResult.Snapshot.Bytes.Span[token.Span.Start] == punctuation;

        private bool IsReturnStatementAhead()
            => this.TryPeekToken(skipUnknown: false, out LexToken token, out _)
            && token.Type is LexTokenType.Keyword
            && this.TokenTextEquals(token, "return"u8);

        private bool TryConsumePunctuation(byte punctuation, SyntaxType type, bool skipUnknown, out SyntaxToken syntaxToken)
        {
            if (this.TryPeekToken(skipUnknown, out LexToken token, out int index)
                && token.Type is LexTokenType.Punctuation
                && token.Span.Length == 1
                && this.lexerResult.Snapshot.Bytes.Span[token.Span.Start] == punctuation)
            {
                this.position = index + 1;

                syntaxToken = new SyntaxToken(type, token);
                return true;
            }

            syntaxToken = default;
            return false;
        }

        /// <summary>
        /// Diagnoses source-backed material separating two components whose grammar requires direct adjacency.
        /// Synthetic missing components receive their own structural diagnostics and cannot cause adjacency errors.
        /// </summary>
        /// <param name="left">
        /// The token on the left-hand side.
        /// </param>
        /// <param name="right">
        /// The token on the right-hand side.
        /// </param>
        /// <param name="message">
        /// The message to show if the adjacency rules are violated.
        /// </param>
        /// <param name="diagnosticReporter">
        /// The diagnostic reporter used to accumulate diagnostics.
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

            diagnosticReporter.GenerateErrorWithCustomMessage(ErrorType.RequiredSyntacticAdjacency, message, new SourceSpan(this.lexerResult.Snapshot, leftSpan.End, rightSpan.Start));
        }

        private bool TokenTextEquals(LexToken token, ReadOnlySpan<byte> expected) => token.Span.Length == expected.Length && this.lexerResult.Snapshot.Bytes.Span[token.Span.Start..token.Span.End].SequenceEqual(expected);

        private int GetCurrentPosition() => this.TryPeekToken(skipUnknown: false, out LexToken token, out _) ? token.Span.Start : this.lexerResult.Snapshot.SourceLength;

        private void AccumulateExpectedDiagnostic(string message, int position, IDiagnosticReporter diagnosticReporter)
        {
            ArgumentNullException.ThrowIfNull(diagnosticReporter);

            diagnosticReporter.GenerateErrorWithCustomMessage(
                        ErrorType.ExpectedSyntax,
                        message,
                        new SourceSpan(this.lexerResult.Snapshot, position, position));
        }

        private bool IsNamespaceDeclarationAhead()
            => this.TryPeekToken(skipUnknown: false, out LexToken token, out _)
                && token.Type is LexTokenType.Keyword
                && this.TokenTextEquals(token, "namespace"u8);

        private bool IsPackageDeclarationAhead()
            => this.TryPeekToken(skipUnknown: false, out LexToken token, out _)
                && token.Type is LexTokenType.Keyword
                && this.TokenTextEquals(token, "package"u8);

        private static bool IsTrivia(LexTokenType type) =>
            type is LexTokenType.Whitespace
            or LexTokenType.LineTerminator
            or LexTokenType.LineComment
            or LexTokenType.DocumentationLineComment
            or LexTokenType.BlockComment;
    }
}
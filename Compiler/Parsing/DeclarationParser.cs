using Sushi.Diagnostics;
using Sushi.Lexing.Tokenization;
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.Parsing;

/// <summary>
/// Parses package, namespace, and function declarations from a shared lexical cursor.
/// </summary>
/// <param name="cursor">
/// The cursor shared with the source-file parser.
/// </param>
/// <param name="cancellationToken">
/// The token used to cancel parsing.
/// </param>
internal sealed class DeclarationParser(ParserCursor cursor, CancellationToken cancellationToken)
{
    private readonly ParserCursor cursor = cursor ?? throw new ArgumentNullException(nameof(cursor));
    private readonly CancellationToken cancellationToken = cancellationToken;
    private readonly StatementParser statements = new(cursor, cancellationToken);

    /// <summary>
    /// Parses a function declaration at the current cursor position.
    /// </summary>
    /// <param name="diagnosticReporter">
    /// Receives syntax diagnostics for the declaration and its body.
    /// </param>
    /// <returns>
    /// The parsed function declaration, including missing syntax required for recovery.
    /// </returns>
    public FunctionDeclarationSyntax ParseFunctionDeclaration(IDiagnosticReporter diagnosticReporter)
    {
        ArgumentNullException.ThrowIfNull(diagnosticReporter);
        this.cancellationToken.ThrowIfCancellationRequested();

        IDiagnosticReporter localDiagnostics = new DiagnosticReporter();

        SyntaxToken accessModifier = this.ParseAccessModifier(localDiagnostics);
        SyntaxToken returnType = this.ParseReturnType(localDiagnostics);
        SyntaxToken name = this.ParseNameComponent("Expected function name.", skipUnknown: false, localDiagnostics);
        SyntaxToken openParenthesis = this.ParsePunctuation((byte)'(', SyntaxType.OpenParenthesisToken, "Expected \"(\" after function name.", localDiagnostics);
        (IReadOnlyList<ParameterSyntax> parameters, IReadOnlyList<SyntaxToken> separators) = this.ParseParameters(localDiagnostics);
        SyntaxToken closeParenthesis = this.ParsePunctuation((byte)')', SyntaxType.CloseParenthesisToken, "Expected \")\" after function parameter list.", localDiagnostics);
        BlockSyntax body = this.statements.ParseBlock(localDiagnostics);

        diagnosticReporter.CommitReporter(localDiagnostics);
        return new FunctionDeclarationSyntax(accessModifier, returnType, name, openParenthesis, parameters, separators, closeParenthesis, body);
    }

    /// <summary>Parses a namespace declaration at the current cursor position.</summary>
    /// <param name="diagnosticReporter">Receives syntax diagnostics for the declaration.</param>
    /// <returns>The parsed namespace declaration.</returns>
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

    /// <summary>Parses a package declaration at the current cursor position.</summary>
    /// <param name="diagnosticReporter">Receives syntax diagnostics for the declaration.</param>
    /// <returns>The parsed package declaration.</returns>
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

    private (IReadOnlyList<ParameterSyntax> Parameters, IReadOnlyList<SyntaxToken> Separators) ParseParameters(IDiagnosticReporter diagnosticReporter)
    {
        List<ParameterSyntax> parameters = [];
        List<SyntaxToken> separators = [];

        if (!this.cursor.TryPeek(out LexToken next) || this.IsPunctuation(next, (byte)')') || this.IsPunctuation(next, (byte)'{'))
        {
            return (parameters, separators);
        }

        while (true)
        {
            this.cancellationToken.ThrowIfCancellationRequested();
            SyntaxToken type = this.ParseParameterType(diagnosticReporter);
            SyntaxToken name = this.ParseNameComponent("Expected parameter name.", skipUnknown: false, diagnosticReporter);
            parameters.Add(new ParameterSyntax(type, name));

            if (this.cursor.TryConsumePunctuation((byte)',', out LexToken comma))
            {
                separators.Add(new SyntaxToken(SyntaxType.CommaToken, comma));

                if (!this.cursor.TryPeek(out next) || this.IsPunctuation(next, (byte)')') || this.IsPunctuation(next, (byte)'{'))
                {
                    int position = this.cursor.Position;
                    this.AccumulateExpectedDiagnostic("Expected parameter declaration after comma.", position, diagnosticReporter);
                    SyntaxToken missingType = SyntaxToken.Missing(SyntaxType.IdentifierToken, this.cursor.Snapshot, position);
                    SyntaxToken missingName = SyntaxToken.Missing(SyntaxType.IdentifierToken, this.cursor.Snapshot, position);
                    parameters.Add(new ParameterSyntax(missingType, missingName));
                    break;
                }

                continue;
            }

            if (!this.cursor.TryPeek(out next) || this.IsPunctuation(next, (byte)')') || this.IsPunctuation(next, (byte)'{'))
            {
                break;
            }

            if (next.Type is not (LexTokenType.Identifier or LexTokenType.EscapedIdentifier))
            {
                break;
            }

            int missingCommaPosition = this.cursor.Position;
            this.AccumulateExpectedDiagnostic("Expected comma between parameters.", missingCommaPosition, diagnosticReporter);
            separators.Add(SyntaxToken.Missing(SyntaxType.CommaToken, this.cursor.Snapshot, missingCommaPosition));
        }

        return (parameters, separators);
    }

    private SyntaxToken ParseParameterType(IDiagnosticReporter diagnosticReporter)
    {
        if (this.cursor.TryConsumeIdentifier(out LexToken token))
        {
            SyntaxType type = token.Type is LexTokenType.EscapedIdentifier ? SyntaxType.EscapedIdentifierToken : SyntaxType.IdentifierToken;
            return new SyntaxToken(type, token);
        }

        int position = this.cursor.Position;
        this.AccumulateExpectedDiagnostic("Expected parameter type.", position, diagnosticReporter);
        return SyntaxToken.Missing(SyntaxType.IdentifierToken, this.cursor.Snapshot, position);
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

    /// <summary>
    /// Determines whether the next significant elements form the supported function-declaration prefix.
    /// </summary>
    /// <returns>
    /// Whether a function declaration begins at the cursor.
    /// </returns>
    public bool IsFunctionDeclarationAhead()
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

    /// <summary>
    /// Determines whether the next significant token introduces a namespace declaration.
    /// </summary>
    /// <returns>
    /// Whether a namespace declaration begins at the cursor.
    /// </returns>
    public bool IsNamespaceDeclarationAhead()
        => this.cursor.TryPeek(out LexToken token)
            && token.Type is LexTokenType.Keyword
            && this.cursor.TokenTextEquals(token, "namespace"u8);

    /// <summary>
    /// Determines whether the next significant token introduces a package declaration.
    /// </summary>
    /// <returns>
    /// Whether a package declaration begins at the cursor.
    /// </returns>
    public bool IsPackageDeclarationAhead()
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

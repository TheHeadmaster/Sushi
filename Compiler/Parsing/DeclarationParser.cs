
using Sushi.Diagnostics;
using Sushi.Lexing.Tokenization;
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.Parsing;

/// <summary>
/// Parses package, namespace, function, and activity declarations from a shared lexical cursor.
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

    /// <summary>
    /// Parses an activity declaration and preserves unsupported members for later validation.
    /// </summary>
    /// <param name="diagnosticReporter">
    /// Receives syntax and recovery diagnostics.
    /// </param>
    /// <returns>
    /// The parsed activity declaration with members in source order.
    /// </returns>
    public ActivityDeclarationSyntax ParseActivityDeclaration(IDiagnosticReporter diagnosticReporter)
    {
        ArgumentNullException.ThrowIfNull(diagnosticReporter);
        this.cancellationToken.ThrowIfCancellationRequested();

        IDiagnosticReporter localDiagnostics = new DiagnosticReporter();

        SyntaxToken accessModifier = this.ParseAccessModifier(localDiagnostics);
        SyntaxToken? rootKeyword = this.cursor.TryConsumeKeyword("root"u8, out LexToken root)
            ? new SyntaxToken(SyntaxType.RootKeyword, root)
            : null;

        SyntaxToken activityKeyword = this.ParseKeyword("activity"u8, SyntaxType.ActivityKeyword, "Expected \"activity\" keyword.", localDiagnostics);
        SyntaxToken name = this.ParseNameComponent("Expected activity name.", skipUnknown: false, localDiagnostics);
        SyntaxToken colonToken = this.ParsePunctuation((byte)':', SyntaxType.ColonToken, "Expected \":\" before activity lifetime.", localDiagnostics);
        SyntaxToken lifetimeKeyword = this.ParseActivityLifetime(localDiagnostics);
        SyntaxToken openBraceToken = this.ParsePunctuation((byte)'{', SyntaxType.OpenBraceToken, "Expected \"{\" to begin activity body.", localDiagnostics);

        List<ActivityMemberSyntax> members = [];

        while (this.cursor.TryPeek(out LexToken next) && !this.IsPunctuation(next, (byte)'}'))
        {
            this.cancellationToken.ThrowIfCancellationRequested();

            if (this.IsActivityMethodDeclarationAhead())
            {
                members.Add(this.ParseActivityMethodDeclaration(localDiagnostics));
            }
            else
            {
                members.Add(this.ParseUnsupportedActivityMember(localDiagnostics));
            }
        }

        SyntaxToken closeBraceToken = this.ParsePunctuation((byte)'}', SyntaxType.CloseBraceToken, "Expected \"}\" to end activity body.", localDiagnostics);

        diagnosticReporter.CommitReporter(localDiagnostics);

        return new ActivityDeclarationSyntax(
            accessModifier, rootKeyword, activityKeyword, name, colonToken,
            lifetimeKeyword, openBraceToken, members, closeBraceToken);
    }

    /// <summary>
    /// Parses a namespace declaration at the current cursor position.
    /// </summary>
    /// <param name="diagnosticReporter">
    /// Receives syntax diagnostics for the declaration.
    /// </param>
    /// <returns>
    /// The parsed namespace declaration.
    /// </returns>
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

    /// <summary>
    /// Parses a package declaration at the current cursor position.
    /// </summary>
    /// <param name="diagnosticReporter">
    /// Receives syntax diagnostics for the declaration.
    /// </param>
    /// <returns>
    /// The parsed package declaration.
    /// </returns>
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

    private ActivityMethodDeclarationSyntax ParseActivityMethodDeclaration(IDiagnosticReporter diagnosticReporter)
    {
        SyntaxToken accessModifier = this.ParseAccessModifier(diagnosticReporter);
        SyntaxToken lifecycleModifier = this.ParseLifecycleModifier(diagnosticReporter);
        SyntaxToken returnType = this.ParseReturnType(diagnosticReporter);
        SyntaxToken name = this.ParseNameComponent("Expected activity method name.", skipUnknown: false, diagnosticReporter);
        SyntaxToken openParenthesis = this.ParsePunctuation((byte)'(', SyntaxType.OpenParenthesisToken, "Expected \"(\" after activity method name.", diagnosticReporter);
        (IReadOnlyList<ParameterSyntax> parameters, IReadOnlyList<SyntaxToken> separators) = this.ParseParameters(diagnosticReporter);
        SyntaxToken closeParenthesis = this.ParsePunctuation((byte)')', SyntaxType.CloseParenthesisToken, "Expected \")\" after activity method parameters.", diagnosticReporter);
        BlockSyntax body = this.statements.ParseBlock(diagnosticReporter);

        return new ActivityMethodDeclarationSyntax(
            accessModifier, lifecycleModifier, returnType, name, openParenthesis,
            parameters, separators, closeParenthesis, body);
    }

    private UnsupportedActivityMemberSyntax ParseUnsupportedActivityMember(IDiagnosticReporter diagnosticReporter)
    {
        int start = this.cursor.Position;
        int end = start;
        int braceDepth = 0;

        while (this.cursor.TryPeek(out LexToken next))
        {
            this.cancellationToken.ThrowIfCancellationRequested();

            if (braceDepth == 0 && this.IsPunctuation(next, (byte)'}'))
            {
                break;
            }

            if (end > start && braceDepth == 0 && this.IsActivityMethodDeclarationAhead())
            {
                break;
            }

            this.cursor.TryAdvance(out LexToken consumed);
            end = consumed.Span.End;

            if (this.IsPunctuation(consumed, (byte)'{'))
            {
                braceDepth++;
            }
            else if (this.IsPunctuation(consumed, (byte)'}'))
            {
                braceDepth--;

                if (braceDepth == 0)
                {
                    break;
                }
            }
            else if (braceDepth == 0 && this.IsPunctuation(consumed, (byte)';'))
            {
                break;
            }
        }

        SourceSpan span = new(this.cursor.Snapshot, start, end);

        diagnosticReporter.GenerateErrorWithCustomMessage(
            ErrorType.ExpectedSyntax,
            "Unsupported activity member syntax.",
            span);

        return new UnsupportedActivityMemberSyntax(span);
    }

    private SyntaxToken ParseActivityLifetime(IDiagnosticReporter diagnosticReporter)
    {
        if (this.cursor.TryConsumeKeyword("task"u8, out LexToken task))
        {
            return new SyntaxToken(SyntaxType.TaskKeyword, task);
        }

        if (this.cursor.TryConsumeKeyword("persistent"u8, out LexToken persistent))
        {
            return new SyntaxToken(SyntaxType.PersistentKeyword, persistent);
        }

        int position = this.cursor.Position;
        this.AccumulateExpectedDiagnostic("Expected \"task\" or \"persistent\" activity lifetime.", position, diagnosticReporter);
        return SyntaxToken.Missing(SyntaxType.TaskKeyword, this.cursor.Snapshot, position);
    }

    private SyntaxToken ParseLifecycleModifier(IDiagnosticReporter diagnosticReporter)
    {
        if (this.cursor.TryConsumeKeyword("entry"u8, out LexToken entry))
        {
            return new SyntaxToken(SyntaxType.EntryKeyword, entry);
        }

        if (this.cursor.TryConsumeKeyword("stop"u8, out LexToken stop))
        {
            return new SyntaxToken(SyntaxType.StopKeyword, stop);
        }

        int position = this.cursor.Position;
        this.AccumulateExpectedDiagnostic("Expected \"entry\" or \"stop\" modifier.", position, diagnosticReporter);
        return SyntaxToken.Missing(SyntaxType.EntryKeyword, this.cursor.Snapshot, position);
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

    private SyntaxToken ParseKeyword(ReadOnlySpan<byte> spelling, SyntaxType type, string diagnosticMessage, IDiagnosticReporter diagnosticReporter)
    {
        if (this.cursor.TryConsumeKeyword(spelling, out LexToken token))
        {
            return new SyntaxToken(type, token);
        }

        int position = this.cursor.Position;
        this.AccumulateExpectedDiagnostic(diagnosticMessage, position, diagnosticReporter);
        return SyntaxToken.Missing(type, this.cursor.Snapshot, position);
    }

    private SyntaxToken ParseNamespaceKeyword(IDiagnosticReporter diagnosticReporter)
        => this.ParseKeyword("namespace"u8, SyntaxType.NamespaceKeyword, "Expected \"namespace\" keyword.", diagnosticReporter);

    private SyntaxToken ParsePackageKeyword(IDiagnosticReporter diagnosticReporter)
        => this.ParseKeyword("package"u8, SyntaxType.PackageKeyword, "Expected \"package\" keyword.", diagnosticReporter);

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
    /// Determines whether the next significant elements form a supported function declaration prefix.
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

    /// <summary>
    /// Determines whether the next declaration starts with an activity or root activity prefix.
    /// </summary>
    /// <returns>
    /// Whether the parser should recognize an activity declaration.
    /// </returns>
    public bool IsActivityDeclarationAhead()
    {
        if (!this.cursor.TryPeek(out LexToken accessModifier) || !this.IsAccessModifier(accessModifier))
        {
            return false;
        }

        return this.cursor.TryPeek(out LexToken next, lookahead: 1)
            && next.Type is LexTokenType.Keyword
            && (this.cursor.TokenTextEquals(next, "root"u8) || this.cursor.TokenTextEquals(next, "activity"u8));
    }

    private bool IsActivityMethodDeclarationAhead()
        => this.cursor.TryPeek(out LexToken accessModifier)
            && this.IsAccessModifier(accessModifier)
            && this.cursor.TryPeek(out LexToken lifecycle, lookahead: 1)
            && lifecycle.Type is LexTokenType.Keyword
            && (this.cursor.TokenTextEquals(lifecycle, "entry"u8) || this.cursor.TokenTextEquals(lifecycle, "stop"u8));

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
    /// The diagnostic message when tokens are separated.
    /// </param>
    /// <param name="diagnosticReporter">
    /// The reporter receiving diagnostics.
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

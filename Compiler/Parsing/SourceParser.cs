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
    // Placeholder until diagnostic numbering is settled.
    private const string ExpectedSyntaxCode = "SUSE006";
    private const string RequiredSyntacticAdjacencyCode = "SUSE007";

    /// <summary>
    /// Parses supported file-level syntax and identifies the remaining source without attempting recovery across grammar productions not yet implemented.
    /// </summary>
    /// <param name="lexerResult">
    /// The lexer result.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// A source file syntax containing any recognized package declaration and its unparsed remainder.
    /// </returns>
    public ParserResult ParseSourceFile(LexerResult lexerResult, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lexerResult);

        cancellationToken.ThrowIfCancellationRequested();

        ParserState state = new(lexerResult, cancellationToken);

        SourceFileSyntax sourceFile = state.ParseSourceFile();

        return new ParserResult(new ConcreteSyntaxTree(lexerResult, sourceFile), [.. state.Diagnostics]);
    }

    /// <summary>
    /// Parses a package declaration beginning at the first significant lexical element.
    /// </summary>
    /// <param name="lexerResult">
    /// The complete lexical result, including trivia and recovery elements.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel parsing.
    /// </param>
    /// <returns>
    /// The parsed declaration and diagnostics generated during syntactic recovery.
    /// </returns>
    public ParserResult ParsePackageDeclaration(LexerResult lexerResult, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lexerResult);

        cancellationToken.ThrowIfCancellationRequested();

        ParserState state = new(lexerResult, cancellationToken);
        PackageDeclarationSyntax declaration = state.ParsePackageDeclaration();

        return new ParserResult(new ConcreteSyntaxTree(lexerResult, declaration), [.. state.Diagnostics]);
    }

    private sealed class ParserState(LexerResult lexerResult, CancellationToken cancellationToken)
    {
        private readonly LexerResult lexerResult = lexerResult;

        private readonly CancellationToken cancellationToken = cancellationToken;

        private readonly List<SushiDiagnostic> diagnostics = [];

        private int position;

        public IReadOnlyList<SushiDiagnostic> Diagnostics => this.diagnostics;

        /// <summary>
        /// Parses supported file-level syntax and identifies the remaining source without attempting recovery across grammar productions not yet implemented.
        /// </summary>
        /// <returns>
        /// A compilation unit containing any recognized package declarationa nd its unparsed remainder.
        /// </returns>
        public SourceFileSyntax ParseSourceFile()
        {
            this.cancellationToken.ThrowIfCancellationRequested();

            PackageDeclarationSyntax? packageDeclaration = null;

            if (this.TryPeekToken(skipUnknown: false, out LexToken token, out _) && token.Type is LexTokenType.Keyword && this.TokenTextEquals(token, "package"u8))
            {
                packageDeclaration = this.ParsePackageDeclaration();
            }

            SourceSpan? unparsedContentSpan = this.TryPeekToken(skipUnknown: false, out LexToken next, out _)
                ? new SourceSpan(this.lexerResult.Snapshot, next.Span.Start, this.lexerResult.Snapshot.SourceLength)
                : null;

            return new SourceFileSyntax(this.lexerResult.Snapshot, packageDeclaration, unparsedContentSpan);
        }

        public PackageDeclarationSyntax ParsePackageDeclaration()
        {
            this.cancellationToken.ThrowIfCancellationRequested();

            SyntaxToken packageKeyword = this.ParsePackageKeyword();
            QualifiedNameSyntax name = this.ParseQualifiedName();
            SyntaxToken semicolonToken = this.ParseSemicolon();

            return new PackageDeclarationSyntax(packageKeyword, name, semicolonToken);
        }

        private SyntaxToken ParsePackageKeyword()
        {
            if (this.TryPeekToken(skipUnknown: false, out LexToken token, out int index) && token.Type is LexTokenType.Keyword && this.TokenTextEquals(token, "package"u8))
            {
                this.position = index + 1;

                return new SyntaxToken(SyntaxType.PackageKeyword, token);
            }

            int position = this.GetCurrentPosition();

            this.AddExpectedDiagnostic("Expected \"package\" keyword.", position);

            return SyntaxToken.Missing(SyntaxType.PackageKeyword, this.lexerResult.Snapshot, position);
        }

        private QualifiedNameSyntax ParseQualifiedName()
        {
            List<SyntaxToken> segments = [this.ParseNameComponent("Expected package name.", skipUnknown: false)];
            List<SyntaxToken> separators = [];

            while (this.TryConsumePunctuation((byte)'.', SyntaxType.DotToken, skipUnknown: true, out SyntaxToken separator))
            {
                // A source-backed name and dot may be separated only by a diagnosed recovery gap.

                this.RequireSyntacticAdjacency(segments[^1], separator, "\".\" must immediately follow the preceding package-name component.");

                SyntaxToken segment = this.ParseNameComponent("Expected identifier after \".\" in package name.", skipUnknown: true);

                this.RequireSyntacticAdjacency(separator, segment, "A package-name component must immediately follow \".\".");

                separators.Add(separator);
                segments.Add(segment);
            }

            return new QualifiedNameSyntax(segments, separators);
        }

        private SyntaxToken ParseNameComponent(string diagnosticMessage, bool skipUnknown)
        {
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

            this.AddExpectedDiagnostic(diagnosticMessage, position);

            return SyntaxToken.Missing(SyntaxType.IdentifierToken, this.lexerResult.Snapshot, position);
        }

        private SyntaxToken ParseSemicolon()
        {
            if (this.TryConsumePunctuation((byte)';', SyntaxType.SemicolonToken, skipUnknown: false, out SyntaxToken semicolon))
            {
                return semicolon;
            }
            
            int position = this.GetCurrentPosition();

            this.AddExpectedDiagnostic("Expected \";\" after package declaration.", position);

            return SyntaxToken.Missing(SyntaxType.SemicolonToken, this.lexerResult.Snapshot, position);
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
        private void RequireSyntacticAdjacency(SyntaxToken left, SyntaxToken right, string message)
        {
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

            this.diagnostics.Add(new SushiDiagnostic(RequiredSyntacticAdjacencyCode, message, DiagnosticSeverity.Error, new SourceSpan(this.lexerResult.Snapshot, leftSpan.End, rightSpan.Start)));
        }

        private bool TokenTextEquals(LexToken token, ReadOnlySpan<byte> expected) => token.Span.Length == expected.Length && this.lexerResult.Snapshot.Bytes.Span[token.Span.Start..token.Span.End].SequenceEqual(expected);

        private int GetCurrentPosition() => this.TryPeekToken(skipUnknown: false, out LexToken token, out _) ? token.Span.Start : this.lexerResult.Snapshot.SourceLength;

        private void AddExpectedDiagnostic(string message, int position)
        {
            this.diagnostics.Add(
                new SushiDiagnostic(
                    ExpectedSyntaxCode,
                    message,
                    DiagnosticSeverity.Error,
                    new SourceSpan(this.lexerResult.Snapshot, position, position)
                )
            );
        }

        private static bool IsTrivia(LexTokenType type) =>
            type is LexTokenType.Whitespace
            or LexTokenType.LineTerminator
            or LexTokenType.LineComment
            or LexTokenType.DocumentationLineComment
            or LexTokenType.BlockComment;
    }
}
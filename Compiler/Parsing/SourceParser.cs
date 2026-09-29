using Sushi.Diagnostics;
using Sushi.Lexing;
using Sushi.Lexing.Tokenization;
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.Parsing;

/// <summary>
/// Parses Sushi lexical elements into structured concrete syntax.
/// </summary>
public sealed class SourceParser
{
    // Placeholder until diagnostic numbering is settled.
    private const string ExpectedSyntaxCode = "SUSE006";

    /// <summary>
    /// Parses a package declaration beginning at the first significant lexical element.
    /// </summary>
    /// <param name="lexerResult">
    /// The lexical result containing the source elements to parse.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel parsing.
    /// </param>
    /// <returns>
    /// The parsed package-declaration syntax and any parser diagnostics produced during recovery.
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
            if (this.TryGetCurrent(out LexToken token)
                && token.Type is LexTokenType.Keyword
                && this.TokenTextEquals(token, "package"u8))
            {
                return new SyntaxToken(SyntaxType.PackageKeyword, this.ConsumeCurrent());
            }

            int position = this.GetCurrentPosition();

            this.AddExpectedDiagnostic("Expected \"package\" keyword.", position);

            return SyntaxToken.Missing(SyntaxType.PackageKeyword, this.lexerResult.Snapshot, position);
        }

        private QualifiedNameSyntax ParseQualifiedName()
        {
            List<SyntaxToken> segments = [];
            List<SyntaxToken> separators = [];

            segments.Add(this.ParseNameComponent("Expected package name."));

            while (this.CurrentIsPunctuation((byte)'.'))
            {
                separators.Add(new SyntaxToken(SyntaxType.DotToken, this.ConsumeCurrent()));

                segments.Add(this.ParseNameComponent("Expected identifier after \".\" in package name."));
            }

            return new QualifiedNameSyntax(segments, separators);
        }

        private SyntaxToken ParseNameComponent(string diagnosticMessage)
        {
            if (this.TryGetCurrent(out LexToken token))
            {
                SyntaxType type = token.Type switch
                {
                    LexTokenType.Identifier => SyntaxType.IdentifierToken,
                    LexTokenType.EscapedIdentifier => SyntaxType.EscapedIdentifierToken,
                    _ => default
                };

                if (type != default)
                {
                    return new SyntaxToken(type, this.ConsumeCurrent());
                }
            }

            int position = this.GetCurrentPosition();

            this.AddExpectedDiagnostic(diagnosticMessage, position);

            return SyntaxToken.Missing(SyntaxType.IdentifierToken, this.lexerResult.Snapshot, position);
        }

        private SyntaxToken ParseSemicolon()
        {
            if (this.CurrentIsPunctuation((byte)';'))
            {
                return new SyntaxToken(SyntaxType.SemicolonToken, this.ConsumeCurrent());
            }

            int position = this.GetCurrentPosition();

            this.AddExpectedDiagnostic("Expected \";\" after package declaration.", position);

            return SyntaxToken.Missing(SyntaxType.SemicolonToken, this.lexerResult.Snapshot, position);
        }

        private bool TryGetCurrent(out LexToken token)
        {
            this.cancellationToken.ThrowIfCancellationRequested();

            this.MovePastTrivia();

            if (this.position >= this.lexerResult.Tokens.Count)
            {
                token = default;
                return false;
            }

            token = this.lexerResult.Tokens[this.position];

            return true;
        }

        private LexToken ConsumeCurrent()
        {
            if (!this.TryGetCurrent(out LexToken token))
            {
                throw new InvalidOperationException("Cannot consume syntax beyond the end of the lexical stream.");
            }

            this.position++;
            return token;
        }

        private bool CurrentIsPunctuation(byte punctuation)
        {
            if (!this.TryGetCurrent(out LexToken token) || token.Type is not LexTokenType.Punctuation || token.Span.Length != 1)
            {
                return false;
            }

            return this.lexerResult.Snapshot.Bytes.Span[token.Span.Start] == punctuation;
        }

        private bool TokenTextEquals(LexToken token, ReadOnlySpan<byte> expected)
        {
            if (token.Span.Length != expected.Length)
            {
                return false;
            }

            return this.lexerResult.Snapshot.Bytes.Span[token.Span.Start..token.Span.End].SequenceEqual(expected);
        }

        private int GetCurrentPosition() => this.TryGetCurrent(out LexToken token) ? token.Span.Start : this.lexerResult.Snapshot.SourceLength;

        private void MovePastTrivia()
        {
            while (this.position < this.lexerResult.Tokens.Count && IsTrivia(this.lexerResult.Tokens[this.position].Type))
            {
                this.cancellationToken.ThrowIfCancellationRequested();
                this.position++;
            }
        }

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
using Sushi.Lexing.Tokenization;
using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents a lexical token after the parser has assigned it a grammatical role.
/// </summary>
public readonly record struct SyntaxToken
{
    private readonly SourceSpan syntheticSpan;

    /// <summary>
    /// Gets the grammatical role assigned to this token.
    /// </summary>
    public SyntaxType Type { get; }

    /// <summary>
    /// Gets the source-backed lexical token, or null when this token was synthesized for parser recovery.
    /// </summary>
    public LexToken? SourceToken { get; }

    /// <summary>
    /// Gets whether this token was synthesized because required syntax was absent from the source.
    /// </summary>
    public bool IsMissing { get; }

    /// <summary>
    /// Gets whether this token corresponds to material physically present in the source.
    /// </summary>
    public bool IsSourceBacked => this.SourceToken.HasValue;

    /// <summary>
    /// Gets the source span of the token.
    /// </summary>
    public SourceSpan Span => this.SourceToken?.Span ?? this.syntheticSpan;

    /// <summary>
    /// Creates a syntax token backed by an existing lexical token.
    /// </summary>
    /// <param name="type">
    /// The grammatical role assigned to the lexical token.
    /// </param>
    /// <param name="sourceToken">
    /// The source-backed lexical token represented by this syntax token.
    /// </param>
    public SyntaxToken(SyntaxType type, LexToken sourceToken)
    {
        this.Type = type;
        this.SourceToken = sourceToken;
        this.IsMissing = false;
        this.syntheticSpan = default;
    }

    private SyntaxToken(SyntaxType type, SourceSpan syntheticSpan)
    {
        this.Type = type;
        this.SourceToken = null;
        this.IsMissing = true;
        this.syntheticSpan = syntheticSpan;
    }

    /// <summary>
    /// Creates a zero-length missing syntax token at the specified source position.
    /// </summary>
    /// <param name="type">
    /// The grammatical type of the missing token.
    /// </param>
    /// <param name="snapshot">
    /// The source snapshot in which the token is missing.
    /// </param>
    /// <param name="position">
    /// The canonical byte position at which the token was expected.
    /// </param>
    /// <returns>
    /// A synthetic missing syntax token located at the requested insertion point.
    /// </returns>
    public static SyntaxToken Missing(SyntaxType type, SourceSnapshot snapshot, int position)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (position < 0 || position > snapshot.SourceLength)
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        return new SyntaxToken(type, new SourceSpan(snapshot, position, position));
    }
}
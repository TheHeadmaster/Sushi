using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents a type-first function parameter, including tokens synthesized during error recovery.
/// </summary>
public sealed class ParameterSyntax : SyntaxNode
{
    /// <inheritdoc />
    public override SyntaxType Type => SyntaxType.Parameter;

    /// <inheritdoc />
    public override SourceSpan Span => new(this.TypeToken.Span.Snapshot, this.TypeToken.Span.Start, this.NameToken.Span.End);

    /// <summary>
    /// Gets the declared type's source token.
    /// </summary>
    public SyntaxToken TypeToken { get; }

    /// <summary>
    /// Gets the parameter's source identifier, which may be missing.
    /// </summary>
    public SyntaxToken NameToken { get; }

    /// <summary>
    /// Creates a type-first parameter from source or recovered syntax.
    /// </summary>
    /// <param name="typeToken">
    /// The declared type.
    /// </param>
    /// <param name="nameToken">
    /// The declared parameter name.
    /// </param>
    public ParameterSyntax(SyntaxToken typeToken, SyntaxToken nameToken)
    {
        if (typeToken.Type is not SyntaxType.IdentifierToken and not SyntaxType.EscapedIdentifierToken)
        {
            throw new ArgumentException("A parameter requires a type identifier.", nameof(typeToken));
        }

        if (nameToken.Type is not SyntaxType.IdentifierToken and not SyntaxType.EscapedIdentifierToken)
        {
            throw new ArgumentException("A parameter requires a name identifier.", nameof(nameToken));
        }

        if (!ReferenceEquals(typeToken.Span.Snapshot, nameToken.Span.Snapshot) || typeToken.Span.End > nameToken.Span.Start)
        {
            throw new ArgumentException("Parameter syntax must share one source snapshot and occur in source order.");
        }

        this.TypeToken = typeToken;
        this.NameToken = nameToken;
    }
}
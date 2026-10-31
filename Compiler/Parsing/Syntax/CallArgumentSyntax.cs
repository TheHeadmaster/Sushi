using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents one explicitly supplied invocation argument, optionally identified by a source-level parameter label.
/// </summary>
public sealed class CallArgumentSyntax : SyntaxNode
{
    /// <inheritdoc />
    public override SyntaxType Type => SyntaxType.CallArgument;

    /// <inheritdoc />
    public override SourceSpan Span => new(this.Expression.Span.Snapshot, this.Name?.Span.Start ?? this.Expression.Span.Start, this.Expression.Span.End);

    /// <summary>
    /// Gets the optional caller-facing parameter name.
    /// </summary>
    public SyntaxToken? Name { get; }

    /// <summary>
    /// Gets the separator following a named-argument label.
    /// </summary>
    public SyntaxToken? ColonToken { get; }

    /// <summary>
    /// Gets the expression supplied at this argument position.
    /// </summary>
    public ExpressionSyntax Expression { get; }

    /// <summary>
    /// Gets whether the argument uses named binding rather than positional binding.
    /// </summary>
    public bool IsNamed => this.Name is not null;

    /// <summary>
    /// Creates an argument while retaining its optional source-level label and expression.
    /// </summary>
    /// <param name="name">
    /// The caller-facing name when supplied.
    /// </param>
    /// <param name="colonToken">
    /// The colon associated with a named argument.
    /// </param>
    /// <param name="expression">
    /// The argument expression, including any recovered syntax.
    /// </param>
    public CallArgumentSyntax(SyntaxToken? name, SyntaxToken? colonToken, ExpressionSyntax expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        if ((name is null) != (colonToken is null))
        {
            throw new ArgumentException("A named argument requires both a name and a colon.");
        }

        if (name is SyntaxToken nameToken && nameToken.Type is not (SyntaxType.IdentifierToken or SyntaxType.EscapedIdentifierToken))
        {
            throw new ArgumentException("An argument name must use identifier syntax.", nameof(name));
        }

        if (colonToken is SyntaxToken colon && colon.Type is not SyntaxType.ColonToken)
        {
            throw new ArgumentException("A named argument requires a colon token.", nameof(colonToken));
        }

        SourceSnapshot snapshot = expression.Span.Snapshot;

        if (name is SyntaxToken label && colonToken is SyntaxToken separator)
        {
            if (!ReferenceEquals(label.Span.Snapshot, snapshot)
                || !ReferenceEquals(separator.Span.Snapshot, snapshot)
                || separator.Span.Start < label.Span.End
                || expression.Span.Start < separator.Span.End)
            {
                throw new ArgumentException("Named-argument syntax must share a snapshot and occur in source order.");
            }
        }

        this.Name = name;
        this.ColonToken = colonToken;
        this.Expression = expression;
    }
}

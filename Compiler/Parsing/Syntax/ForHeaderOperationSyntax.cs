using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents an independently parsed for-header declaration, assignment,
/// or integer increment/decrement operation. Header commas separate these
/// operations without introducing a comma expression.
/// </summary>
public sealed class ForHeaderOperationSyntax
{
    /// <summary>
    /// Gets the explicit declared type, when this operation initializes
    /// a new local rather than updating an existing one.
    /// </summary>
    public SyntaxToken? TypeToken { get; }

    /// <summary>
    /// Gets the declared or updated local's identifier.
    /// </summary>
    public SyntaxToken NameToken { get; }

    /// <summary>
    /// Gets the initializer, assignment, increment, or decrement operator.
    /// </summary>
    public SyntaxToken OperatorToken { get; }

    /// <summary>
    /// Gets the initializer or assignment expression, when applicable.
    /// Increment and decrement do not have a separate value operand.
    /// </summary>
    public ExpressionSyntax? Value { get; }

    /// <summary>
    /// Gets whether an increment or decrement operator precedes the target.
    /// Prefix and postfix forms are equivalent when used solely as updates.
    /// </summary>
    public bool IsPrefix { get; }

    /// <summary>
    /// Gets whether this operation declares a new local.
    /// </summary>
    public bool IsDeclaration => this.TypeToken is not null;

    /// <summary>
    /// Gets whether this operation is an increment or decrement.
    /// </summary>
    public bool IsIncrementOrDecrement => this.Value is null;

    /// <summary>
    /// Gets the complete source span of the header operation.
    /// </summary>
    public SourceSpan Span
    {
        get
        {
            SourceSpan first = this.TypeToken?.Span ?? (this.IsPrefix ? this.OperatorToken.Span : this.NameToken.Span);
            SourceSpan last = this.Value?.Span ?? (this.IsPrefix ? this.NameToken.Span : this.OperatorToken.Span);
            return new SourceSpan(first.Snapshot, first.Start, last.End);
        }
    }

    /// <summary>
    /// Creates an operation with its source-backed components in lexical order.
    /// </summary>
    /// <param name="typeToken">
    /// The optional explicit type for a new local declaration.
    /// </param>
    /// <param name="nameToken">
    /// The declaration or assignment target.
    /// </param>
    /// <param name="operatorToken">
    /// The source-backed operator token.
    /// </param>
    /// <param name="value">
    /// An initializer or assignment value, or null for increment/decrement.
    /// </param>
    /// <param name="isPrefix">
    /// Whether an increment/decrement operator appears before its target.
    /// </param>
    public ForHeaderOperationSyntax(SyntaxToken? typeToken, SyntaxToken nameToken, SyntaxToken operatorToken, ExpressionSyntax? value, bool isPrefix = false)
    {
        if (nameToken.Type is not (SyntaxType.IdentifierToken or SyntaxType.EscapedIdentifierToken)
            || operatorToken.Type is not SyntaxType.OperatorToken
            || typeToken is SyntaxToken declaredType && declaredType.Type is not (SyntaxType.IdentifierToken or SyntaxType.EscapedIdentifierToken)
            || typeToken is not null && (isPrefix || value is null)
            || isPrefix && value is not null)
        {
            throw new ArgumentException("Invalid for-header operation shape.");
        }

        List<SourceSpan> spans = [];

        if (typeToken is SyntaxToken type)
        {
            spans.Add(type.Span);
        }

        if (isPrefix)
        {
            spans.Add(operatorToken.Span);
        }

        spans.Add(nameToken.Span);

        if (!isPrefix)
        {
            spans.Add(operatorToken.Span);
        }

        if (value is not null)
        {
            spans.Add(value.Span);
        }

        for (int i = 1; i < spans.Count; i++)
        {
            if (!ReferenceEquals(spans[i].Snapshot, spans[0].Snapshot) || spans[i].Start < spans[i - 1].End)
            {
                throw new ArgumentException("For-header operation tokens must share a snapshot and occur in source order.");
            }
        }

        this.TypeToken = typeToken;
        this.NameToken = nameToken;
        this.OperatorToken = operatorToken;
        this.Value = value;
        this.IsPrefix = isPrefix;
    }
}
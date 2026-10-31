using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents invocation-expression syntax without selecting a callable or determining its return contract.
/// </summary>
public sealed class InvocationExpressionSyntax : ExpressionSyntax
{
    /// <inheritdoc />
    public override SyntaxType Type => SyntaxType.InvocationExpression;

    /// <inheritdoc />
    public override SourceSpan Span => new(this.Target.Span.Snapshot, this.Target.Span.Start, this.CloseParenthesisToken.Span.End);

    /// <summary>
    /// Gets the syntactic invocation target.
    /// </summary>
    public ExpressionSyntax Target { get; }

    /// <summary>
    /// Gets the opening argument-list parenthesis.
    /// </summary>
    public SyntaxToken OpenParenthesisToken { get; }

    /// <summary>
    /// Gets explicitly supplied arguments in source evaluation order.
    /// </summary>
    public IReadOnlyList<CallArgumentSyntax> Arguments { get; }

    /// <summary>
    /// Gets the commas separating adjacent arguments.
    /// </summary>
    public IReadOnlyList<SyntaxToken> ArgumentSeparators { get; }

    /// <summary>
    /// Gets the closing argument-list parenthesis.
    /// </summary>
    public SyntaxToken CloseParenthesisToken { get; }

    /// <summary>
    /// Creates a syntactic invocation with its unbound target, ordered arguments, and punctuation.
    /// </summary>
    /// <param name="target">
    /// The expression identifying the callable.
    /// </param>
    /// <param name="openParenthesisToken">
    /// The opening parenthesis.
    /// </param>
    /// <param name="arguments">
    /// The arguments in source order.
    /// </param>
    /// <param name="argumentSeparators">
    /// The commas separating consecutive arguments.
    /// </param>
    /// <param name="closeParenthesisToken">
    /// The closing parenthesis, which may be recovered.
    /// </param>
    public InvocationExpressionSyntax(
        ExpressionSyntax target,
        SyntaxToken openParenthesisToken,
        IReadOnlyList<CallArgumentSyntax> arguments,
        IReadOnlyList<SyntaxToken> argumentSeparators,
        SyntaxToken closeParenthesisToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(argumentSeparators);

        if (openParenthesisToken.Type is not SyntaxType.OpenParenthesisToken || closeParenthesisToken.Type is not SyntaxType.CloseParenthesisToken)
        {
            throw new ArgumentException("An invocation requires argument-list parentheses.");
        }

        if (argumentSeparators.Count != Math.Max(0, arguments.Count - 1))
        {
            throw new ArgumentException("Argument separators must match the argument count.", nameof(argumentSeparators));
        }

        SourceSnapshot snapshot = target.Span.Snapshot;

        if (!ReferenceEquals(openParenthesisToken.Span.Snapshot, snapshot)
            || !ReferenceEquals(closeParenthesisToken.Span.Snapshot, snapshot)
            || openParenthesisToken.Span.Start < target.Span.End)
        {
            throw new ArgumentException("Invocation target and parentheses must be ordered in one source snapshot.");
        }

        int previousEnd = openParenthesisToken.Span.End;

        for (int i = 0; i < arguments.Count; i++)
        {
            CallArgumentSyntax argument = arguments[i] ?? throw new ArgumentException("Invocation arguments cannot be null.", nameof(arguments));

            if (!ReferenceEquals(argument.Span.Snapshot, snapshot) || argument.Span.Start < previousEnd)
            {
                throw new ArgumentException("Invocation arguments must occur in source order.", nameof(arguments));
            }

            previousEnd = argument.Span.End;

            if (i < argumentSeparators.Count)
            {
                SyntaxToken separator = argumentSeparators[i];

                if (separator.Type is not SyntaxType.CommaToken
                    || !ReferenceEquals(separator.Span.Snapshot, snapshot)
                    || separator.Span.Start < previousEnd)
                {
                    throw new ArgumentException("Invocation argument separators must occur in source order.", nameof(argumentSeparators));
                }

                previousEnd = separator.Span.End;
            }
        }

        if (closeParenthesisToken.Span.Start < previousEnd)
        {
            throw new ArgumentException("The invocation must close after its arguments.", nameof(closeParenthesisToken));
        }

        this.Target = target;
        this.OpenParenthesisToken = openParenthesisToken;
        this.Arguments = [.. arguments];
        this.ArgumentSeparators = [.. argumentSeparators];
        this.CloseParenthesisToken = closeParenthesisToken;
    }
}

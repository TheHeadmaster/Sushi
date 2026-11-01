using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents a traditional three-part for loop. Initialization and
/// iteration operations are separate ordered sequences, and the condition
/// is optional, defaulting semantically to true.
/// </summary>
public sealed class ForStatementSyntax : StatementSyntax
{
    /// <inheritdoc />
    public override SyntaxType Type => SyntaxType.ForStatement;

    /// <inheritdoc />
    public override SourceSpan Span => new(this.ForKeyword.Span.Snapshot, this.ForKeyword.Span.Start, this.Body.Span.End);

    /// <summary>
    /// Gets the opening loop keyword.
    /// </summary>
    public SyntaxToken ForKeyword { get; }

    /// <summary>
    /// Gets the opening parenthesis.
    /// </summary>
    public SyntaxToken OpenParenthesisToken { get; }

    /// <summary>
    /// Gets the operations executed once before the first condition test.
    /// </summary>
    public IReadOnlyList<ForHeaderOperationSyntax> Initialization { get; }

    /// <summary>
    /// Gets the semicolon ending initialization.
    /// </summary>
    public SyntaxToken InitializationSemicolon { get; }

    /// <summary>
    /// Gets the optional Boolean condition.
    /// </summary>
    public ExpressionSyntax? Condition { get; }

    /// <summary>
    /// Gets the semicolon ending the condition clause.
    /// </summary>
    public SyntaxToken ConditionSemicolon { get; }

    /// <summary>
    /// Gets the operations executed after each body iteration, including
    /// an iteration continued using a matching continue statement.
    /// </summary>
    public IReadOnlyList<ForHeaderOperationSyntax> Iteration { get; }

    /// <summary>
    /// Gets the closing parenthesis.
    /// </summary>
    public SyntaxToken CloseParenthesisToken { get; }

    /// <summary>
    /// Gets the required braced body.
    /// </summary>
    public BlockSyntax Body { get; }

    /// <summary>
    /// Creates an ordered for statement while retaining its header boundaries.
    /// </summary>
    /// <param name="forKeyword">
    /// The opening for keyword.
    /// </param>
    /// <param name="openParenthesisToken">
    /// The opening parenthesis.
    /// </param>
    /// <param name="initialization">
    /// The initialization operations.
    /// </param>
    /// <param name="initializationSemicolon">
    /// The first header separator.
    /// </param>
    /// <param name="condition">
    /// The optional Boolean condition.
    /// </param>
    /// <param name="conditionSemicolon">
    /// The second header separator.
    /// </param>
    /// <param name="iteration">
    /// The iteration operations.
    /// </param>
    /// <param name="closeParenthesisToken">
    /// The closing parenthesis.
    /// </param>
    /// <param name="body">
    /// The required braced body.
    /// </param>
    public ForStatementSyntax(
        SyntaxToken forKeyword,
        SyntaxToken openParenthesisToken,
        IReadOnlyList<ForHeaderOperationSyntax> initialization,
        SyntaxToken initializationSemicolon,
        ExpressionSyntax? condition,
        SyntaxToken conditionSemicolon,
        IReadOnlyList<ForHeaderOperationSyntax> iteration,
        SyntaxToken closeParenthesisToken,
        BlockSyntax body)
    {
        ArgumentNullException.ThrowIfNull(initialization);
        ArgumentNullException.ThrowIfNull(iteration);
        ArgumentNullException.ThrowIfNull(body);

        if (forKeyword.Type is not SyntaxType.ForKeyword
            || openParenthesisToken.Type is not SyntaxType.OpenParenthesisToken
            || initializationSemicolon.Type is not SyntaxType.SemicolonToken
            || conditionSemicolon.Type is not SyntaxType.SemicolonToken
            || closeParenthesisToken.Type is not SyntaxType.CloseParenthesisToken)
        {
            throw new ArgumentException("A for statement requires its keyword, parentheses, and two semicolons.");
        }

        SourceSnapshot snapshot = forKeyword.Span.Snapshot;
        SourceSpan[] boundaries =
        [
            forKeyword.Span,
            openParenthesisToken.Span,
            initializationSemicolon.Span,
            conditionSemicolon.Span,
            closeParenthesisToken.Span,
            body.Span
        ];

        for (int i = 1; i < boundaries.Length; i++)
        {
            if (!ReferenceEquals(boundaries[i].Snapshot, snapshot) || boundaries[i].Start < boundaries[i - 1].End)
            {
                throw new ArgumentException("For statement boundaries must share a snapshot and occur in source order.");
            }
        }

        ValidateOperations(initialization, snapshot, openParenthesisToken.Span.End, initializationSemicolon.Span.Start);
        ValidateOperations(iteration, snapshot, conditionSemicolon.Span.End, closeParenthesisToken.Span.Start);

        if (condition is not null
            && (!ReferenceEquals(condition.Span.Snapshot, snapshot)
                || condition.Span.Start < initializationSemicolon.Span.End
                || condition.Span.End > conditionSemicolon.Span.Start))
        {
            throw new ArgumentException("The for condition must occur between its two semicolons.");
        }

        this.ForKeyword = forKeyword;
        this.OpenParenthesisToken = openParenthesisToken;
        this.Initialization = [.. initialization];
        this.InitializationSemicolon = initializationSemicolon;
        this.Condition = condition;
        this.ConditionSemicolon = conditionSemicolon;
        this.Iteration = [.. iteration];
        this.CloseParenthesisToken = closeParenthesisToken;
        this.Body = body;
    }

    private static void ValidateOperations(IReadOnlyList<ForHeaderOperationSyntax> operations, SourceSnapshot snapshot, int start, int end)
    {
        int previousEnd = start;

        foreach (ForHeaderOperationSyntax operation in operations)
        {
            ArgumentNullException.ThrowIfNull(operation);

            if (!ReferenceEquals(operation.Span.Snapshot, snapshot)
                || operation.Span.Start < previousEnd
                || operation.Span.End > end)
            {
                throw new ArgumentException("For-header operations must remain in their clause and occur in source order.");
            }

            previousEnd = operation.Span.End;
        }
    }
}
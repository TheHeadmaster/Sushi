
using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents a lifecycle method declared inside an activity.
/// </summary>
public sealed class ActivityMethodDeclarationSyntax : ActivityMemberSyntax
{
    /// <inheritdoc />
    public override SyntaxType Type => SyntaxType.ActivityMethodDeclaration;

    /// <inheritdoc />
    public override SourceSpan Span => new(this.AccessModifier.Span.Snapshot, this.AccessModifier.Span.Start, this.Body.Span.End);

    /// <summary>
    /// Gets the method's explicit accessibility modifier.
    /// </summary>
    public SyntaxToken AccessModifier { get; }

    /// <summary>
    /// Gets the entry or stop modifier that determines the method's lifecycle role.
    /// </summary>
    public SyntaxToken LifecycleModifier { get; }

    /// <summary>
    /// Gets the declared return type.
    /// </summary>
    public SyntaxToken ReturnType { get; }

    /// <summary>
    /// Gets the method's declared name.
    /// </summary>
    public SyntaxToken Name { get; }

    /// <summary>
    /// Gets the opening parenthesis.
    /// </summary>
    public SyntaxToken OpenParenthesisToken { get; }

    /// <summary>
    /// Gets the ordered parameter declarations.
    /// </summary>
    public IReadOnlyList<ParameterSyntax> Parameters { get; }

    /// <summary>
    /// Gets the commas separating consecutive parameters.
    /// </summary>
    public IReadOnlyList<SyntaxToken> ParameterSeparators { get; }

    /// <summary>
    /// Gets the closing parenthesis.
    /// </summary>
    public SyntaxToken CloseParenthesisToken { get; }

    /// <summary>
    /// Gets the method's parsed body.
    /// </summary>
    public BlockSyntax Body { get; }

    /// <summary>
    /// Creates a lifecycle method with its complete recovered or source-backed signature.
    /// </summary>
    /// <param name="accessModifier">
    /// The access modifier preceding the lifecycle designation.
    /// </param>
    /// <param name="lifecycleModifier">
    /// The entry or stop modifier.
    /// </param>
    /// <param name="returnType">
    /// The method's source-level return type.
    /// </param>
    /// <param name="name">
    /// The declared method identifier.
    /// </param>
    /// <param name="openParenthesisToken">
    /// The opening parenthesis.
    /// </param>
    /// <param name="parameters">
    /// The parameters in source order.
    /// </param>
    /// <param name="parameterSeparators">
    /// The commas between parameters.
    /// </param>
    /// <param name="closeParenthesisToken">
    /// The closing parenthesis.
    /// </param>
    /// <param name="body">
    /// The braced method body.
    /// </param>
    public ActivityMethodDeclarationSyntax(
        SyntaxToken accessModifier,
        SyntaxToken lifecycleModifier,
        SyntaxToken returnType,
        SyntaxToken name,
        SyntaxToken openParenthesisToken,
        IReadOnlyList<ParameterSyntax> parameters,
        IReadOnlyList<SyntaxToken> parameterSeparators,
        SyntaxToken closeParenthesisToken,
        BlockSyntax body)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(parameterSeparators);
        ArgumentNullException.ThrowIfNull(body);

        if (accessModifier.Type is not SyntaxType.AccessModifierKeyword)
        {
            throw new ArgumentException("Activity methods require an access modifier.", nameof(accessModifier));
        }

        if (lifecycleModifier.Type is not (SyntaxType.EntryKeyword or SyntaxType.StopKeyword))
        {
            throw new ArgumentException("Activity methods require an entry or stop modifier.", nameof(lifecycleModifier));
        }

        if (returnType.Type is not (SyntaxType.IdentifierToken or SyntaxType.EscapedIdentifierToken or SyntaxType.VoidKeyword))
        {
            throw new ArgumentException("Activity methods require return-type syntax.", nameof(returnType));
        }

        if (name.Type is not (SyntaxType.IdentifierToken or SyntaxType.EscapedIdentifierToken))
        {
            throw new ArgumentException("Activity methods require an identifier.", nameof(name));
        }

        if (openParenthesisToken.Type is not SyntaxType.OpenParenthesisToken || closeParenthesisToken.Type is not SyntaxType.CloseParenthesisToken)
        {
            throw new ArgumentException("Activity methods require parenthesis tokens.");
        }

        if (parameterSeparators.Count != Math.Max(0, parameters.Count - 1))
        {
            throw new ArgumentException("Activity method parameter separators must match the parameter count.", nameof(parameterSeparators));
        }

        SourceSnapshot snapshot = accessModifier.Span.Snapshot;
        SourceSpan[] spans =
        [
            lifecycleModifier.Span,
            returnType.Span,
            name.Span,
            openParenthesisToken.Span,
            closeParenthesisToken.Span,
            body.Span
        ];

        if (spans.Any(span => !ReferenceEquals(span.Snapshot, snapshot)))
        {
            throw new ArgumentException("All activity method syntax must belong to the same source snapshot.");
        }

        int previousEnd = accessModifier.Span.End;

        foreach (SourceSpan span in spans[..4])
        {
            if (span.Start < previousEnd)
            {
                throw new ArgumentException("Activity method signature tokens must occur in source order.");
            }

            previousEnd = span.End;
        }

        for (int i = 0; i < parameters.Count; i++)
        {
            ParameterSyntax parameter = parameters[i] ?? throw new ArgumentException("Activity method parameters cannot be null.", nameof(parameters));

            if (!ReferenceEquals(parameter.Span.Snapshot, snapshot) || parameter.Span.Start < previousEnd)
            {
                throw new ArgumentException("Activity method parameters must occur in source order.", nameof(parameters));
            }

            previousEnd = parameter.Span.End;

            if (i < parameterSeparators.Count)
            {
                SyntaxToken separator = parameterSeparators[i];

                if (separator.Type is not SyntaxType.CommaToken || !ReferenceEquals(separator.Span.Snapshot, snapshot) || separator.Span.Start < previousEnd)
                {
                    throw new ArgumentException("Activity method parameter separators must occur in source order.", nameof(parameterSeparators));
                }

                previousEnd = separator.Span.End;
            }
        }

        if (closeParenthesisToken.Span.Start < previousEnd || body.Span.Start < closeParenthesisToken.Span.End)
        {
            throw new ArgumentException("Activity method parameters must precede the method body.");
        }

        this.AccessModifier = accessModifier;
        this.LifecycleModifier = lifecycleModifier;
        this.ReturnType = returnType;
        this.Name = name;
        this.OpenParenthesisToken = openParenthesisToken;
        this.Parameters = [.. parameters];
        this.ParameterSeparators = [.. parameterSeparators];
        this.CloseParenthesisToken = closeParenthesisToken;
        this.Body = body;
    }
}

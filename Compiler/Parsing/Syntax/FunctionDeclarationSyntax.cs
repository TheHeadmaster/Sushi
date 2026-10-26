using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents a callable declaration parsed at package scope.
/// </summary>
public sealed class FunctionDeclarationSyntax : SyntaxNode
{
    /// <inheritdoc />
    public override SyntaxType Type => SyntaxType.FunctionDeclaration;

    /// <inheritdoc />
    public override SourceSpan Span => new(this.AccessModifier.Span.Snapshot, this.AccessModifier.Span.Start, this.Body.Span.End);

    /// <summary>
    /// Gets the explicit access modifier.
    /// </summary>
    public SyntaxToken AccessModifier { get; }

    /// <summary>
    /// Gets the source-level return type.
    /// </summary>
    public SyntaxToken ReturnType { get; }

    /// <summary>
    /// Gets the declared function name.
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
    /// Gets comma separators between parameter declarations.
    /// </summary>
    public IReadOnlyList<SyntaxToken> ParameterSeparators { get; }

    /// <summary>
    /// Gets the closing parenthesis.
    /// </summary>
    public SyntaxToken CloseParenthesisToken { get; }

    /// <summary>
    /// Gets the parsed function body.
    /// </summary>
    public BlockSyntax Body { get; }

    /// <summary>
    /// Creates a parameterless function declaration for existing consumers.
    /// </summary>
    /// <param name="accessModifier">
    /// The explicit access modifier.
    /// </param>
    /// <param name="returnType">
    /// The return type.
    /// </param>
    /// <param name="name">
    /// The declared function name.
    /// </param>
    /// <param name="openParenthesisToken">
    /// The opening parenthesis.
    /// </param>
    /// <param name="closeParenthesisToken">
    /// The closing parenthesis.
    /// </param>
    /// <param name="body">
    /// The function body.
    /// </param>
    public FunctionDeclarationSyntax(SyntaxToken accessModifier, SyntaxToken returnType, SyntaxToken name, SyntaxToken openParenthesisToken, SyntaxToken closeParenthesisToken, BlockSyntax body)
        : this(accessModifier, returnType, name, openParenthesisToken, [], [], closeParenthesisToken, body)
    {
    }

    /// <summary>
    /// Creates a function declaration preserving the type-first parameter syntax.
    /// </summary>
    /// <param name="accessModifier">
    /// The explicit access modifier.
    /// </param>
    /// <param name="returnType">
    /// The return type.
    /// </param>
    /// <param name="name">
    /// The declared function name.
    /// </param>
    /// <param name="openParenthesisToken">
    /// The opening parenthesis.
    /// </param>
    /// <param name="parameters">
    /// The parameter declarations in source order.
    /// </param>
    /// <param name="parameterSeparators">
    /// Commas between the parameter declarations.
    /// </param>
    /// <param name="closeParenthesisToken">
    /// The closing parenthesis.
    /// </param>
    /// <param name="body">
    /// The function body.
    /// </param>
    public FunctionDeclarationSyntax(SyntaxToken accessModifier, SyntaxToken returnType, SyntaxToken name, SyntaxToken openParenthesisToken, IReadOnlyList<ParameterSyntax> parameters, IReadOnlyList<SyntaxToken> parameterSeparators, SyntaxToken closeParenthesisToken, BlockSyntax body)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(parameterSeparators);
        ArgumentNullException.ThrowIfNull(body);

        if (accessModifier.Type is not SyntaxType.AccessModifierKeyword)
        {
            throw new ArgumentException("Function declarations require an access modifier.", nameof(accessModifier));
        }

        if (returnType.Type is not SyntaxType.IdentifierToken and not SyntaxType.EscapedIdentifierToken and not SyntaxType.VoidKeyword)
        {
            throw new ArgumentException("Function declarations require supported return-type syntax.", nameof(returnType));
        }

        if (name.Type is not SyntaxType.IdentifierToken and not SyntaxType.EscapedIdentifierToken)
        {
            throw new ArgumentException("Function declarations require an identifier name.", nameof(name));
        }

        if (openParenthesisToken.Type is not SyntaxType.OpenParenthesisToken || closeParenthesisToken.Type is not SyntaxType.CloseParenthesisToken)
        {
            throw new ArgumentException("Function declarations require parenthesis tokens.");
        }

        if (parameterSeparators.Count != Math.Max(0, parameters.Count - 1))
        {
            throw new ArgumentException("A parameter list requires one comma between consecutive parameters.", nameof(parameterSeparators));
        }

        SourceSnapshot snapshot = accessModifier.Span.Snapshot;
        SourceSpan[] headerSpans = [returnType.Span, name.Span, openParenthesisToken.Span, closeParenthesisToken.Span, body.Span];

        if (headerSpans.Any(span => !ReferenceEquals(span.Snapshot, snapshot)))
        {
            throw new ArgumentException("All function-declaration syntax must originate from the same source snapshot.");
        }

        int end = openParenthesisToken.Span.End;

        for (int i = 0; i < parameters.Count; i++)
        {
            ParameterSyntax parameter = parameters[i] ?? throw new ArgumentException("Parameter declarations cannot be null.", nameof(parameters));

            if (!ReferenceEquals(parameter.Span.Snapshot, snapshot) || parameter.Span.Start < end)
            {
                throw new ArgumentException("Parameters must be in source order within the function's source snapshot.", nameof(parameters));
            }

            end = parameter.Span.End;

            if (i < parameterSeparators.Count)
            {
                SyntaxToken separator = parameterSeparators[i];

                if (separator.Type is not SyntaxType.CommaToken || !ReferenceEquals(separator.Span.Snapshot, snapshot) || separator.Span.Start < end)
                {
                    throw new ArgumentException("Parameter separators must be ordered source-backed or recovered commas.", nameof(parameterSeparators));
                }

                end = separator.Span.End;
            }
        }

        if (closeParenthesisToken.Span.Start < end || body.Span.Start < closeParenthesisToken.Span.End)
        {
            throw new ArgumentException("Function parameter syntax must precede the body in source order.");
        }

        this.AccessModifier = accessModifier;
        this.ReturnType = returnType;
        this.Name = name;
        this.OpenParenthesisToken = openParenthesisToken;
        this.Parameters = [.. parameters];
        this.ParameterSeparators = [.. parameterSeparators];
        this.CloseParenthesisToken = closeParenthesisToken;
        this.Body = body;
    }
}
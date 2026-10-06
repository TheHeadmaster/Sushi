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
    /// Gets the return-type syntax currently supported by the parser.
    /// </summary>
    public SyntaxToken ReturnType { get; }

    /// <summary>
    /// Gets the declared function name.
    /// </summary>
    public SyntaxToken Name { get; }

    /// <summary>
    /// Gets the opening parenthesis of the parameter list.
    /// </summary>
    public SyntaxToken OpenParenthesisToken { get; }

    /// <summary>
    /// Gets the function body.
    /// </summary>
    public BlockSyntax Body { get; }

    /// <summary>
    /// Gets the closing parenthesis of the parameter list.
    /// </summary>
    public SyntaxToken CloseParenthesisToken { get; }

    /// <summary>
    /// Creates a function declaration.
    /// </summary>
    /// <param name="accessModifier">
    /// The explicit access modifier.
    /// </param>
    /// <param name="returnType">
    /// The return-type syntax currently supported by the parser.
    /// </param>
    /// <param name="name">
    /// The declared function name.
    /// </param>
    /// <param name="openParenthesisToken">
    /// The opening parenthesis of the parameter list.
    /// </param>
    /// <param name="closeParenthesisToken">
    /// The closing parenthesis of the parameter list.
    /// </param>
    /// <param name="body">
    /// The function body.
    /// </param>
    public FunctionDeclarationSyntax(SyntaxToken accessModifier, SyntaxToken returnType, SyntaxToken name, SyntaxToken openParenthesisToken, SyntaxToken closeParenthesisToken, BlockSyntax body)
    {
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

        if (openParenthesisToken.Type is not SyntaxType.OpenParenthesisToken)
        {
            throw new ArgumentException("Function declarations require an opening parenthesis token.", nameof(openParenthesisToken));
        }

        if (closeParenthesisToken.Type is not SyntaxType.CloseParenthesisToken)
        {
            throw new ArgumentException("Function declarations require a closing parenthesis token.", nameof(closeParenthesisToken));
        }

        SourceSnapshot snapshot = accessModifier.Span.Snapshot;

        if (!ReferenceEquals(returnType.Span.Snapshot, snapshot)
            || !ReferenceEquals(name.Span.Snapshot, snapshot)
            || !ReferenceEquals(openParenthesisToken.Span.Snapshot, snapshot)
            || !ReferenceEquals(closeParenthesisToken.Span.Snapshot, snapshot)
            || !ReferenceEquals(body.Span.Snapshot, snapshot))
        {
            throw new ArgumentException("All function-declaration syntax must originate from the same source snapshot.");
        }

        this.AccessModifier = accessModifier;
        this.ReturnType = returnType;
        this.Name = name;
        this.OpenParenthesisToken = openParenthesisToken;
        this.CloseParenthesisToken = closeParenthesisToken;
        this.Body = body;
    }
}
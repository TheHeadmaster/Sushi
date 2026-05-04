using System.Diagnostics.CodeAnalysis;
using Sushi.Diagnostics;
using Sushi.Diagnostics.Errors;
using Sushi.Parsing.Scope;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes.Expressions.Core;

/// <summary>
/// Represents a type that is defined somewhere else in the code.
/// </summary>
/// <param name="token">
/// The <see cref="Token"/> used to mark the start of the node.
/// </param>
/// <param name="filePath">
/// The path of the file that this node exists in.
/// </param>
public sealed class TypeNode([NotNull] Token token, [NotNull] string filePath) : StatementNode(null)
{
    /// <summary>
    /// The name of the type.
    /// </summary>
    public string Name { get; set; } = token.Type is TokenType.Identifier ? token.Value : Constants.TryGetPrimitiveType(token);

    /// <summary>
    /// The resolved type of the node.
    /// </summary>
    public SushiType? ResolvedType { get; set; }

    /// <summary>
    /// Returns whether the type is a reference type or a copy type.
    /// </summary>
    /// <returns>
    /// True if the type is a reference type. False otherwise.
    /// </returns>
    public bool IsReferenceType()
    {
        if (this.ResolvedType is null)
        {
            return false;
        }

        return this.ResolvedType.IsReferenceType();
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<CompilerMessage> GetMessages()
    {
        if (token.Type is TokenType.Identifier && char.IsLower(this.Name[0]))
        {
            yield return new IllegalIdentifierError(token, filePath);
        }
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<Token> GetTokens()
    {
        yield return token;
    }
}
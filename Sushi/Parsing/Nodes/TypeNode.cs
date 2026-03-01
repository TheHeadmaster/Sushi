using System.Diagnostics.CodeAnalysis;
using Sushi.Lexing.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents a type that is defined somewhere else in the code.
/// </summary>
/// <param name="startToken">
/// The token used to mark the start of the node.
/// </param>
public sealed class TypeNode(Token startToken) : SyntaxNode(startToken)
{
    /// <summary>
    /// The name of the type.
    /// </summary>
    public string? Name { get; set; }

    /// <inheritdoc />
    public override Task<bool> VisitKeyword([NotNull] ParsingContext context)
    {
        Token token = context.Peek()!;
        if (Constants.PrimitiveTypes.ContainsKey(token.Value))
        {
            this.Name = Constants.PrimitiveTypes[token.Value];

            context.Pop();

            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }
}

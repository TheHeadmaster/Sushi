using System.Diagnostics.CodeAnalysis;
using Sushi.Lexing.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents an identifier that is defined somewhere else in the code.
/// </summary>
/// <param name="startToken">
/// The token used to mark the start of the node.
/// </param>
public sealed class IdentifierNode(Token startToken) : SyntaxNode(startToken)
{
    /// <summary>
    /// The name of the identifier.
    /// </summary>
    public string? Name { get; set; }

    /// <inheritdoc />
    public override Task<bool> VisitIdentifier([NotNull] ParsingContext context)
    {
        Token token = context.Peek()!;

        this.Name = token.Value;

        this.LineNumber = token.LineNumber;
        this.LinePosition = token.LinePosition;
        this.CurrentLine = token.CurrentLine;
        this.Length = token.Value.Length;

        context.Pop();

        return Task.FromResult(true);
    }
}

using System.Diagnostics.CodeAnalysis;
using Sushi.Lexing.Tokenization;

namespace Sushi.Parsing.Nodes;

public sealed class TypeNode(Token startToken) : SyntaxNode(startToken)
{
    public string? Name { get; set; }

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

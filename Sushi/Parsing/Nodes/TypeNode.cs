using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;
using Sushi.Verification;

namespace Sushi.Parsing.Nodes;

public class TypeNode([NotNull] Token token) : StatementNode
{
    public string Name { get; set; } = token.Type is TokenType.Identifier ? token.Value : Constants.TryGetPrimitiveType(token.Value);

    public override Token GetStartToken() => token;
    public override Task Verify(VerificationContext context) => Task.CompletedTask;
}

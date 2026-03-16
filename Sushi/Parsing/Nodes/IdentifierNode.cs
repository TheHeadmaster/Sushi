using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;
using Sushi.Verification;

namespace Sushi.Parsing.Nodes;

public class IdentifierNode([NotNull] Token token) : ExpressionNode, ICallableNode
{
    public string Name { get; set; } = token.Value;

    public override Token GetStartToken() => token;
    public bool ResolvesToIdentifier() => true;
    public override Task Verify(VerificationContext context) => Task.CompletedTask;
}

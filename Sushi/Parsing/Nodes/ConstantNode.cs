using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;
using Sushi.Verification;

namespace Sushi.Parsing.Nodes;

public class ConstantNode([NotNull] Token token) : ExpressionNode
{
    public string Value { get; set; } = token.Value;

    public override Token GetStartToken() => token;

    public override Task Verify(VerificationContext context) => Task.CompletedTask;
}
using System;
using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;
using Sushi.Verification;

namespace Sushi.Parsing.Nodes;

public class NamespaceNode([NotNull] IdentifierNode identifier, [NotNull] ExpressionNode right) : ExpressionNode
{
    public ExpressionNode Right { get; set; } = right;

    public IdentifierNode Name { get; set; } = identifier;

    public override Token GetStartToken() => this.Name.GetStartToken();
    public override async Task Verify(VerificationContext context)
    {
        await this.Right.Verify(context);

        await this.Name.Verify(context);
    }
}

using System.Diagnostics.CodeAnalysis;
using Sushi.Compilation;
using Sushi.Tokenization;
using Sushi.Verification;

namespace Sushi.Parsing.Nodes;

public class BlockNode([NotNull] Token token, List<StatementNode> statements) : StatementNode
{
    public List<StatementNode> Body { get; set; } = statements;

    public override Token GetStartToken() => token;

    public override async Task Verify(VerificationContext context)
    {
        foreach (StatementNode node in this.Body)
        {
            await node.Verify(context);
        }
    }

    public override async Task Compile([NotNull] Compiler compiler)
    {
        foreach (StatementNode node in this.Body)
        {
            await node.Compile(compiler);
        }
    }

    public override async Task CompileHeader([NotNull] Compiler compiler)
    {
        foreach (StatementNode node in this.Body)
        {
            await node.CompileHeader(compiler);
        }
    }
}

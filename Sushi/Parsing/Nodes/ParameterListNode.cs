using System.Diagnostics.CodeAnalysis;
using Sushi.Compilation;
using Sushi.Tokenization;
using Sushi.Verification;

namespace Sushi.Parsing.Nodes;

public class ParameterListNode([NotNull] Token token, [NotNull] List<ParameterNode> parameters) : StatementNode
{
    public List<ParameterNode> Parameters { get; set; } = parameters;

    public override Token GetStartToken() => token;
    public override async Task Verify(VerificationContext context)
    {
        foreach (ParameterNode parameter in this.Parameters)
        {
            await parameter.Verify(context);
        }
    }

    public override async Task Compile([NotNull] Compiler compiler)
    {
        bool isFirst = true;

        foreach (ParameterNode parameter in this.Parameters)
        {
            if (!isFirst)
            {
                await compiler.Write(", ");
            }

            await parameter.Compile(compiler);
        }
    }

    public override async Task CompileHeader([NotNull] Compiler compiler)
    {
        bool isFirst = true;

        foreach (ParameterNode parameter in this.Parameters)
        {
            if (!isFirst)
            {
                await compiler.WriteHeader(", ");
            }

            await parameter.CompileHeader(compiler);
        }
    }
}

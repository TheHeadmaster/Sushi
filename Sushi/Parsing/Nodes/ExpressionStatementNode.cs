using System.Diagnostics.CodeAnalysis;
using Sushi.Compilation;
using Sushi.Tokenization;
using Sushi.Verification;

namespace Sushi.Parsing.Nodes;

public class ExpressionStatementNode(ExpressionNode? expression) : StatementNode
{
    public ExpressionNode? Expression { get; set; } = expression;

    public override Token? GetStartToken() => this.Expression?.GetStartToken();
    public override async Task Verify(VerificationContext context)
    {
        if (this.Expression is not null)
        {
            await this.Expression.Verify(context);
        }
    }

    public override async Task Compile([NotNull] Compiler compiler)
    {
        await this.Expression.Compile(compiler);
        await compiler.Write(";");
        await compiler.EndLine();
    }

    public override async Task CompileHeader([NotNull] Compiler compiler)
    {
        await this.Expression.CompileHeader(compiler);
        await compiler.WriteHeader(";");
        await compiler.HeaderEndLine();
    }
}

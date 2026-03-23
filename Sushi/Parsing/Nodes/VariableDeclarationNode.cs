using System.Diagnostics.CodeAnalysis;
using Sushi.Compilation;
using Sushi.Tokenization;
using Sushi.Verification;

namespace Sushi.Parsing.Nodes;

public class VariableDeclarationNode(TypeNode? type, AssignmentNode? assignment) : StatementNode
{
    public TypeNode? Type { get; set; } = type;

    public AssignmentNode? Assignment { get; set; } = assignment;

    public override Token? GetStartToken() => this.Type?.GetStartToken();
    public override async Task Verify(VerificationContext context)
    {
        if (this.Type is not null)
        {
            await this.Type.Verify(context);
        }

        if (this.Assignment is not null)
        {
            await this.Assignment.Verify(context);
        }
    }

    public override async Task Compile([NotNull] Compiler compiler)
    {
        if (this.Assignment is not null)
        {
            await this.Assignment.Compile(compiler);
        }

        await compiler.Write(";");
        await compiler.EndLine();
    }

    public override async Task CompileHeader([NotNull] Compiler compiler)
    {
        if (this.Assignment is not null)
        {
            await this.Assignment.CompileHeader(compiler);
        }

        await compiler.WriteHeader(";");
        await compiler.HeaderEndLine();
    }
}

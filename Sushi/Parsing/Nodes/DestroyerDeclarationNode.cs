using System.Diagnostics.CodeAnalysis;
using Sushi.Compilation;
using Sushi.Tokenization;
using Sushi.Verification;

namespace Sushi.Parsing.Nodes;

public class DestroyerDeclarationNode([NotNull] Token token, IdentifierNode? name, ParameterListNode? parameterList, BlockNode? body) : StatementNode
{
    public IdentifierNode? Name { get; set; } = name;
    public ParameterListNode? ParameterList { get; set; } = parameterList;
    public BlockNode? Body { get; set; } = body;
    public override Token GetStartToken() => token;
    public override async Task Verify(VerificationContext context)
    {
        if (this.Name is not null)
        {
            await this.Name.Verify(context);
        }

        if (this.ParameterList is not null)
        {
            await this.ParameterList.Verify(context);
        }

        if (this.Body is not null)
        {
            await this.Body.Verify(context);
        }
    }

    public override async Task Compile([NotNull] Compiler compiler)
    {
        await compiler.Write("void ");

        if (this.Name is not null)
        {
            await this.Name.Compile(compiler);
        }

        await compiler.Write($" ");
        await compiler.Write("(");

        if (this.ParameterList is not null)
        {
            await this.ParameterList.Compile(compiler);
        }

        await compiler.Write(")");
        await compiler.EndLine();
        await compiler.WriteLine("{");
        await compiler.Indent();

        if (this.Body is not null)
        {
            await this.Body.Compile(compiler);
        }

        await compiler.Dedent();
        await compiler.WriteLine("}");
    }

    public override async Task CompileHeader([NotNull] Compiler compiler)
    {
        await compiler.WriteHeader("void (*");

        if (this.Name is not null)
        {
            await this.Name.CompileHeader(compiler);
        }

        await compiler.WriteHeader(")");
        await compiler.WriteHeader("(");

        if (this.ParameterList is not null)
        {
            await this.ParameterList.CompileHeader(compiler);
        }

        await compiler.WriteHeader(");");
        await compiler.HeaderEndLine();
    }
}
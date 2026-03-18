using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sushi.Compilation;
using Sushi.Tokenization;
using Sushi.Verification;

namespace Sushi.Parsing.Nodes;

public class MethodDeclarationNode([NotNull] Token token, TypeNode? returnType, IdentifierNode? name, List<ParameterNode> parameters, BlockNode? body) : StatementNode
{
    public TypeNode? ReturnType { get; set; } = returnType;

    public IdentifierNode? Name { get; set; } = name;

    public List<ParameterNode> Parameters { get; set; } = parameters;

    public BlockNode? Body { get; set; } = body;

    public override Token GetStartToken() => token;
    public override async Task Verify(VerificationContext context)
    {
        if (this.ReturnType is not null)
        {
            await this.ReturnType.Verify(context);
        }
        
        await this.Name.Verify(context);
    }

    public override async Task Compile([NotNull] Compiler compiler)
    {
        await compiler.Write($"{this.ReturnType?.Name ?? "void"} {this.Name.Name}");
        await compiler.Write("()");
        await compiler.EndLine();
        await compiler.WriteLine("{");
        await compiler.Indent();

        await compiler.Dedent();
        await compiler.WriteLine("}");
    }

    public override async Task CompileHeader([NotNull] Compiler compiler)
    {
        await compiler.WriteHeaderLine($"{this.ReturnType?.Name ?? "void"} {this.Name.Name};");
    }
}

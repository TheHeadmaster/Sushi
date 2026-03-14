using System.Diagnostics.CodeAnalysis;
using Sushi.Compilation;
using Sushi.Tokenization;
using Sushi.Verification;

namespace Sushi.Parsing.Nodes;

public class ClassNode([NotNull] Token token, [NotNull] IdentifierNode identifier, [NotNull] BlockNode body) : StatementNode, ICanBeStatic, IAccessModifiable
{
    public bool IsStatic { get; set; }

    public IdentifierNode Name { get; set; } = identifier;

    public BlockNode Body { get; set; } = body;
    public AccessModifier AccessModifier { get; set; }

    public override Token GetStartToken() => token;

    public override async Task Verify(VerificationContext context)
    {
        await this.Name.Verify(context);
        await this.Body.Verify(context);
    }

    public override async Task Compile([NotNull] Compiler compiler) => await this.Body.Compile(compiler);

    public override async Task CompileHeader([NotNull] Compiler compiler)
    {
        await compiler.WriteHeaderLine("typedef struct");
        await compiler.WriteHeaderLine("{");
        await compiler.IndentHeader();

        await this.Body.CompileHeader(compiler);

        await compiler.DedentHeader();
        await compiler.WriteHeaderLine($"}} {this.Name.Name};");
    }
}

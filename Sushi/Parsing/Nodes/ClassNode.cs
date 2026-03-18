using System.Diagnostics.CodeAnalysis;
using Sushi.Compilation;
using Sushi.Parsing.Core;
using Sushi.Tokenization;
using Sushi.Verification;

namespace Sushi.Parsing.Nodes;

public class ClassNode([NotNull] Token token, TypeNode? identifier, BlockNode? body) : StatementNode, ICanBeStatic, IAccessModifiable
{
    public bool IsStatic { get; set; }

    public TypeNode? Name { get; set; } = identifier;

    public BlockNode? Body { get; set; } = body;
    public AccessModifier AccessModifier { get; set; }

    public override Token GetStartToken() => token;

    public override async Task Verify(VerificationContext context)
    {
        if (this.Name is not null)
        {
            await this.Name.Verify(context);
        }

        if (this.Body is not null)
        {
            await this.Body.Verify(context);
        }
    }

    /// <inheritdoc />
    public override async Task Compile([NotNull] Compiler compiler)
    {
        if (this.Body is not null)
        {
            await this.Body.Compile(compiler);
        }
    }

    /// <inheritdoc />
    public override async Task CompileHeader([NotNull] Compiler compiler)
    {
        await compiler.WriteHeaderLine("typedef struct");
        await compiler.WriteHeaderLine("{");
        await compiler.IndentHeader();

        if (this.Body is not null)
        {
            await this.Body.CompileHeader(compiler);
        }

        await compiler.DedentHeader();

        string resolvedName = this.Name is null ? string.Empty : this.Name.ResolvedType is null ? this.Name.Name : this.Name.ResolvedType.FullName.Replace('.', '_');

        await compiler.WriteHeaderLine($"}} {resolvedName};");
    }
}

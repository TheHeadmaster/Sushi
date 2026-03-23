using System.Diagnostics.CodeAnalysis;
using Sushi.Compilation;
using Sushi.Tokenization;
using Sushi.Verification;

namespace Sushi.Parsing.Nodes;

public class ParameterNode(TypeNode? type, IdentifierNode? identifier) : StatementNode
{
    public TypeNode? Type { get; set; } = type;

    public IdentifierNode? Name { get; set; } = identifier;

    public override Token? GetStartToken() => this.Type?.GetStartToken();

    public override async Task Verify(VerificationContext context)
    {
        if (this.Type is not null)
        {
            await this.Type.Verify(context);
        }

        if (this.Name is not null)
        {
            await this.Name.Verify(context);
        }
    }

    public override async Task Compile([NotNull] Compiler compiler)
    {
        string resolvedName = this.Type is null ? string.Empty : this.Type.ResolvedType is null ? this.Type.Name : this.Type.ResolvedType.FullName.Replace('.', '_');

        await compiler.Write($"{resolvedName} {this.Name?.Name ?? string.Empty}");
    }

    public override async Task CompileHeader([NotNull] Compiler compiler)
    {
        string resolvedName = this.Type is null ? string.Empty : this.Type.ResolvedType is null ? this.Type.Name : this.Type.ResolvedType.FullName.Replace('.', '_');

        await compiler.WriteHeader($"{resolvedName} {this.Name?.Name ?? string.Empty}");
    }
}

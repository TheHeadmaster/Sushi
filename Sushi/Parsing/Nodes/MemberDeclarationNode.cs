using System.Diagnostics.CodeAnalysis;
using Sushi.Compilation;
using Sushi.Tokenization;
using Sushi.Verification;

namespace Sushi.Parsing.Nodes;

public class MemberDeclarationNode([NotNull] Token token, [NotNull] TypeNode type, [NotNull] IdentifierNode identifier) : StatementNode
{
    public TypeNode Type { get; set; } = type;

    public IdentifierNode Identifier { get; set; } = identifier;

    public override Token GetStartToken() => token;
    public override async Task Verify(VerificationContext context)
    {
        await this.Type.Verify(context);
        await this.Identifier.Verify(context);
    }

    public override async Task CompileHeader([NotNull] Compiler compiler)
    {
        string resolvedName = this.Type is null ? string.Empty : this.Type.ResolvedType is null ? this.Type.Name : this.Type.ResolvedType.FullName.Replace('.', '_');

        await compiler.WriteHeaderLine($"{resolvedName} {this.Identifier.Name};");
    }
}

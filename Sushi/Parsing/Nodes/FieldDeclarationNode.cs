using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sushi.Compilation;
using Sushi.Tokenization;
using Sushi.Verification;

namespace Sushi.Parsing.Nodes;

public class FieldDeclarationNode([NotNull] Token token, [NotNull] IdentifierNode type, [NotNull] IdentifierNode identifier) : StatementNode
{
    public IdentifierNode Type { get; set; } = type;

    public IdentifierNode Identifier { get; set; } = identifier;

    public override Token GetStartToken() => token;
    public override async Task Verify(VerificationContext context)
    {
        await this.Type.Verify(context);
        await this.Identifier.Verify(context);
    }

    public override async Task CompileHeader([NotNull] Compiler compiler)
    {
        await compiler.WriteHeaderLine($"{this.Type.Name} {this.Identifier.Name};");
    }
}

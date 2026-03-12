using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

public class ClassNode([NotNull] Token token, [NotNull] IdentifierNode identifier, [NotNull] BlockNode body, [NotNull] bool isStatic) : StatementNode
{
    public bool IsStatic { get; set; } = isStatic;

    public IdentifierNode Name { get; set; } = identifier;

    public BlockNode Body { get; set; } = body;

    public override Token GetStartToken() => token;
}

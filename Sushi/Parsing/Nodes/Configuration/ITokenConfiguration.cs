using System;
using System.Collections.Generic;
using System.Text;

namespace Sushi.Parsing.Nodes.Configuration;

public interface ITokenConfiguration<TNode> where TNode : SyntaxNode
{
    public ITokenConfiguration<TNode> IsRequired();
}

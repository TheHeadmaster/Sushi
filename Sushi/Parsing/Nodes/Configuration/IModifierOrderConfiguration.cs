using System;
using System.Collections.Generic;
using System.Text;

namespace Sushi.Parsing.Nodes.Configuration;

public interface IModifierOrderConfiguration
{
    public IModifierOrderThenConfiguration ByAccessModifier();
}

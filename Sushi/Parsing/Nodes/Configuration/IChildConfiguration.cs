using System;
using System.Collections.Generic;
using System.Text;

namespace Sushi.Parsing.Nodes.Configuration;

public interface IChildConfiguration
{
    public IChildConfiguration IsRequired(bool value = true);
}

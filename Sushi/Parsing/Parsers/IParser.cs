using System;
using System.Collections.Generic;
using System.Text;

namespace Sushi.Parsing.Parsers;

public interface IParser
{
    public BindingPower Power();
}

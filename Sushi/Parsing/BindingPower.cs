using System;
using System.Collections.Generic;
using System.Text;

namespace Sushi.Parsing;

public enum BindingPower : int
{
    Primary = 0,
    Assignment = 1,
    Conditional = 2,
    SumDifference = 3,
    ProductQuotient = 4,
    Exponent = 5,
    Prefix = 6,
    Postfix = 7,
    Call = 8
}
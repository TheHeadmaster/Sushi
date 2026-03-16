using System;
using System.Collections.Generic;
using System.Text;

namespace Sushi.Parsing;

/// <summary>
/// Denotes what type and context of statements that the parser can actually parse.
/// </summary>
public enum ParserRole
{
    TopLevelStatement,
    AccessModifier,
    StaticModifier,
    BlockStatement
}

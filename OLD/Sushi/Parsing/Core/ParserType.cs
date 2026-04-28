namespace Sushi.Parsing.Core;

/// <summary>
/// Represents the type of parser that a parser is, such as statement or prefix parser.
/// </summary>
public enum ParserType
{
    Prefix,
    Infix,
    Statement
}

namespace Sushi.Parsing;

/// <summary>
/// Represents the type of parser that a parser is, such as statement or prefix parser.
/// </summary>
public enum ParserType
{
    Prefix,
    Infix,
    Statement
}

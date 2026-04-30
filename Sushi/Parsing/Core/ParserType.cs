namespace Sushi.Parsing.Core;

/// <summary>
/// Represents the type of parser that a parser is, such as statement or prefix parser.
/// </summary>
public enum ParserType
{
    /// <summary>
    /// This parser is a prefix parser.
    /// </summary>
    Prefix,

    /// <summary>
    /// This parser is an infix parser.
    /// </summary>
    Infix,

    /// <summary>
    /// This parser is a statement parser.
    /// </summary>
    Statement
}


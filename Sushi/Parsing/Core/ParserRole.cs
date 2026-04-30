namespace Sushi.Parsing.Core;

/// <summary>
/// Denotes what type and context of statements that the parser can actually parse.
/// </summary>
public enum ParserRole
{
    /// <summary>
    /// This parser can parse top-level statements.
    /// </summary>
    TopLevelStatement,

    /// <summary>
    /// This parser can parse access modified statements.
    /// </summary>
    AccessModifier,

    /// <summary>
    /// This parser can parse static modified statements.
    /// </summary>
    StaticModifier,

    /// <summary>
    /// This parser can parse block statements.
    /// </summary>
    BlockStatement,

    /// <summary>
    /// This parser can parse member declarations.
    /// </summary>
    MemberDeclaration,

    /// <summary>
    /// This parser can parse parameters.
    /// </summary>
    Parameter,

    /// <summary>
    /// This parser can parse parameter lists.
    /// </summary>
    ParameterList
}

using Sushi.Parsing.Syntax;

namespace Sushi.Parsing;

/// <summary>
/// Represents the syntax tree.
/// </summary>
/// <param name="Tree">
/// The concrete syntax tree produced by the parser.
/// </param>
public sealed record ParserResult(ConcreteSyntaxTree Tree);
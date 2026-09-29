using Sushi.Diagnostics;
using Sushi.Parsing.Syntax;

namespace Sushi.Parsing;

/// <summary>
/// Represents the syntax tree and diagnostics produced by parsing source.
/// </summary>
/// <param name="Tree">
/// The concrete syntax tree produced by the parser.
/// </param>
/// <param name="Diagnostics">
/// Diagnostics produced while recovering and structuring syntax.
/// </param>
public sealed record ParserResult(ConcreteSyntaxTree Tree, IReadOnlyList<SushiDiagnostic> Diagnostics);
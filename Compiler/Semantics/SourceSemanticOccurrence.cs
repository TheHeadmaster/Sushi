using Sushi.Documentation;
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.Semantics;

/// <summary>
/// Identifies a source declaration category that can provide editor information.
/// </summary>
internal enum SourceSemanticSymbolKind
{
    Function,
    Activity,
    ActivityMethod,
    Parameter,
    Local,
    Type
}

/// <summary>
/// Describes a compiler-resolved source symbol independently of its spelling at a use site.
/// </summary>
/// <param name="Declaration">The exact declaration syntax node selected during binding.</param>
/// <param name="Kind">The source symbol category presented by tooling.</param>
/// <param name="Index">The parameter or local ordinal when the symbol has one.</param>
/// <param name="Name">The compiler-decoded semantic name.</param>
/// <param name="Type">The bound built-in type when the symbol is typed.</param>
/// <param name="Callable">The callable signature that owns or defines this symbol.</param>
/// <param name="Documentation">The documentation attached to the defining declaration.</param>
/// <param name="ContainingName">The containing callable or activity name when useful.</param>
/// <param name="Accessibility">The declared access level for activities when available.</param>
/// <param name="CompilerSymbol">The compiler-assigned callable identity for a resolved function reference.</param>
/// <param name="DefinitionSpan">The exact source token that declares this symbol, when it has a source definition.</param>
internal sealed record SourceSemanticSymbol(
    SyntaxNode Declaration,
    SourceSemanticSymbolKind Kind,
    int Index,
    string Name,
    BoundType? Type,
    BoundFunction? Callable,
    DocumentationBlock? Documentation,
    string? ContainingName,
    Accessibility? Accessibility = null,
    string? CompilerSymbol = null,
    SourceSpan? DefinitionSpan = null);

/// <summary>
/// Associates a source-token range with the exact symbol selected during binding.
/// </summary>
/// <param name="Span">
/// The declaration or reference token in its owning source snapshot.
/// </param>
/// <param name="Symbol">
/// The compiler-resolved semantic symbol.
/// </param>
/// <param name="IsDeclaration">
/// Whether this occurrence is the symbol's declaration token.
/// </param>
internal sealed record SourceSemanticOccurrence(SourceSpan Span, SourceSemanticSymbol Symbol, bool IsDeclaration = false);

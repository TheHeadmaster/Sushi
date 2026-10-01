namespace Sushi.Parsing.Syntax;

/// <summary>
/// Identifies the grammatical role of syntax nodes and tokens in a Sushi concrete syntax tree.
/// </summary>
public enum SyntaxType
{
    PackageDeclaration,
    QualifiedName,
    PackageKeyword,
    IdentifierToken,
    EscapedIdentifierToken,
    DotToken,
    SemicolonToken,
    SourceFile
}
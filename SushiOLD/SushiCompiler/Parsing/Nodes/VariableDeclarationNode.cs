using SushiCompiler.Tokenization;

namespace SushiCompiler.Parsing.Nodes;

internal class VariableDeclarationNode(Token variableDeclaration) : SyntaxNode
{
    internal string VariableType { get; set; } = variableDeclaration.Value;

    internal string? VariableName { get; set; }

    internal override Type ILGenerator { get; } = null!;
}

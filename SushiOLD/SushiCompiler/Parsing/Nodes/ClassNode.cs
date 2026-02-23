using SushiCompiler.Compiling.L2.ILGenerators;

namespace SushiCompiler.Parsing.Nodes;

internal class ClassNode : SyntaxNode
{
    internal string? ClassName { get; set; }
    internal override Type ILGenerator { get; } = typeof(ClassILGenerator);
}

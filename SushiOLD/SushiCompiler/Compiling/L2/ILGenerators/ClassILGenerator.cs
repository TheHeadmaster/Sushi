using SushiCompiler.Parsing;
using SushiCompiler.Parsing.Nodes;
using System.Text;

namespace SushiCompiler.Compiling.L2.ILGenerators;

internal class ClassILGenerator : ILGenerator
{
    internal override Task<string> GenerateIL(SyntaxNode node, List<ILGenerator> ilGenerators)
    {
        if (node is not ClassNode classNode)
        {
            return Task.FromResult(string.Empty);
        }

        StringBuilder sb = new();

        sb.AppendLine($".class private auto ansi {classNode.ClassName} extends [System.Runtime]System.Object");
        sb.AppendLine("{");
        sb.AppendLine("}");

        return Task.FromResult(sb.ToString());
    }
}

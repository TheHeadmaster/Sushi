using SushiCompiler.Compiling.L2;
using SushiCompiler.Parsing;
using SushiCompiler.Parsing.Nodes;

namespace SushiCompiler.Steps;

internal static class ILWriter
{
    internal static async Task Write()
    {
        List<FileNode> nodes = AbstractSyntaxTree.FileNodes;

        List<ILGenerator> ilGenerators = ILGenerator.GetGenerators();

        foreach (var node in nodes)
        {
            if (node.ILGenerator is null)
            {
                continue;
            }

            ILGenerator? existing = ilGenerators.FirstOrDefault(x => x.GetType() == node.ILGenerator) ?? throw new InvalidOperationException("No generator found for node");

            string ilFile = await existing.GenerateIL(node, ilGenerators);

            if (!Directory.Exists("intermediate"))
            {
                Directory.CreateDirectory("intermediate");
            }

            foreach (string filePath in Directory.GetFiles("intermediate"))
            {
                File.Delete(filePath);
            }

            File.WriteAllText(Path.Combine("intermediate", $"{Path.GetFileNameWithoutExtension(node.FileName)}.il"), ilFile);
        }
    }
}

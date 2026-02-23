using SushiCompiler.Parsing;
using System.Reflection;

namespace SushiCompiler.Compiling.L2;

internal abstract class ILGenerator
{
    internal abstract Task<string> GenerateIL(SyntaxNode node, List<ILGenerator> ilGenerators);

    internal static List<ILGenerator> GetGenerators()
    {
        Assembly assembly = typeof(ILGenerator).Assembly;
        Type baseType = typeof(ILGenerator);

        List<Type> concreteSubclasses = [.. assembly.GetTypes().Where(type => type.IsSubclassOf(baseType) && !type.IsAbstract && !type.IsInterface)];

        List<Type> leafSubclasses = [.. concreteSubclasses.Where(type => !concreteSubclasses.Any(otherType => otherType.IsSubclassOf(type)))];

        List<ILGenerator> instances = [];
        foreach (var type in leafSubclasses)
        {
            object? instance = Activator.CreateInstance(type);
            if (instance is ILGenerator typedInstance)
            {
                instances.Add(typedInstance);
            }
        }

        return instances;
    }
}

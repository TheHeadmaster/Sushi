using System.Reflection;

namespace Sushi.Extensions;

/// <summary>
/// Contains extensions for reflection.
/// </summary>
public static class ReflectionEx
{
    /// <summary>
    /// Gets the leaf subclasses of the specified type. Leaf subclasses in this context
    /// are classes that are themselves not inherited by a class and not abstract.
    /// </summary>
    /// <typeparam name="T">
    /// The type to get the leaf subclasses of.
    /// </typeparam>
    /// <returns>
    /// The list of leaf subclasses.
    /// </returns>
    public static List<T> GetLeafSubclasses<T>()
    {
        Assembly assembly = typeof(T).Assembly;
        Type baseType = typeof(T);

        List<Type> concreteSubclasses = [.. assembly.GetTypes().Where(type => type.IsSubclassOf(baseType) && !type.IsAbstract && !type.IsInterface)];
        List<Type> leafSubclasses = [.. concreteSubclasses.Where(type => !concreteSubclasses.Any(otherType => otherType.IsSubclassOf(type)))];

        List<T> instances = [];

        foreach (Type type in leafSubclasses)
        {
            object? instance = Activator.CreateInstance(type);
            if (instance is T typedInstance)
            {
                instances.Add(typedInstance);
            }
        }

        return instances;
    }
}

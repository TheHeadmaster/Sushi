namespace System.Reflection;

/// <summary>
/// Contains extensions for reflection.
/// </summary>
public static class ReflectionEx
{
    /// <summary>
    /// Gets the leaf subclasses of the specified type. Leaf subclasses in this context
    /// are classes that are themselves not inherited by a class and not abstract. Leaf
    /// subclasses must have a parameterless constructor or this will throw an error.
    /// </summary>
    /// <typeparam name="T">
    /// The type to get the leaf subclasses of.
    /// </typeparam>
    /// <returns>
    /// The list of leaf subclasses.
    /// </returns>
    public static IEnumerable<T> GetLeafSubclasses<T>()
    {
        Assembly assembly = typeof(T).Assembly;
        Type baseType = typeof(T);

        List<Type> subclasses = [.. assembly.GetTypes().Where(type => type != baseType && type.IsAssignableTo(baseType) && !type.IsInterface)];
        List<Type> leafSubclasses = [.. subclasses.Where(type => !type.IsAbstract && !type.ContainsGenericParameters && !subclasses.Any(otherType => otherType.IsSubclassOf(type)))];

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
namespace Sushi.Parsing;

/// <summary>
/// Represents a scope in which identifiers and type names live. Two identifiers with the same name
/// of cannot live in the same scope. Two types of the same name cannot live in the same scope.
/// Identifiers in a parent scope will be superseded by identifiers in a child scope. Types of the
/// same name will have to be disambiguated.
/// </summary>
public sealed class ReferenceScope(ReferenceScope? parentScope)
{
    /// <summary>
    /// The type references collected from traversing the source.
    /// </summary>
    private readonly List<SushiType> types = [];

    /// <summary>
    /// The identifiers collected from traversing the source.
    /// </summary>
    private readonly List<SushiIdentifier> identifiers = [];

    /// <summary>
    /// The scope that this scope exists within. If the scope does not have a parent, then it is the global scope.
    /// </summary>
    public ReferenceScope? ParentScope { get; set; } = parentScope;

    /// <summary>
    /// Resolves a given type name to the type object, or null if it wasn't found in the scope chain.
    /// This will resolve the first type it finds from the bottom up, therefore enforcing supersession
    /// of scopes.
    /// </summary>
    /// <param name="name">
    /// The name of the type to resolve.
    /// </param>
    /// <returns>
    /// The <see cref="SushiType"/> or null if it was not found.
    /// </returns>
    public SushiType? ResolveType(string name)
    {
        SushiType? existing = this.types.FirstOrDefault(x => x.Name == name);

        if (existing is null)
        {
            return this.ParentScope?.ResolveType(name) ?? null;
        }

        return existing;
    }

    /// <summary>
    /// Resolves a given identifier name to the identifier object, or null if it wasn't found in the scope chain.
    /// This will resolve the first identifier it finds from the bottom up, therefore enforcing supersession
    /// of scopes.
    /// </summary>
    /// <param name="name">
    /// The name of the identifier to resolve.
    /// </param>
    /// <returns>
    /// The <see cref="SushiIdentifier"/> or null if it was not found.
    /// </returns>
    public SushiIdentifier? ResolveIdentifier(string name)
    {
        SushiIdentifier? existing = this.identifiers.FirstOrDefault(x => x.Name == name);

        if (existing is null)
        {
            return this.ParentScope?.ResolveIdentifier(name) ?? null;
        }

        return existing;
    }

    /// <summary>
    /// Tries to register a new type name to the scope. If the type name already resolves to an existing type, returns false. Otherwise, returns true.
    /// </summary>
    /// <param name="name">
    /// The name of the new type.
    /// </param>
    /// <returns>
    /// True for success, false for name collision error.
    /// </returns>
    public bool TryAddType(string name)
    {
        SushiType? resolvedType = this.ResolveType(name);

        if (resolvedType is not null)
        {
            return false;
        }

        this.types.Add(new SushiType() { Name = name });

        return true;
    }

    /// <summary>
    /// Tries to register a new identifier name to the scope. If the identifier name already resolves to an existing identifier, returns false. Otherwise, returns true.
    /// </summary>
    /// <param name="name">
    /// The name of the new identifier.
    /// </param>
    /// <param name="type">
    /// The name of the type declared when the identifier was declared.
    /// </param>
    /// <returns>
    /// True for success, false for name collision error.
    /// </returns>
    public bool TryAddIdentifier(string name, string type)
    {
        SushiIdentifier? resolvedIdentifier = this.ResolveIdentifier(name);

        if (resolvedIdentifier is not null)
        {
            return false;
        }

        this.identifiers.Add(new SushiIdentifier() { Name = name, Type = type });

        return true;
    }
}

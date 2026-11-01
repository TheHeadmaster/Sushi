namespace Sushi.Semantics;

/// <summary>
/// Maintains lexical local scopes and stable declaration identities within a function.
/// Parameter bindings remain visible in every nested scope.
/// </summary>
internal sealed class LocalBindingContext
{
    private readonly IReadOnlyList<BoundParameter> parameters;
    private readonly Stack<Dictionary<string, BoundLocal>> scopes = new();
    private int nextIndex;

    /// <summary>
    /// Creates a function-local context with its declared parameters.
    /// </summary>
    /// <param name="parameters">
    /// The enclosing function's parameters.
    /// </param>
    public LocalBindingContext(IReadOnlyList<BoundParameter> parameters)
    {
        this.parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
    }

    /// <summary>
    /// Begins a nested lexical block.
    /// </summary>
    public void EnterScope() => this.scopes.Push(new Dictionary<string, BoundLocal>(StringComparer.Ordinal));

    /// <summary>
    /// Ends the innermost lexical block without removing outer bindings.
    /// </summary>
    public void ExitScope() => this.scopes.Pop();

    /// <summary>
    /// Declares an initialized local unless its name conflicts with a visible binding.
    /// </summary>
    /// <param name="name">
    /// The new local's semantic name.
    /// </param>
    /// <param name="type">
    /// The explicitly declared value type.
    /// </param>
    /// <param name="local">
    /// The new declaration identity when successful.
    /// </param>
    /// <returns>
    /// True if the declaration was introduced without a naming conflict.
    /// </returns>
    public bool TryDeclare(string name, BoundType type, out BoundLocal? local)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (this.Resolve(name) is not null)
        {
            local = null;
            return false;
        }

        local = new BoundLocal(this.nextIndex++, name, type);
        this.scopes.Peek().Add(name, local);
        return true;
    }

    /// <summary>
    /// Resolves a visible local or enclosing function parameter by semantic name.
    /// </summary>
    /// <param name="name">
    /// The identifier being referenced.
    /// </param>
    /// <returns>
    /// A typed binding reference, or null when no binding is visible.
    /// </returns>
    public BoundExpression? Resolve(string name)
    {
        foreach (Dictionary<string, BoundLocal> scope in this.scopes)
        {
            if (scope.TryGetValue(name, out BoundLocal? local))
            {
                return new BoundLocalReferenceExpression(local);
            }
        }

        BoundParameter? parameter = this.parameters.FirstOrDefault(candidate => StringComparer.Ordinal.Equals(candidate.Name, name));
        return parameter is null ? null : new BoundParameterReferenceExpression(parameter);
    }
}
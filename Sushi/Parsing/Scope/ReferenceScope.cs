namespace Sushi.Parsing.Scope;

/// <summary>
/// A reference scope is a container for references that are only accessible from within that scope or a child scope, and tracks whether a reference
/// is referred to before it is declared within that scope, or if shadowing of references should happen, or if there is a name collision.
/// </summary>
public sealed class ReferenceScope(ReferenceScope? parentScope = null)
{
    public Task BeginScope()
    {
        return Task.CompletedTask;
    }

    public Task EndScope()
    {
        return Task.CompletedTask;
    }
}

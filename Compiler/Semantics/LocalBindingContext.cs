using System.Collections.ObjectModel;
using Sushi.Source;

namespace Sushi.Semantics;

/// <summary>
/// Maintains lexical local scopes, stable declaration identities, and
/// enclosing loop depth within a function.
/// </summary>
internal sealed class LocalBindingContext
{
    private readonly IReadOnlyList<BoundParameter> parameters;
    private readonly Stack<Dictionary<string, BoundLocal>> scopes = new();
    private readonly Dictionary<int, SourceSpan> declarations = [];
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
    /// Gets the number of enclosing loops active at the current binding location.
    /// Ordinary blocks and conditionals do not increase this depth.
    /// </summary>
    public int LoopDepth { get; private set; }

    /// <summary>
    /// Begins a nested lexical block.
    /// </summary>
    public void EnterScope() => this.scopes.Push(new Dictionary<string, BoundLocal>(StringComparer.Ordinal));

    /// <summary>
    /// Ends the innermost lexical block without removing outer bindings.
    /// </summary>
    public void ExitScope() => this.scopes.Pop();

    /// <summary>
    /// Begins binding a loop body, making its boundary available to
    /// break and continue statements contained within that body.
    /// </summary>
    public void EnterLoop() => this.LoopDepth++;

    /// <summary>
    /// Completes binding the innermost enclosing loop body.
    /// </summary>
    public void ExitLoop()
    {
        if (this.LoopDepth == 0)
        {
            throw new InvalidOperationException("No active loop is available to exit.");
        }

        this.LoopDepth--;
    }

    /// <summary>
    /// Declares an initialized local unless its name conflicts with a visible binding.
    /// </summary>
    /// <param name="name">
    /// The new local's semantic name.
    /// </param>
    /// <param name="type">
    /// The explicitly declared value type.
    /// </param>
    /// <param name="nameSpan">
    /// The exact source token that introduced the local.
    /// </param>
    /// <param name="local">
    /// The new declaration identity when successful.
    /// </param>
    /// <returns>
    /// True if the declaration was introduced without a naming conflict.
    /// </returns>
    public bool TryDeclare(string name, BoundType type, SourceSpan nameSpan, out BoundLocal? local)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (nameSpan.Snapshot is null || nameSpan.Start < 0 || nameSpan.Start >= nameSpan.End
            || nameSpan.End > nameSpan.Snapshot.SourceLength)
        {
            throw new ArgumentException("A local declaration requires its nonempty source-backed name token.", nameof(nameSpan));
        }

        if (this.Resolve(name) is not null)
        {
            local = null;
            return false;
        }

        local = new BoundLocal(this.nextIndex++, name, type);
        if (!this.declarations.TryAdd(local.Index, nameSpan))
        {
            throw new InvalidOperationException("A function-local index was assigned more than one declaration span.");
        }

        this.scopes.Peek().Add(name, local);
        return true;
    }

    /// <summary>
    /// Returns immutable source-name spans for every successfully declared function local.
    /// </summary>
    /// <returns>
    /// A read-only map keyed by the local indices assigned by this context.
    /// </returns>
    public IReadOnlyDictionary<int, SourceSpan> GetDeclarationSpans()
        => new ReadOnlyDictionary<int, SourceSpan>(new Dictionary<int, SourceSpan>(this.declarations));

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

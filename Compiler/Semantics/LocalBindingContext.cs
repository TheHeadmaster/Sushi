using System.Collections.ObjectModel;
using Sushi.Documentation;
using Sushi.Parsing.Syntax;
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
    private readonly List<SourceSemanticOccurrence> occurrences = [];
    private readonly Dictionary<int, SourceSemanticSymbol> localSymbols = [];
    private readonly FunctionDeclarationSyntax? owner;
    private readonly BoundFunction? function;
    private readonly DocumentationBlock? documentation;
    private int nextIndex;

    /// <summary>
    /// Creates a function-local context that can retain source navigation metadata.
    /// </summary>
    /// <param name="parameters">The enclosing function's bound parameters.</param>
    /// <param name="owner">The exact source declaration that owns this binding context.</param>
    /// <param name="function">The validated callable signature used for this body.</param>
    /// <param name="documentation">The documentation attached to the callable, if any.</param>
    public LocalBindingContext(IReadOnlyList<BoundParameter> parameters, FunctionDeclarationSyntax owner, BoundFunction function, DocumentationBlock? documentation)
    {
        this.parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.function = function ?? throw new ArgumentNullException(nameof(function));
        this.documentation = documentation;
    }

    /// <summary>
    /// Creates a binding context for compiler paths that do not carry source-Hover ownership.
    /// </summary>
    /// <param name="parameters">The enclosing function's bound parameters.</param>
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
    /// <param name="name">The new local's semantic name.</param>
    /// <param name="type">The explicitly declared value type.</param>
    /// <param name="nameSpan">The exact source token that introduced the local.</param>
    /// <param name="local">The new declaration identity when successful.</param>
    /// <returns>True if the declaration was introduced without a naming conflict.</returns>
    public bool TryDeclare(string name, BoundType type, SourceSpan nameSpan, SourceSpan? typeSpan, out BoundLocal? local)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (nameSpan.Snapshot is null || nameSpan.Start < 0 || nameSpan.Start >= nameSpan.End
            || nameSpan.End > nameSpan.Snapshot.SourceLength)
        {
            throw new ArgumentException("A local declaration requires its nonempty source-backed name token.", nameof(nameSpan));
        }

        if (this.IsDeclared(name))
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
        if (this.owner is not null && this.function is not null)
        {
            SourceSemanticSymbol symbol = new(this.owner, SourceSemanticSymbolKind.Local, local.Index, local.Name, local.Type,
                this.function, null, this.function.Name, DefinitionSpan: nameSpan);
            this.localSymbols.Add(local.Index, symbol);
            this.occurrences.Add(new SourceSemanticOccurrence(nameSpan, symbol, IsDeclaration: true));
            if (typeSpan is SourceSpan typeLocation)
            {
                SourceSemanticSymbol typeSymbol = new(this.owner, SourceSemanticSymbolKind.Type, 0, type.ToString(), type, this.function, null, this.function.Name);
                this.occurrences.Add(new SourceSemanticOccurrence(typeLocation, typeSymbol));
            }
        }

        return true;
    }

    /// <summary>
    /// Returns immutable source-name spans for every successfully declared function local.
    /// </summary>
    /// <returns>A read-only map keyed by the local indices assigned by this context.</returns>
    public IReadOnlyDictionary<int, SourceSpan> GetDeclarationSpans()
        => new ReadOnlyDictionary<int, SourceSpan>(new Dictionary<int, SourceSpan>(this.declarations));

    /// <summary>
    /// Resolves a visible local or enclosing function parameter by semantic name.
    /// </summary>
    /// <param name="name">The identifier being referenced.</param>
    /// <param name="occurrenceSpan">The source token being bound.</param>
    /// <returns>A typed binding reference, or null when no binding is visible.</returns>
    public BoundExpression? Resolve(string name, SourceSpan occurrenceSpan)
    {
        foreach (Dictionary<string, BoundLocal> scope in this.scopes)
        {
            if (scope.TryGetValue(name, out BoundLocal? local))
            {
                if (this.localSymbols.TryGetValue(local.Index, out SourceSemanticSymbol? localSymbol))
                {
                    this.occurrences.Add(new SourceSemanticOccurrence(occurrenceSpan, localSymbol));
                }

                return new BoundLocalReferenceExpression(local);
            }
        }

        BoundParameter? parameter = this.parameters.FirstOrDefault(candidate => StringComparer.Ordinal.Equals(candidate.Name, name));
        if (parameter is not null && this.owner is not null && this.function is not null)
        {
            SourceSemanticSymbol symbol = new(this.owner, SourceSemanticSymbolKind.Parameter, parameter.Index, parameter.Name,
                parameter.Type, this.function, this.documentation, this.function.Name,
                DefinitionSpan: this.owner.Parameters[parameter.Index].NameToken.Span);
            this.occurrences.Add(new SourceSemanticOccurrence(occurrenceSpan, symbol));
        }

        return parameter is null ? null : new BoundParameterReferenceExpression(parameter);
    }

    private bool IsDeclared(string name)
    {
        foreach (Dictionary<string, BoundLocal> scope in this.scopes)
        {
            if (scope.ContainsKey(name))
            {
                return true;
            }
        }

        return this.parameters.Any(parameter => StringComparer.Ordinal.Equals(parameter.Name, name));
    }

    /// <summary>
    /// Records the package callable selected by the compiler at an invocation target.
    /// </summary>
    /// <param name="span">The target identifier's exact source range.</param>
    /// <param name="callable">The unique visible callable selected by binding.</param>
    public void RecordCallableOccurrence(SourceSpan span, PackageCallable callable)
    {
        if (this.owner is null || callable.Declaration is null)
        {
            return;
        }

        SourceSemanticSymbol symbol = new(callable.Declaration, SourceSemanticSymbolKind.Function, 0, callable.Name,
            callable.Function.ReturnType, callable.Function, callable.Documentation, null, CompilerSymbol: callable.Symbol,
            DefinitionSpan: callable.Declaration.Name.Span);
        this.occurrences.Add(new SourceSemanticOccurrence(span, symbol));
    }

    /// <summary>
    /// Records a named argument label resolved to a parameter of the selected callee.
    /// </summary>
    /// <param name="span">The argument label's exact source range.</param>
    /// <param name="callable">The selected callable.</param>
    /// <param name="parameter">The parameter selected by the compiler.</param>
    public void RecordParameterOccurrence(SourceSpan span, PackageCallable callable, BoundParameter parameter)
    {
        if (this.owner is null || callable.Declaration is null)
        {
            return;
        }

        SourceSemanticSymbol symbol = new(callable.Declaration, SourceSemanticSymbolKind.Parameter, parameter.Index,
            parameter.Name, parameter.Type, callable.Function, callable.Documentation, callable.Name, CompilerSymbol: callable.Symbol,
            DefinitionSpan: callable.Declaration.Parameters[parameter.Index].NameToken.Span);
        this.occurrences.Add(new SourceSemanticOccurrence(span, symbol));
    }

    /// <summary>
    /// Records the selected parameter mapping for a source-backed invocation argument.
    /// </summary>
    /// <param name="span">The argument expression's exact source range.</param>
    /// <param name="callable">The uniquely selected callable.</param>
    /// <param name="parameter">The parameter selected by compiler binding.</param>
    /// <param name="isNamedArgument">Whether the source already labels this argument.</param>
    /// <param name="isDirectIdentifierMatch">Whether a direct identifier exactly names the selected parameter.</param>
    public void RecordParameterHint(SourceSpan span, PackageCallable callable, BoundParameter parameter, bool isNamedArgument, bool isDirectIdentifierMatch)
    {
        if (this.owner is null || callable.Declaration is null || span.Start >= span.End)
        {
            return;
        }

        this.parameterHints.Add(new BoundParameterHint(span, callable, parameter, isNamedArgument, isDirectIdentifierMatch));
    }

    /// <summary>
    /// Returns captured argument-to-parameter mappings in binding order.
    /// </summary>
    /// <returns>An immutable snapshot of semantic parameter hints.</returns>
    public IReadOnlyList<BoundParameterHint> GetParameterHints()
        => Array.AsReadOnly(this.parameterHints.ToArray());

    /// <summary>
    /// Returns resolved source occurrences in binding order.
    /// </summary>
    /// <returns>An immutable snapshot of semantic occurrences.</returns>
    public IReadOnlyList<SourceSemanticOccurrence> GetOccurrences()
        => Array.AsReadOnly(this.occurrences.ToArray());
}

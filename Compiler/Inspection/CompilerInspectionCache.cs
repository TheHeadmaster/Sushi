using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Sushi.Diagnostics;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;
using Sushi.Source;

namespace Sushi.Inspection;

/// <summary>
/// Retains a bounded set of successful function representations for one inspection service lifetime.
/// </summary>
internal sealed class CompilerInspectionCache : IDisposable
{
    internal const int DefaultCapacity = 64;

    private readonly object syncRoot = new();
    private readonly int capacity;
    private readonly Dictionary<CompilerInspectionCacheKey, LinkedListNode<CacheEntry>> entries = [];
    private readonly LinkedList<CacheEntry> leastRecentlyUsed = [];
    private bool disposed;

    /// <summary>
    /// Creates a bounded cache, with a smaller capacity available to deterministic tests.
    /// </summary>
    /// <param name="capacity">The maximum number of function input identities to retain.</param>
    internal CompilerInspectionCache(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        this.capacity = capacity;
    }

    /// <summary>
    /// Gets the number of retained function input identities.
    /// </summary>
    internal int Count
    {
        get
        {
            lock (this.syncRoot)
            {
                return this.entries.Count;
            }
        }
    }

    /// <summary>
    /// Builds an exact identity from the configuration, ordered project sources, and selected declaration.
    /// </summary>
    /// <param name="projectPath">The resolved project file identity.</param>
    /// <param name="projectSnapshot">The authoritative configuration bytes.</param>
    /// <param name="sources">The authoritative source snapshots in project discovery order.</param>
    /// <param name="functionSymbol">The selected compiler-generated callable identity.</param>
    /// <param name="declaration">The selected declaration span within its source.</param>
    /// <returns>A collision-safe key whose equality compares all captured input content.</returns>
    internal static CompilerInspectionCacheKey CreateKey(string projectPath, SourceSnapshot projectSnapshot, IReadOnlyList<SourceSnapshot> sources,
        string functionSymbol, SourceSpan declaration)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectPath);
        ArgumentNullException.ThrowIfNull(projectSnapshot);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentException.ThrowIfNullOrEmpty(functionSymbol);

        return new CompilerInspectionCacheKey(projectPath, projectSnapshot, sources, functionSymbol, declaration);
    }

    /// <summary>
    /// Gets a completed entry and promotes it to the most recently used position.
    /// </summary>
    internal bool TryGet(CompilerInspectionCacheKey key, out CompilerInspectionCacheEntry entry)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (this.syncRoot)
        {
            if (this.disposed || !this.entries.TryGetValue(key, out LinkedListNode<CacheEntry>? node))
            {
                entry = null!;
                return false;
            }

            this.leastRecentlyUsed.Remove(node);
            this.leastRecentlyUsed.AddFirst(node);
            entry = node.Value.Products;
            return true;
        }
    }

    /// <summary>
    /// Retains the successfully converted Structured IR and its formatted document atomically.
    /// </summary>
    internal void StoreStructured(CompilerInspectionCacheKey key, IRFunction structuredIR, InspectionDocument document,
        IReadOnlyList<CachedInspectionDiagnostic> bindingDiagnostics)
    {
        ArgumentNullException.ThrowIfNull(structuredIR);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(bindingDiagnostics);
        this.Update(key, products => products with
        {
            StructuredIR = structuredIR,
            StructuredDocument = document,
            BindingDiagnostics = Freeze(bindingDiagnostics)
        });
    }

    /// <summary>
    /// Retains completed Structured and Lowered IR products after lowering and formatting both succeeded.
    /// </summary>
    internal void StoreLowered(CompilerInspectionCacheKey key, IRFunction structuredIR, LoweredFunction loweredIR, InspectionDocument document,
        IReadOnlyList<CachedInspectionDiagnostic> bindingDiagnostics)
    {
        ArgumentNullException.ThrowIfNull(structuredIR);
        ArgumentNullException.ThrowIfNull(loweredIR);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(bindingDiagnostics);
        this.Update(key, products => products with
        {
            StructuredIR = products.StructuredIR ?? structuredIR,
            LoweredIR = loweredIR,
            LoweredDocument = document,
            BindingDiagnostics = products.BindingDiagnostics.Count == 0 ? Freeze(bindingDiagnostics) : products.BindingDiagnostics
        });
    }

    /// <summary>
    /// Adds a formatted Structured IR document produced from a retained Structured IR function.
    /// </summary>
    internal void StoreStructuredDocument(CompilerInspectionCacheKey key, InspectionDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        this.Update(key, products => products with { StructuredDocument = document });
    }

    /// <summary>
    /// Adds a formatted Lowered IR document produced from a retained Lowered IR function.
    /// </summary>
    internal void StoreLoweredDocument(CompilerInspectionCacheKey key, InspectionDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        this.Update(key, products => products with { LoweredDocument = document });
    }

    /// <summary>
    /// Releases all retained compiler objects when the owning service ends.
    /// </summary>
    public void Dispose()
    {
        lock (this.syncRoot)
        {
            this.entries.Clear();
            this.leastRecentlyUsed.Clear();
            this.disposed = true;
        }
    }

    private static IReadOnlyList<CachedInspectionDiagnostic> Freeze(IReadOnlyList<CachedInspectionDiagnostic> diagnostics)
        => Array.AsReadOnly<CachedInspectionDiagnostic>([.. diagnostics]);

    private void Update(CompilerInspectionCacheKey key, Func<CompilerInspectionCacheEntry, CompilerInspectionCacheEntry> update)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (this.syncRoot)
        {
            if (this.disposed)
            {
                return;
            }

            if (this.entries.TryGetValue(key, out LinkedListNode<CacheEntry>? existing))
            {
                existing.Value = existing.Value with { Products = update(existing.Value.Products) };
                this.leastRecentlyUsed.Remove(existing);
                this.leastRecentlyUsed.AddFirst(existing);
            }
            else
            {
                CacheEntry entry = new(key, update(CompilerInspectionCacheEntry.Empty));
                LinkedListNode<CacheEntry> node = this.leastRecentlyUsed.AddFirst(entry);
                this.entries.Add(key, node);
            }

            while (this.entries.Count > this.capacity)
            {
                LinkedListNode<CacheEntry> oldest = this.leastRecentlyUsed.Last!;
                this.entries.Remove(oldest.Value.Key);
                this.leastRecentlyUsed.RemoveLast();
            }
        }
    }

    private sealed record CacheEntry(CompilerInspectionCacheKey Key, CompilerInspectionCacheEntry Products);
}

/// <summary>
/// Represents a compiler request identity and compares exact snapshot bytes after hash-based dictionary lookup.
/// </summary>
internal sealed class CompilerInspectionCacheKey : IEquatable<CompilerInspectionCacheKey>
{
    private readonly string projectPath;
    private readonly SourceSnapshot projectSnapshot;
    private readonly SourceSnapshot[] sources;
    private readonly string[] sourcePaths;
    private readonly string functionSymbol;
    private readonly string declarationPath;
    private readonly int declarationStart;
    private readonly int declarationEnd;
    private readonly int hashCode;

    internal CompilerInspectionCacheKey(string projectPath, SourceSnapshot projectSnapshot, IReadOnlyList<SourceSnapshot> sources,
        string functionSymbol, SourceSpan declaration)
    {
        this.projectPath = NormalizePath(projectPath);
        this.projectSnapshot = projectSnapshot;
        this.sources = [.. sources];
        this.sourcePaths = [.. this.sources.Select(snapshot => NormalizePath(snapshot.Uri.LocalPath))];
        this.functionSymbol = functionSymbol;
        this.declarationPath = NormalizePath(declaration.Snapshot.Uri.LocalPath);
        this.declarationStart = declaration.Start;
        this.declarationEnd = declaration.End;
        this.hashCode = this.ComputeHashCode();
    }

    public bool Equals(CompilerInspectionCacheKey? other)
    {
        if (other is null || !StringComparer.Ordinal.Equals(this.projectPath, other.projectPath)
            || !SnapshotEquals(this.projectSnapshot, other.projectSnapshot)
            || !StringComparer.Ordinal.Equals(this.functionSymbol, other.functionSymbol)
            || !StringComparer.Ordinal.Equals(this.declarationPath, other.declarationPath)
            || this.declarationStart != other.declarationStart || this.declarationEnd != other.declarationEnd
            || this.sources.Length != other.sources.Length)
        {
            return false;
        }

        for (int i = 0; i < this.sources.Length; i++)
        {
            if (!StringComparer.Ordinal.Equals(this.sourcePaths[i], other.sourcePaths[i])
                || !SnapshotEquals(this.sources[i], other.sources[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is CompilerInspectionCacheKey other && this.Equals(other);

    public override int GetHashCode() => this.hashCode;

    private int ComputeHashCode()
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendField(hash, this.projectPath);
        AppendSnapshot(hash, this.projectSnapshot);
        AppendField(hash, this.sources.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
        for (int i = 0; i < this.sources.Length; i++)
        {
            AppendField(hash, this.sourcePaths[i]);
            AppendField(hash, this.sources[i].Bytes.Span);
        }

        AppendField(hash, this.functionSymbol);
        AppendField(hash, this.declarationPath);
        AppendField(hash, this.declarationStart.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendField(hash, this.declarationEnd.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Span<byte> digest = stackalloc byte[32];
        hash.GetHashAndReset(digest);
        return BinaryPrimitives.ReadInt32LittleEndian(digest);
    }

    private static bool SnapshotEquals(SourceSnapshot left, SourceSnapshot right)
        => StringComparer.Ordinal.Equals(NormalizePath(left.Uri.LocalPath), NormalizePath(right.Uri.LocalPath))
            && left.Bytes.Span.SequenceEqual(right.Bytes.Span);

    private static void AppendSnapshot(IncrementalHash hash, SourceSnapshot snapshot)
    {
        AppendField(hash, NormalizePath(snapshot.Uri.LocalPath));
        AppendField(hash, snapshot.Bytes.Span);
    }

    private static void AppendField(IncrementalHash hash, string value)
        => AppendField(hash, Encoding.UTF8.GetBytes(value));

    private static void AppendField(IncrementalHash hash, ReadOnlySpan<byte> value)
    {
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(length, value.Length);
        hash.AppendData(length);
        hash.AppendData(value);
    }

    private static string NormalizePath(string path)
    {
        string fullPath = Path.GetFullPath(path);
        return OperatingSystem.IsWindows() ? fullPath.ToUpperInvariant() : fullPath;
    }
}

/// <summary>
/// Captures binding-independent compiler objects and formatted documents for one exact input identity.
/// </summary>
internal sealed record CompilerInspectionCacheEntry(
    IRFunction? StructuredIR,
    LoweredFunction? LoweredIR,
    InspectionDocument? StructuredDocument,
    InspectionDocument? LoweredDocument,
    IReadOnlyList<CachedInspectionDiagnostic> BindingDiagnostics)
{
    internal static CompilerInspectionCacheEntry Empty { get; } = new(null, null, null, null, Array.Empty<CachedInspectionDiagnostic>());
}

/// <summary>
/// Stores diagnostic data without retaining the source snapshot that originally produced it.
/// </summary>
internal sealed record CachedInspectionDiagnostic(int Code, string Message, DiagnosticSeverity Severity, string SourcePath, int Start, int End);

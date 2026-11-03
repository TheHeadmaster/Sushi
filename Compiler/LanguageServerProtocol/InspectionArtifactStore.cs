using Sushi.Inspection;
using Sushi.Source;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Owns completed inspection results for one language-server session until explicit release or shutdown.
/// Capacity rejection preserves older open documents instead of silently evicting their metadata.
/// </summary>
public sealed class InspectionArtifactStore(int capacity = 32) : IDisposable
{
    private readonly int capacity = capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity));
    private readonly object syncRoot = new();
    private readonly Dictionary<string, InspectionArtifact> artifacts = new(StringComparer.Ordinal);
    private readonly string session = Guid.NewGuid().ToString("N");
    private bool disposed;

    /// <summary>
    /// Gets the number of session-owned live artifacts.
    /// </summary>
    internal int Count
    {
        get
        {
            lock (this.syncRoot)
            {
                return this.artifacts.Count;
            }
        }
    }

    /// <summary>
    /// Retains a successful immutable result with a unique virtual URI and opaque identifier.
    /// </summary>
    internal InspectionArtifact Add(CompilerInspectionResult result, CancellationToken cancellationToken)
    {
        if (!result.Succeeded)
        {
            throw new ArgumentException("Only complete inspection results may be retained.", nameof(result));
        }

        lock (this.syncRoot)
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            if (this.artifacts.Count >= this.capacity)
            {
                throw new InspectionInputException($"Close an inspection document before opening another (session limit: {this.capacity}).");
            }

            string id = Guid.NewGuid().ToString("N");
            string stage = result.Stage switch
            {
                InspectionStage.StructuredIR => "structuredIR",
                InspectionStage.LoweredIR => "loweredIR",
                _ => throw new InvalidOperationException("Unsupported inspection representation.")
            };
            string label = Uri.EscapeDataString(result.FunctionName ?? result.FunctionSymbol!);
            string uri = $"sushi-inspect://{this.session}/{id}/{label}.{stage}";
            InspectionArtifact artifact = new(id, uri, result);
            this.artifacts.Add(id, artifact);
            return artifact;
        }
    }

    /// <summary>
    /// Looks up the original semantic metadata and compilation context for a live document.
    /// </summary>
    internal InspectionArtifact? Find(string id)
    {
        lock (this.syncRoot)
        {
            return this.artifacts.GetValueOrDefault(id);
        }
    }

    /// <summary>
    /// Resolves a caller-supplied document URI only when it still names that exact session artifact.
    /// </summary>
    internal InspectionArtifact? Find(string id, string documentUri)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(documentUri))
        {
            return null;
        }
        if (!Uri.TryCreate(documentUri, UriKind.Absolute, out Uri? requested) || requested.Scheme != "sushi-inspect")
        {
            return null;
        }

        lock (this.syncRoot)
        {
            if (!this.artifacts.TryGetValue(id, out InspectionArtifact? artifact)
                || !Uri.TryCreate(artifact.DocumentUri, UriKind.Absolute, out Uri? retained))
            {
                return null;
            }

            return Uri.Compare(requested, retained, UriComponents.AbsoluteUri, UriFormat.SafeUnescaped, StringComparison.Ordinal) == 0
                ? artifact
                : null;
        }
    }

    /// <summary>
    /// Reuses an artifact only when it belongs to the same captured context, stage, and function identity.
    /// </summary>
    internal InspectionArtifact? FindCompatible(CompilerInspectionContext context, InspectionStage stage, string functionSymbol)
    {
        lock (this.syncRoot)
        {
            return this.artifacts.Values.FirstOrDefault(item => ReferenceEquals(item.Result.Context, context)
                && item.Result.Stage == stage && StringComparer.Ordinal.Equals(item.Result.FunctionSymbol, functionSymbol));
        }
    }

    /// <summary>
    /// Atomically reuses a compatible target or retains the new result once generation has completed.
    /// </summary>
    internal InspectionArtifact AddOrFindCompatible(CompilerInspectionResult result, CancellationToken cancellationToken, out bool added)
    {
        lock (this.syncRoot)
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            InspectionArtifact? existing = this.artifacts.Values.FirstOrDefault(item => ReferenceEquals(item.Result.Context, result.Context)
                && item.Result.Stage == result.Stage && StringComparer.Ordinal.Equals(item.Result.FunctionSymbol, result.FunctionSymbol));
            if (existing is not null)
            {
                added = false;
                return existing;
            }

            added = true;
            return this.Add(result, cancellationToken);
        }
    }

    /// <summary>
    /// Reuses an open artifact only when every captured project/source snapshot, version, stage, and function identity match.
    /// </summary>
    internal InspectionArtifact AddOrFindEquivalentSnapshot(CompilerInspectionResult result, CancellationToken cancellationToken, out bool added)
    {
        lock (this.syncRoot)
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            InspectionArtifact? existing = this.artifacts.Values.FirstOrDefault(item => IsEquivalentInspection(item.Result, result));
            if (existing is not null)
            {
                added = false;
                return existing;
            }

            added = true;
            return this.Add(result, cancellationToken);
        }
    }

    private static bool IsEquivalentInspection(CompilerInspectionResult left, CompilerInspectionResult right)
    {
        if (left.Document is not InspectionDocument leftDocument || right.Document is not InspectionDocument rightDocument
            || left.FunctionSymbol is not string leftFunctionSymbol || right.FunctionSymbol is not string rightFunctionSymbol)
        {
            return false;
        }

        if (left.Stage != right.Stage || !StringComparer.Ordinal.Equals(left.FunctionSymbol, right.FunctionSymbol)
            || !SamePath(left.Context.ProjectPath, right.Context.ProjectPath)
            || !SameSnapshot(left.Context.ProjectSnapshot, right.Context.ProjectSnapshot)
            || left.Context.Sources.Count != right.Context.Sources.Count
            || !left.Context.Functions.TryGetValue(leftFunctionSymbol, out SourceSpan leftDeclaration)
            || !right.Context.Functions.TryGetValue(rightFunctionSymbol, out SourceSpan rightDeclaration)
            || leftDeclaration.Start != rightDeclaration.Start || leftDeclaration.End != rightDeclaration.End
            || !SameSourceIdentity(leftDeclaration.Snapshot, rightDeclaration.Snapshot))
        {
            return false;
        }

        for (int index = 0; index < left.Context.Sources.Count; index++)
        {
            if (!SameSnapshot(left.Context.Sources[index], right.Context.Sources[index]))
            {
                return false;
            }
        }

        return leftDocument.Text == rightDocument.Text && leftDocument.Occurrences.SequenceEqual(rightDocument.Occurrences);
    }

    private static bool SameSnapshot(SourceSnapshot left, SourceSnapshot right)
        => left.Version == right.Version && SameSourceIdentity(left, right)
            && left.Bytes.Span.SequenceEqual(right.Bytes.Span);

    private static bool SameSourceIdentity(SourceSnapshot left, SourceSnapshot right)
        => SamePath(left.Uri.LocalPath, right.Uri.LocalPath);

    private static bool SamePath(string left, string right)
    {
        StringComparer comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        return comparer.Equals(Path.GetFullPath(left), Path.GetFullPath(right));
    }

    /// <summary>
    /// Releases a closed or abandoned artifact. Repeated releases are harmless.
    /// </summary>
    public void Release(string id)
    {
        lock (this.syncRoot)
        {
            this.artifacts.Remove(id);
        }
    }

    /// <summary>
    /// Releases all retained snapshots and semantic metadata when the server session ends.
    /// </summary>
    public void Dispose()
    {
        lock (this.syncRoot)
        {
            this.artifacts.Clear();
            this.disposed = true;
        }
    }
}

/// <summary>
/// Associates one generated URI with the untouched compiler result used by future same-stage navigation.
/// </summary>
internal sealed record InspectionArtifact(string Id, string DocumentUri, CompilerInspectionResult Result);

using Sushi.Inspection;

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
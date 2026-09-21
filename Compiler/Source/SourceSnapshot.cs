using System.Text;

namespace Sushi.Source;

/// <summary>
/// Represents a snapshot of a specific source file.
/// </summary>
public sealed class SourceSnapshot
{    
    private readonly byte[] bytes;
    private readonly int[] lineStarts;

    /// <summary>
    /// The document uri.
    /// </summary>
    public Uri Uri { get; }

    /// <summary>
    /// The version used for concurrency.
    /// </summary>
    public int? Version { get; }

    /// <summary>
    /// The snapshot text.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// The bytes of the source snapshot.
    /// </summary>
    public ReadOnlyMemory<byte> Bytes => this.bytes;

    /// <summary>
    /// The list of line starts in the source snapshot.
    /// </summary>
    public IReadOnlyList<int> LineStarts => this.lineStarts;

    public SourceSnapshot(Uri uri, int? version, string text)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(text);

        this.Uri = uri;
        this.Version = version;
        this.bytes = Encoding.UTF8.GetBytes(text);
        this.Text = text;

        this.lineStarts = BuildLineStarts(this.bytes);
    }

    /// <summary>
    /// Builds the line starts from the source bytes.
    /// </summary>
    /// <param name="bytes">
    /// The source span of the bytes.
    /// </param>
    /// <returns>
    /// An <see cref="int[]"/> containing the line starts.
    /// </returns>
    private static int[] BuildLineStarts(ReadOnlySpan<byte> bytes)
    {
        List<int> starts = [0];

        for (int i = 0; i < bytes.Length; i++)
        {
            switch (bytes[i])
            {
                case (byte)'\r':
                    if (i + 1 < bytes.Length && bytes[i + 1] == (byte)'\n')
                    {
                        i++;
                    }

                    starts.Add(i + 1);
                    break;
                case (byte)'\n':
                    starts.Add(i + 1);
                    break;
            }
        }

        return [.. starts];
    }
}
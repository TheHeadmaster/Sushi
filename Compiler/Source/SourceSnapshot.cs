using System.Buffers;
using System.Text;

namespace Sushi.Source;

/// <summary>
/// Represents a canonical snapshot of a specific source file.
/// </summary>
public sealed class SourceSnapshot
{    
    private readonly byte[] bytes;
    private readonly int[] lineStarts;

    private readonly SourceEncodingIssue[] encodingIssues;

    /// <summary>
    /// The document <see cref="Uri"/>.
    /// </summary>
    public Uri Uri { get; }

    /// <summary>
    /// The version used for concurrency.
    /// </summary>
    public int? Version { get; }

    /// <summary>
    /// A decoded text view of the source.
    ///
    /// For malformed UTF-8, replacement characters may appear here.
    /// <see cref="Bytes"> remains the authoritative source representation.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// The canonical post-BOM source bytes.
    /// </summary>
    public ReadOnlyMemory<byte> Bytes => this.bytes;

    /// <summary>
    /// The length of the canonical source in bytes.
    /// </summary>
    public int SourceLength => this.bytes.Length;

    /// <summary>
    /// The byte offsets at which logical source lines begin.
    /// </summary>
    public IReadOnlyList<int> LineStarts => this.lineStarts;

    /// <summary>
    /// Malformed UTF-8 byte sequences present in the source.
    /// </summary>
    public IReadOnlyList<SourceEncodingIssue> EncodingIssues => this.encodingIssues;

    /// <summary>
    /// Whether the canonical source is valid UTF-8.
    /// </summary>
    public bool IsValidUtf8 => this.encodingIssues.Length == 0;
 
    public SourceSnapshot(Uri uri, int? version, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(bytes);

        this.Uri = uri;
        this.Version = version;
        this.bytes = bytes;

        this.encodingIssues = FindEncodingIssues(this.bytes);

        // This is intentionally a lossy convenience view when malformed
        // UTF-8 exists. The byte array above remains authoritative.
        this.Text = Encoding.UTF8.GetString(this.bytes);

        this.lineStarts = BuildLineStarts(this.bytes);
    }

    /// <summary>
    /// Creates a snapshot from raw UTF-8 source bytes.
    /// </summary>
    /// <param name="uri">
    /// The document <see cref="Uri"/>.
    /// </param>
    /// <param name="version">
    /// The version used for concurrency.
    /// </param>
    /// <param name="source">
    /// The source bytes.
    /// </param>
    /// <returns>
    /// The <see cref="SourceSnapshot"/>.
    /// </returns>
    public static SourceSnapshot FromUtf8(Uri uri, int? version, ReadOnlySpan<byte> source)
    {
        ArgumentNullException.ThrowIfNull(uri);

        source = RemoveBOM(source);

        return new SourceSnapshot(uri, version, source.ToArray());
    }

    /// <summary>
    /// Creates a snapshot from source text supplied by an editor or
    /// another already-decoded text source.
    /// </summary>
    /// <param name="uri">
    /// The document <see cref="Uri"/>.
    /// </param>
    /// <param name="version">
    /// The version used for concurrency.
    /// </param>
    /// <param name="text">
    /// The source text.
    /// </param>
    /// <returns>
    /// The <see cref="SourceSnapshot"/>.
    /// </returns>
    public static SourceSnapshot FromText(Uri uri, int? version, string text)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(text);

        // If the editor surfaced the BOM as U+FEFF, it has the same
        // language-level treatment as the original UTF-8 BOM.
        if (text.Length > 0 && text[0] == '\uFEFF')
        {
            text = text[1..];
        }

        byte[] bytes = Encoding.UTF8.GetBytes(text);

        return new SourceSnapshot(uri, version, bytes);
    }

    /// <summary>
    /// Removes the Byte-Order Mark (BOM) from the source bytes in order to normalize the source.
    /// </summary>
    /// <param name="source">
    /// The source bytes.
    /// </param>
    /// <returns>
    /// The normalized source bytes.
    /// </returns>
    private static ReadOnlySpan<byte> RemoveBOM(ReadOnlySpan<byte> source)
    {
        if (source.Length >= 3 &&
            source[0] == 0xEF &&
            source[1] == 0xBB &&
            source[2] == 0xBF)
        {
            return source[3..];
        }

        return source;
    }

    /// <summary>
    /// Finds all the encoding issues within the source bytes.
    /// </summary>
    /// <param name="source">
    /// The source bytes.
    /// </param>
    /// <returns>
    /// The list of found <see cref="SourceEncodingIssue"/> objects.
    /// </returns>
    private static SourceEncodingIssue[] FindEncodingIssues(ReadOnlySpan<byte> source)
    {
        List<SourceEncodingIssue> issues = [];

        int position = 0;

        while (position < source.Length)
        {
            OperationStatus status = Rune.DecodeFromUtf8(source[position..], out _, out int bytesConsumed);

            if (status == OperationStatus.Done)
            {
                position += bytesConsumed;

                continue;
            }

            // Rune.DecodeFromUtf8 reports how many bytes belong to the malformed or incomplete sequence.
            int length = Math.Max(bytesConsumed, 1);

            issues.Add(new SourceEncodingIssue(position, length));

            position += length;
        }

        return [.. issues];
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

    /// <summary>
    /// Gets the UTF-16 position from a byte offset.
    /// </summary>
    /// <param name="byteOffset">
    /// The byte offset to translate into a UTF-16 position.
    /// </param>
    /// <returns>
    /// The line and character of the UTF-16 position.
    /// </returns>
    public (int Line, int Character) GetUtf16Position(int byteOffset)
    {
        if (byteOffset < 0 || byteOffset > this.bytes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(byteOffset));
        }

        int low = 0;
        int high = this.lineStarts.Length - 1;
        int line = 0;

        while (low <= high)
        {
            int middle = low + ((high - low) / 2);

            if (this.lineStarts[middle] <= byteOffset)
            {
                line = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        int lineStart = this.lineStarts[line];

        ReadOnlySpan<byte> prefix = this.bytes.AsSpan(lineStart, byteOffset - lineStart);

        // Replacement decoding is intentional here.
        // LSP coordinates are UTF-16 coordinates, not byte offsets.
        int character = Encoding.UTF8.GetString(prefix).Length;

        return (line, character);
    }
}
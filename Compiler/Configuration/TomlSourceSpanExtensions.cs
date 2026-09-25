using System.Text;
using Sushi.Source;
using SushiSourceSpan = Sushi.Source.SourceSpan;
using TomlSourceSpan = Tomlyn.Syntax.SourceSpan;

namespace Sushi.Configuration;

public static class TomlSourceSpanExtensions
{
    public static SushiSourceSpan ToSushiSpan(this TomlSourceSpan span, SourceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        int startUtf16 = NormalizeOffset(span.Start.Offset, snapshot.Text.Length);

        int endUtf16 = span.End.Offset < 0
            ? startUtf16
            : Math.Min(span.End.Offset + 1, snapshot.Text.Length);

        if (endUtf16 < startUtf16)
        {
            endUtf16 = startUtf16;
        }

        int startByte = Encoding.UTF8.GetByteCount(snapshot.Text.AsSpan(0, startUtf16));

        int endByte = Encoding.UTF8.GetByteCount(snapshot.Text.AsSpan(0, endUtf16));

        return new SushiSourceSpan(snapshot, startByte, endByte);
    }

    private static int NormalizeOffset(int offset, int textLength)
    {
        if (offset < 0)
        {
            return textLength;
        }

        return Math.Min(offset, textLength);
    }
}
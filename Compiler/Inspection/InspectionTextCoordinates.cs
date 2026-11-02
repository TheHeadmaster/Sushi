namespace Sushi.Inspection;

/// <summary>
/// Converts between generated-text UTF-16 offsets and editor line/character positions.
/// </summary>
public static class InspectionTextCoordinates
{
    /// <summary>
    /// Converts a zero-based UTF-16 line and character to an offset in generated text.
    /// </summary>
    /// <param name="text">The immutable generated representation text.</param>
    /// <param name="line">The zero-based editor line.</param>
    /// <param name="character">The zero-based UTF-16 character within the line.</param>
    /// <returns>The generated-text offset, or null when the position is invalid.</returns>
    public static int? GetOffset(string text, int line, int character)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (line < 0 || character < 0)
        {
            return null;
        }

        int currentLine = 0;
        int lineStart = 0;
        for (int offset = 0; offset < text.Length && currentLine < line; offset++)
        {
            if (text[offset] == '\n')
            {
                currentLine++;
                lineStart = offset + 1;
            }
        }

        if (currentLine != line)
        {
            return null;
        }

        int lineEnd = text.IndexOf('\n', lineStart);
        if (lineEnd < 0)
        {
            lineEnd = text.Length;
        }
        if (lineEnd > lineStart && text[lineEnd - 1] == '\r')
        {
            lineEnd--;
        }

        return character <= lineEnd - lineStart ? lineStart + character : null;
    }

    /// <summary>
    /// Converts a generated-text UTF-16 offset to a zero-based editor position.
    /// </summary>
    /// <param name="text">The immutable generated representation text.</param>
    /// <param name="offset">The zero-based generated-text offset.</param>
    /// <returns>The editor line and UTF-16 character, or null when the offset is invalid.</returns>
    public static (int Line, int Character)? GetPosition(string text, int offset)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (offset < 0 || offset > text.Length)
        {
            return null;
        }

        int line = 0;
        int lineStart = 0;
        for (int index = 0; index < offset; index++)
        {
            if (text[index] == '\n')
            {
                line++;
                lineStart = index + 1;
            }
        }

        int character = offset - lineStart;
        if (character > 0 && text[offset - 1] == '\r')
        {
            character--;
        }
        return (line, character);
    }
}
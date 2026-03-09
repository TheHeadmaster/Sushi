using System.Diagnostics.CodeAnalysis;
using Serilog;

namespace Sushi.Diagnostics;

/// <summary>
/// Represents a compiler warning or error of a specific kind and a helpful message of why and where the message occurred.
/// </summary>
/// <param name="currentLine">
/// The line that the message was reported on.
/// </param>
/// <param name="lineNumber">
/// The line number of the message.
/// </param>
/// <param name="linePosition">
/// The position in the line that the message was reported on.
/// </param>
public abstract class CompilerMessage([NotNull] string currentLine, [NotNull] int lineNumber, [NotNull] int linePosition)
{
    /// <summary>
    /// The message number is a unique identifier that categorizes the type of message.
    /// </summary>
    public abstract int MessageNumber { get; }

    /// <summary>
    /// The line number of the message.
    /// </summary>
    public int LineNumber { get; } = lineNumber;

    /// <summary>
    /// The position in the line that the message was reported on.
    /// </summary>
    public int LinePosition { get; } = linePosition;

    /// <summary>
    /// The line that the message was reported on.
    /// </summary>
    public string CurrentLine { get; } = currentLine;

    /// <summary>
    /// Type type of message.
    /// </summary>
    public abstract CompilerMessageType Type { get; }

    /// <summary>
    /// Gets the span of the message, which determines how long the underline (or pointer) of the message is to indicate to the user what part of the line is errant.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/> that returns the span as an int.
    /// </returns>
    public abstract Task<int> GetMessageSpan();

    /// <summary>
    /// Gets the description of the message.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/> that returns the description as a string.
    /// </returns>
    public abstract Task<string> GetDescription();

    /// <summary>
    /// Gets the message as a string for logging.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task LogMessage()
    {
        int span = await this.GetMessageSpan();

        if (this.Type is CompilerMessageType.Warning)
        {
            Log.Warning("SUSWARN{ID}: {Description}\nat Line {LineNumber} Position {LinePosition}\n{Line}\n{Padding}{Indicator}",
                this.MessageNumber,
                this.GetDescription(),
                this.LineNumber,
                this.LinePosition,
                this.CurrentLine,
                new string(' ', this.LinePosition),
                span > 1 ? new string('~', span) : '^');
        }
        else
        {
            Log.Error("SUSE{ID}: {Description}\nat Line {LineNumber} Position {LinePosition}\n{Line}\n{Padding}{Indicator}",
                this.MessageNumber.ToString("0000"),
                this.GetDescription(),
                this.LineNumber,
                this.LinePosition,
                this.CurrentLine,
                new string(' ', this.LinePosition),
                span > 1 ? new string('~', span) : '^');
        }
    }
}

namespace Sushi.Extensions;

/// <summary>
/// Contains extension methods for <see cref="DateTime"/> objects.
/// </summary>
public static class DateTimeEx
{
    /// <summary>
    /// Returns the time since this <see cref="DateTime"/> as a <see cref="TimeSpan"/>.
    /// </summary>
    /// <param name="dateTime">
    /// The <see cref="DateTime"/> to translate.
    /// </param>
    /// <returns>
    /// The time since the <see cref="DateTime"/>.
    /// </returns>
    public static TimeSpan TimeSince(this DateTime dateTime) => DateTime.Now - dateTime;

    /// <summary>
    /// Returns the time since this <see cref="DateTime"/> as a string.
    /// </summary>
    /// <param name="dateTime">
    /// The <see cref="DateTime"/> to translate.
    /// </param>
    /// <returns>
    /// The time since the <see cref="DateTime"/> as a friendly string.
    /// </returns>
    public static string TimeSinceAsString(this DateTime dateTime)
    {
        TimeSpan timeSince = dateTime.TimeSince();

        if (timeSince.TotalMinutes >= 1)
        {
            return $"{timeSince.Minutes} min, {timeSince.TotalSeconds - (timeSince.Minutes * 60)} s";
        }
        else if (timeSince.TotalSeconds >= 1)
        {
            return $"{timeSince.TotalSeconds} s";
        }
        else
        {
            return $"{timeSince.TotalMilliseconds} ms";
        }
    }
}

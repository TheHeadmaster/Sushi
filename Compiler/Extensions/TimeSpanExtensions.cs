namespace System;

/// <summary>
/// Contains extensions methods for <see cref="TimeSpan"/> objects.
/// </summary>
public static class TimeSpanExtensions
{
    /// <summary>
    /// Returns the <see cref="TimeSpan"/> as a formatted string.
    /// </summary>
    /// <param name="timeSpan">
    /// The <see cref="TiemSpan"/> to translate.
    /// </param>
    /// <returns>
    /// The <see cref="TimeSpan"/> as a formatted string.
    /// </returns>
    public static string AsFormattedString(this TimeSpan timeSpan)
    {
        if (timeSpan.TotalDays >= 1)
        {
            return $"{timeSpan.Days} days, {timeSpan.Hours} hrs, {timeSpan.Minutes} min";
        }
        else if (timeSpan.TotalHours >= 1)
        {
            return $"{timeSpan.Hours} hrs, {timeSpan.Minutes} min";
        }
        else if (timeSpan.TotalMinutes >= 1)
        {
            return $"{timeSpan.Minutes} min, {timeSpan.Seconds} s";
        }
        else if (timeSpan.TotalSeconds >= 1)
        {
            return $"{timeSpan.TotalSeconds:F2} s";
        }
        else
        {
            return $"{timeSpan.TotalMilliseconds:F2} ms";
        }
    }
}
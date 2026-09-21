namespace System;

/// <summary>
/// Contains extensions for comparing paths and <see cref="Uri"/> objects.
/// </summary>
public static class PathExtensions
{
    /// <summary>
    /// Gets whether this path is the same as the specified string path.
    /// </summary>
    /// <param name="path1">
    /// This path.
    /// </param>
    /// <param name="path2">
    /// The string path to check.
    /// </param>
    /// <returns>
    /// True if they're effectively the same path. False otherwise.
    /// </returns>
    public static bool IsSamePath(this string path1, string path2) => Uri.Compare(new Uri(path1), new Uri(path2), ComparableComponents, UriFormat.SafeUnescaped, OSPathComparison) == 0;

    /// <summary>
    /// Gets whether this path is the same as the specified <see cref="Uri"/> path.
    /// </summary>
    /// <param name="path1">
    /// This path.
    /// </param>
    /// <param name="path2">
    /// The <see cref="Uri"/> path to check.
    /// </param>
    /// <returns>
    /// True if they're effectively the same path. False otherwise.
    /// </returns>
    public static bool IsSamePath(this string path1, Uri path2) => Uri.Compare(new Uri(path1), path2, ComparableComponents, UriFormat.SafeUnescaped, OSPathComparison) == 0;

    /// <summary>
    /// Gets whether this path is the same as the specified string path.
    /// </summary>
    /// <param name="path1">
    /// This path.
    /// </param>
    /// <param name="path2">
    /// The string path to check.
    /// </param>
    /// <returns>
    /// True if they're effectively the same path. False otherwise.
    /// </returns>
    public static bool IsSamePath(this Uri path1, string path2) => Uri.Compare(path1, new Uri(path2), ComparableComponents, UriFormat.SafeUnescaped, OSPathComparison) == 0;

    /// <summary>
    /// Gets whether this path is the same as the specified <see cref="Uri"/> path.
    /// </summary>
    /// <param name="path1">
    /// This path.
    /// </param>
    /// <param name="path2">
    /// The <see cref="Uri"/> path to check.
    /// </param>
    /// <returns>
    /// True if they're effectively the same path. False otherwise.
    /// </returns>
    public static bool IsSamePath(this Uri path1, Uri path2) => Uri.Compare(path1, path2, ComparableComponents, UriFormat.SafeUnescaped, OSPathComparison) == 0;

    /// <summary>
    /// The <see cref="StringComparison"/> to use based on which operating system the user is on.
    /// Windows uses a case-insensitive file system, every other supported operating system does not.
    /// </summary>
    private static StringComparison OSPathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>
    /// The comparable components of the <see cref="Uri"/> to be matched for identity.
    /// </summary>
    private const UriComponents ComparableComponents = UriComponents.SchemeAndServer | UriComponents.Path;
}

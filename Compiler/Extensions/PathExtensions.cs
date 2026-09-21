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
    public static bool IsSamePath(this string path1, string path2) => ToFileUri(path1).IsSamePath(ToFileUri(path2));

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
    public static bool IsSamePath(this string path1, Uri path2) => ToFileUri(path1).IsSamePath(path2);

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
    public static bool IsSamePath(this Uri path1, string path2) => path1.IsSamePath(ToFileUri(path2));

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
    /// Converts a path to a full file uri, fully resolving relative paths.
    /// </summary>
    /// <param name="path">
    /// The path to convert.
    /// </param>
    /// <returns>
    /// The <see cref="Uri"/> representation of the path string.
    /// </returns>
    private static Uri ToFileUri(string path) => new(Path.GetFullPath(path));

    /// <summary>
    /// The <see cref="StringComparison"/> to use based on which operating system the user is on.
    /// Uses the conventional path comparison for the current operating system.
    /// </summary>
    private static StringComparison OSPathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>
    /// The comparable components of the <see cref="Uri"/> to be matched for identity.
    /// </summary>
    private const UriComponents ComparableComponents = UriComponents.SchemeAndServer | UriComponents.Path;
}

using Serilog.Sinks.SystemConsole.Themes;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Sushi;

/// <summary>
/// Contains application-level metadata.
/// </summary>
public static partial class AppMeta
{
    /// <summary>
    /// Gets the assembly version as a string.
    /// </summary>
    /// <returns></returns>
    public static Task<string> GetVersion()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();

        AssemblyInformationalVersionAttribute? informationalVersionAttribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        if (informationalVersionAttribute is null)
        {
            return Task.FromResult(string.Empty);
        }

        string productVersion = informationalVersionAttribute.InformationalVersion;

        Regex regex = VersionRegex();

        Match match = regex.Match(productVersion);

        return !match.Success ? Task.FromResult(string.Empty) : Task.FromResult(match.Groups[1].Value);
    }

    /// <summary>
    /// Gets the directory of the executing assembly.
    /// </summary>
    /// <returns>
    /// The assembly directory as a string.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Throws if the executing assembly is null.
    /// </exception>
    public static string GetAssemblyDirectory() => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? throw new InvalidOperationException("Assembly directory is null");

    /// <summary>
    /// Options for how the compiler should behave, translated from the command line arguments.
    /// </summary>
    public static CompilerOptions Options { get; set; } = null!;

    /// <summary>
    /// Whether the program is running in debug mode or not.
    /// </summary>
    public static bool IsDebug =>
#if DEBUG
        true;
#else
        return false;
#endif

    /// <summary>
    /// Matches a semantic version with or without a suffix.
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex("([^+]+)(?:\\+[0-9a-zA-Z]+)?")]
    private static partial Regex VersionRegex();

    /// <summary>
    /// The console theme used for logging to the console and changes the colors associated with various tokens of text.
    /// </summary>
    public static AnsiConsoleTheme ConsoleTheme { get; } = new(
    new Dictionary<ConsoleThemeStyle, string>
    {
        [ConsoleThemeStyle.Text] = "\x1b[38;5;0229m",
        [ConsoleThemeStyle.SecondaryText] = "\x1b[38;5;0246m",
        [ConsoleThemeStyle.TertiaryText] = "\x1b[38;5;0242m",
        [ConsoleThemeStyle.Invalid] = "\x1b[33;1m",
        [ConsoleThemeStyle.Null] = "\x1b[38;5;0038m",
        [ConsoleThemeStyle.Name] = "\x1b[38;5;0081m",
        [ConsoleThemeStyle.String] = "\x1b[38;5;0216m",
        [ConsoleThemeStyle.Number] = "\x1b[38;5;151m",
        [ConsoleThemeStyle.Boolean] = "\x1b[38;5;0038m",
        [ConsoleThemeStyle.Scalar] = "\x1b[38;5;0079m",
        [ConsoleThemeStyle.LevelVerbose] = "\x1b[197m",
        [ConsoleThemeStyle.LevelDebug] = "\x1b[089m",
        [ConsoleThemeStyle.LevelInformation] = "\x1b[37;163m",
        [ConsoleThemeStyle.LevelWarning] = "\x1b[38;5;0226m",
        [ConsoleThemeStyle.LevelError] = "\x1b[38;5;0160m",
        [ConsoleThemeStyle.LevelFatal] = "\x1b[38;5;0124m"
    });
}

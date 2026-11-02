using Microsoft.Extensions.FileSystemGlobbing;
using Sushi.Analysis;
using Sushi.Source;

namespace Sushi.Compilation;

/// <summary>
/// Shares project path resolution and configured source discovery between compiler entry points.
/// </summary>
internal static class ProjectSourceLoader
{
    /// <summary>
    /// Resolves an explicit project or the single project in a directory using existing compilation rules.
    /// </summary>
    internal static string ResolveProjectPath(string projectOrFolderPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string path = Path.GetFullPath(projectOrFolderPath);

        if (File.Exists(path))
        {
            string extension = Path.GetExtension(path);

            if (extension.Equals(".susproj", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            if (extension.Equals(".susln", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException("Solution compilation is not supported by the executable MVP. Compile a .susproj directly.");
            }

            throw new ArgumentException($"\"{path}\" is not a Sushi project file.", nameof(projectOrFolderPath));
        }

        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Project directory \"{path}\" does not exist.");
        }

        string[] projects = [.. Directory
            .EnumerateFiles(path, "*.susproj", SearchOption.TopDirectoryOnly)
            .OrderBy(project => project, StringComparer.Ordinal)];

        cancellationToken.ThrowIfCancellationRequested();
        return projects.Length switch
        {
            1 => projects[0],
            0 => throw new InvalidOperationException($"Directory \"{path}\" does not contain a Sushi project file."),
            _ => throw new InvalidOperationException($"Directory \"{path}\" contains multiple Sushi project files. Specify the project to compile explicitly.")
        };
    }

    /// <summary>
    /// Applies configured include and exclude patterns and preserves deterministic source ordering.
    /// </summary>
    internal static IReadOnlyList<string> DiscoverSourcePaths(string projectDirectory, ProjectDefinition project, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        Matcher matcher = new(comparison);

        foreach (string pattern in project.Build.Sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            matcher.AddInclude(pattern);
        }

        foreach (string pattern in project.Source.Exclude)
        {
            cancellationToken.ThrowIfCancellationRequested();
            matcher.AddExclude(pattern);
        }

        string[] paths =
        [
            .. matcher
                .GetResultsInFullPath(projectDirectory)
                .Where(path => Path.GetExtension(path).Equals(".sus", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal)
        ];
        cancellationToken.ThrowIfCancellationRequested();
        return paths;
    }

    /// <summary>
    /// Loads canonical UTF-8 bytes without changing their source identity.
    /// </summary>
    internal static async Task<SourceSnapshot> LoadSnapshot(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string fullPath = Path.GetFullPath(path);
        byte[] bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken);

        SourceSnapshot snapshot = SourceSnapshot.FromUtf8(new Uri(fullPath), version: null, bytes);
        cancellationToken.ThrowIfCancellationRequested();
        return snapshot;
    }
}
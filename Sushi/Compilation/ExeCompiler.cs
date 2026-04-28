using System.Text;

namespace Sushi.Compilation;

/// <summary>
/// Handles compilation of intermediate files to an executable exe or dll file.
/// </summary>
public static class ExeCompiler
{
    /// <summary>
    /// Compiles an exe or dll file.
    /// </summary>
    /// <param name="exeOrDllName">
    /// The name of the exe or dll file without the extension.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public static async Task Compile(string exeOrDllName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(AppMeta.Options.ProjectPath);

        string gccPath = ExeHelper.GetFilePathFromEnvPath("gcc");

        DirectoryInfo binFolder = new(Path.Combine(AppMeta.Options.ProjectPath, "bin"));
        DirectoryInfo intermediateFolder = new(Path.Combine(AppMeta.Options.ProjectPath, "intermediate"));

        if (Directory.Exists(binFolder.FullName))
        {
            Directory.Delete(binFolder.FullName, true);
        }

        Directory.CreateDirectory(binFolder.FullName);

        List<string> directories = [];

        foreach (FileInfo path in intermediateFolder.EnumerateFiles())
        {
            string directory = Path.GetRelativePath(intermediateFolder.FullName, path.DirectoryName ?? string.Empty);

            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            if (!directories.Contains(directory))
            {
                directories.Add(directory);
            }
        }

        StringBuilder sb = new();

        foreach (string directory in directories)
        {
            sb.Append($"-g intermediate/{directory}/*.c");
            sb.Append(' ');
            sb.Append($"-g intermediate/{directory}/*.h");
        }

        sb.Append($" -o bin/{exeOrDllName}.exe");

        await ExeHelper.RunExecutableAndOutputToConsole(gccPath, sb.ToString().Trim());
    }
}

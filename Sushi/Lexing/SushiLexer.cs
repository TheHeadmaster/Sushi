using System.Diagnostics.CodeAnalysis;

namespace Sushi.Lexing;

/// <summary>
/// Handles lexing of the files in the project.
/// </summary>
public static class SushiLexer
{
    /// <summary>
    /// Initializes the lexer by loading the files from disk and turning them into raw <see cref="SourceFile"/> objects.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/> that returns an <see cref="IEnumerable{T}"/> of <see cref="SourceFile"/> objects.
    /// </returns>
    public static async Task<IEnumerable<SourceFile>> Initialize()
    {
        List<Task<SourceFile>> fileTasks = [];

        foreach (string file in Directory.EnumerateFiles(AppMeta.Options.ProjectPath, "*.sus", SearchOption.AllDirectories))
        {
            fileTasks.Add(Task.Run(() => LoadFile(file)));
        }

        Task.WaitAll(fileTasks);

        return fileTasks.Select(x => x.Result);
    }

    /// <summary>
    /// Loads the specified file from disk and translates it into a raw <see cref="SourceFile"/>.
    /// </summary>
    /// <param name="file">
    /// The path of the file.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/> that returns the raw <see cref="SourceFile"/> object.
    /// </returns>
    private static async Task<SourceFile> LoadFile([NotNull] string file)
    {
        string source = await File.ReadAllTextAsync(file);

        return new() { FileName = Path.GetFileName(file), FilePath = file, Source = source };
    }

    /// <summary>
    /// Lexes all of the files in the project in parallel.
    /// </summary>
    /// <param name="files">
    /// The files to lex.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public static Task LexFiles([NotNull] IEnumerable<SourceFile> files)
    {
        List<Task> fileTasks = [];

        foreach (SourceFile file in files)
        {
            fileTasks.Add(Task.Run(() => LexFile(file)));
        }

        Task.WaitAll(fileTasks);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Lexes a <see cref="SourceFile"/> into a list of tokens.
    /// </summary>
    /// <param name="file">
    /// The file to lex.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    private static async Task LexFile([NotNull] SourceFile file)
    {

    }
}

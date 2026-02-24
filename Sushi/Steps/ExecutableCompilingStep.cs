using System.Diagnostics.CodeAnalysis;

namespace Sushi.Steps;

public class ExecutableCompilingStep : ICompilerStep
{
    private static string gccPath;

    private static DirectoryInfo binFolder = null!;
    private static DirectoryInfo objFolder = null!;

    public int StepNumber => 4;

    public Task Initialize([NotNull] CompileJob job)
    {
        gccPath = ExeHelper.GetFilePathFromEnvPath("gcc");

        ArgumentException.ThrowIfNullOrWhiteSpace(AppMeta.Options.ProjectPath);

        binFolder = new DirectoryInfo(Path.Combine(AppMeta.Options.ProjectPath, "bin"));
        objFolder = new DirectoryInfo(Path.Combine(AppMeta.Options.ProjectPath, "obj"));

        if (Directory.Exists(binFolder.FullName))
        {
            Directory.Delete(binFolder.FullName, true);
        }

        if (Directory.Exists(objFolder.FullName))
        {
            Directory.Delete(objFolder.FullName, true);
        }

        Directory.CreateDirectory(binFolder.FullName);
        Directory.CreateDirectory(objFolder.FullName);

        return Task.CompletedTask;
    }
    public async Task Run([NotNull] CompileJob job) => await ExeHelper.RunExecutableAndOutputToConsole(gccPath, "-g intermediate/*.c -o bin/Test.exe");
}

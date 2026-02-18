namespace Sushi.Compilation;

public static class ASMCompiler
{
    private static string gccPath;
    private static string nasmPath;

    private static DirectoryInfo binFolder = null!;
    private static DirectoryInfo objFolder = null!;

    public static async Task Initialize()
    {
        nasmPath = ExeHelper.GetFilePathFromEnvPath("nasm");
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
    }

    public static async Task Compile()
    {
        await ExeHelper.RunExecutableAndOutputToConsole(nasmPath, "-fwin64 Test.asm -o obj/Test.obj");
        await ExeHelper.RunExecutableAndOutputToConsole(gccPath, "-g obj/*.obj -o bin/Test.exe");
    }
}

using System.Reflection;
using System.Runtime.InteropServices;

namespace Sushi.Backends.Native;

/// <summary>
/// Selects the pinned Windows linker without allowing machine-wide tools to affect native builds.
/// </summary>
internal static class NativeToolchainResolver
{
    public const string OverrideEnvironmentVariable = "SUSHI_NATIVE_TOOLCHAIN_ROOT";
    private const string BundledToolchainRelativePath = "toolchains/win-x64/llvm-mingw";
    private const string DevelopmentRootMetadataKey = "SushiNativeToolchainDevelopmentRoot";
    private const string VerificationMarkerFileName = ".sushi-llvm-mingw.json";

    /// <summary>
    /// Resolves the native linker for the current host and process architecture.
    /// </summary>
    /// <returns>The selected linker executable and its verified toolchain root, when applicable.</returns>
    public static ResolvedNativeToolchain ResolveForCurrentHost()
        => Resolve(
            OperatingSystem.IsWindows(),
            RuntimeInformation.ProcessArchitecture,
            Environment.GetEnvironmentVariable(OverrideEnvironmentVariable),
            AppContext.BaseDirectory,
            GetDevelopmentToolchainRoot());

    internal static ResolvedNativeToolchain Resolve(
        bool isWindows,
        Architecture processArchitecture,
        string? explicitRoot,
        string applicationBaseDirectory,
        string? developmentToolchainRoot = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationBaseDirectory);

        if (!isWindows)
        {
            return new ResolvedNativeToolchain("clang", null, IsBundled: false);
        }

        if (processArchitecture is not Architecture.X64)
        {
            throw new PlatformNotSupportedException(
                $"Bundled LLVM-MinGW support currently targets Windows x64. The current process architecture is {processArchitecture}.");
        }

        string root;
        bool isOverride = !string.IsNullOrWhiteSpace(explicitRoot);
        if (isOverride)
        {
            root = Path.GetFullPath(explicitRoot!);
        }
        else
        {
            string bundledRoot = Path.GetFullPath(Path.Combine(applicationBaseDirectory, BundledToolchainRelativePath));
            if (Directory.Exists(bundledRoot))
            {
                root = bundledRoot;
            }
            else if (!string.IsNullOrWhiteSpace(developmentToolchainRoot))
            {
                root = Path.GetFullPath(developmentToolchainRoot);
            }
            else
            {
                root = bundledRoot;
            }
        }

        ValidateToolchainRoot(root, isOverride);
        return new ResolvedNativeToolchain(Path.Combine(root, "bin", "clang.exe"), root, IsBundled: true);
    }

    private static string? GetDevelopmentToolchainRoot()
    {
        AssemblyMetadataAttribute? metadata = typeof(NativeToolchainResolver).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, DevelopmentRootMetadataKey, StringComparison.Ordinal));

        return metadata?.Value;
    }

    internal static void ValidateToolchainRoot(string root, bool isOverride)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        string fullRoot = Path.GetFullPath(root);
        string[] requiredFiles =
        [
            Path.Combine("bin", "clang.exe"),
            Path.Combine("bin", "ld.lld.exe"),
            Path.Combine("x86_64-w64-mingw32", "lib", "crt2.o"),
            Path.Combine("x86_64-w64-mingw32", "lib", "crtbegin.o"),
            Path.Combine("x86_64-w64-mingw32", "lib", "crtend.o"),
            Path.Combine("x86_64-w64-mingw32", "lib", "libmingw32.a"),
            VerificationMarkerFileName
        ];

        List<string> missing = requiredFiles
            .Select(path => Path.Combine(fullRoot, path))
            .Where(path => !File.Exists(path))
            .ToList();

        string compilerRuntimeDirectory = Path.Combine(fullRoot, "lib", "clang");
        bool hasCompilerRuntime = Directory.Exists(compilerRuntimeDirectory)
            && Directory.EnumerateFiles(compilerRuntimeDirectory, "libclang_rt.builtins-x86_64.a", SearchOption.AllDirectories).Any();
        if (!hasCompilerRuntime)
        {
            missing.Add(Path.Combine(compilerRuntimeDirectory, "<version>", "lib", "windows", "libclang_rt.builtins-x86_64.a"));
        }

        if (!Directory.Exists(Path.Combine(fullRoot, "include"))
            && !Directory.Exists(Path.Combine(fullRoot, "x86_64-w64-mingw32", "include")))
        {
            missing.Add(Path.Combine(fullRoot, "include"));
        }

        if (missing.Count > 0)
        {
            string source = isOverride
                ? $"The toolchain selected by {OverrideEnvironmentVariable} is incomplete at {fullRoot}."
                : $"The pinned LLVM-MinGW toolchain is missing or incomplete at {fullRoot}. Run tools/RestoreVendoredLlvmMingw.ps1 or dotnet build to reconstruct it from the checked-in chunks.";

            throw new InvalidOperationException($"{source} Missing required toolchain files: {string.Join(", ", missing)}.");
        }
    }
}

/// <summary>
/// Identifies the selected native linker driver.
/// </summary>
/// <param name="ExecutablePath">The absolute bundled driver path or non-Windows PATH command.</param>
/// <param name="RootPath">The complete verified toolchain root, when using LLVM-MinGW.</param>
/// <param name="IsBundled">Whether the selected driver comes from the pinned toolchain.</param>
internal sealed record ResolvedNativeToolchain(string ExecutablePath, string? RootPath, bool IsBundled);

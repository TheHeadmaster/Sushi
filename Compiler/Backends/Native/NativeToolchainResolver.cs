using System.Reflection;
using System.Runtime.InteropServices;

namespace Sushi.Backends.Native;

/// <summary>
/// Resolves the Windows toolchain from an explicit override, published distribution, or restored NuGet package; non-Windows hosts retain PATH lookup.
/// </summary>
internal static class NativeToolchainResolver
{
    public const string OverrideEnvironmentVariable = "SUSHI_NATIVE_TOOLCHAIN_ROOT";
    private const string BundledToolchainRelativePath = "toolchains/win-x64/llvm-mingw";
    private const string PackageRootMetadataKey = "SushiNativeToolchainPackageRoot";

    /// <summary>
    /// Resolves a native linker executable for the current process.
    /// </summary>
    /// <returns>The selected executable and its toolchain root when bundled.</returns>
    public static ResolvedNativeToolchain ResolveForCurrentHost()
        => Resolve(
            OperatingSystem.IsWindows(),
            RuntimeInformation.ProcessArchitecture,
            Environment.GetEnvironmentVariable(OverrideEnvironmentVariable),
            AppContext.BaseDirectory,
            GetRestoredPackageToolchainRoot());

    internal static ResolvedNativeToolchain Resolve(
        bool isWindows,
        Architecture processArchitecture,
        string? explicitRoot,
        string applicationBaseDirectory,
        string? restoredPackageRoot = null)
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

        bool isOverride = !string.IsNullOrWhiteSpace(explicitRoot);
        string bundledRoot = Path.GetFullPath(Path.Combine(applicationBaseDirectory, BundledToolchainRelativePath));
        string selectedRoot = isOverride
            ? explicitRoot!
            : Directory.Exists(bundledRoot) || string.IsNullOrWhiteSpace(restoredPackageRoot)
                ? bundledRoot
                : restoredPackageRoot;
        string root = Path.GetFullPath(selectedRoot);

        ValidateToolchainRoot(root, isOverride);

        return new ResolvedNativeToolchain(Path.Combine(root, "bin", "clang.exe"), root, IsBundled: true);
    }

    private static string? GetRestoredPackageToolchainRoot()
    {
        AssemblyMetadataAttribute? metadata = typeof(NativeToolchainResolver).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, PackageRootMetadataKey, StringComparison.Ordinal));

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
            Path.Combine("x86_64-w64-mingw32", "lib", "libmingw32.a")
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
                : $"The restored or bundled LLVM-MinGW toolchain is missing or incomplete at {fullRoot}. Run dotnet restore to restore the pinned native toolchain package, or republish Sushi.";

            throw new InvalidOperationException(
                $"{source} Missing required toolchain files: {string.Join(", ", missing)}.");
        }
    }
}

/// <summary>
/// Identifies a resolved native toolchain driver.
/// </summary>
/// <param name="ExecutablePath">The compiler driver path or a non-Windows PATH command.</param>
/// <param name="RootPath">The complete toolchain root, when using LLVM-MinGW.</param>
/// <param name="IsBundled">Whether the driver is pinned and selected from a complete toolchain root.</param>
internal sealed record ResolvedNativeToolchain(string ExecutablePath, string? RootPath, bool IsBundled);

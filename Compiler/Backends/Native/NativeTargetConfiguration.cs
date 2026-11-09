using System.Runtime.InteropServices;

namespace Sushi.Backends.Native;

/// <summary>
/// Keeps the native object-emission and linker target decisions consistent.
/// </summary>
internal static class NativeTargetConfiguration
{
    /// <summary>
    /// Gets the only Windows target currently supported by the bundled native toolchain.
    /// </summary>
    public const string WindowsX64GnuTriple = "x86_64-w64-windows-gnu";

    /// <summary>
    /// Selects the target triple for this compiler process.
    /// </summary>
    /// <param name="hostTriple">LLVM's default host triple for non-Windows platforms.</param>
    /// <returns>The target triple shared by object emission and linking.</returns>
    public static string SelectTargetTriple(string hostTriple)
        => SelectTargetTriple(hostTriple, OperatingSystem.IsWindows(), RuntimeInformation.ProcessArchitecture);

    internal static string SelectTargetTriple(string hostTriple, bool isWindows, Architecture processArchitecture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostTriple);

        if (!isWindows)
        {
            return hostTriple;
        }

        if (processArchitecture is not Architecture.X64)
        {
            throw new PlatformNotSupportedException(
                $"Bundled native compilation currently supports Windows x64 only. The current process architecture is {processArchitecture}.");
        }

        return WindowsX64GnuTriple;
    }

    internal static void ValidateArtifactTarget(string targetTriple, bool isWindows, Architecture processArchitecture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetTriple);

        if (!isWindows)
        {
            return;
        }

        string expectedTriple = SelectTargetTriple("unsupported-host-triple", isWindows, processArchitecture);
        if (!StringComparer.Ordinal.Equals(targetTriple, expectedTriple))
        {
            throw new InvalidOperationException(
                $"The native object target {targetTriple} does not match the configured linker target {expectedTriple}.");
        }
    }
}

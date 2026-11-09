using System.Runtime.InteropServices;
using FluentAssertions;
using NUnit.Framework;
using Sushi.Backends.Native;

namespace Sushi.Testing.Backends.Native;

/// <summary>
/// Verifies deterministic target and LLVM-MinGW selection without launching native tools.
/// </summary>
[TestFixture]
public sealed class NativeToolchainResolverTests
{
    [Test]
    public void WindowsX64ShouldSelectTheWindowsGnuTargetTriple()
    {
        NativeTargetConfiguration.SelectTargetTriple("x86_64-pc-windows-msvc", true, Architecture.X64)
            .Should()
            .Be(NativeTargetConfiguration.WindowsX64GnuTriple);
    }

    [Test]
    public void WindowsArm64ShouldRejectTheUnsupportedTargetArchitecture()
    {
        Action select = () => NativeTargetConfiguration.SelectTargetTriple("aarch64-pc-windows-msvc", true, Architecture.Arm64);

        select.Should()
            .Throw<PlatformNotSupportedException>()
            .WithMessage("*Windows x64 only*");
    }

    [Test]
    public void LinkerShouldRejectAnObjectForAnotherWindowsTarget()
    {
        Action validate = () => NativeTargetConfiguration.ValidateArtifactTarget("x86_64-pc-windows-msvc", true, Architecture.X64);

        validate.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*does not match*");
    }

    [Test]
    public void NonWindowsShouldRetainTheExistingHostTripleAndPathCommand()
    {
        NativeTargetConfiguration.SelectTargetTriple("x86_64-unknown-linux-gnu", false, Architecture.X64)
            .Should()
            .Be("x86_64-unknown-linux-gnu");

        NativeToolchainResolver.Resolve(false, Architecture.X64, null, Path.GetTempPath())
            .Should()
            .Be(new ResolvedNativeToolchain("clang", null, IsBundled: false));
    }

    [Test]
    public void BundledWindowsToolchainShouldBeSelectedWhenAvailable()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            string appBase = Path.Combine(directory, "Sushi 雪");
            string expectedRoot = Path.Combine(appBase, "toolchains", "win-x64", "llvm-mingw");
            CreateCompleteToolchain(expectedRoot);

            ResolvedNativeToolchain toolchain = NativeToolchainResolver.Resolve(true, Architecture.X64, null, appBase);

            toolchain.ExecutablePath.Should().Be(Path.GetFullPath(Path.Combine(expectedRoot, "bin", "clang.exe")));
            toolchain.RootPath.Should().Be(Path.GetFullPath(expectedRoot));
            toolchain.IsBundled.Should().BeTrue();
            Path.IsPathFullyQualified(toolchain.ExecutablePath).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void RestoredNuGetToolchainShouldBeSelectedWhenTheDistributionIsNotBundled()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            string packageRoot = Path.Combine(directory, "nuget cache", "toolchain");
            CreateCompleteToolchain(packageRoot);

            ResolvedNativeToolchain toolchain = NativeToolchainResolver.Resolve(true, Architecture.X64, null, Path.Combine(directory, "Sushi"), packageRoot);

            toolchain.RootPath.Should().Be(Path.GetFullPath(packageRoot));
            toolchain.ExecutablePath.Should().Be(Path.GetFullPath(Path.Combine(packageRoot, "bin", "clang.exe")));
            toolchain.IsBundled.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void ExplicitToolchainOverrideShouldTakePrecedenceOverBundledToolchain()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            string appBase = Path.Combine(directory, "Sushi");
            string bundledRoot = Path.Combine(appBase, "toolchains", "win-x64", "llvm-mingw");
            string overrideRoot = Path.Combine(directory, "Custom Toolchain ü");
            CreateCompleteToolchain(bundledRoot);
            CreateCompleteToolchain(overrideRoot);

            ResolvedNativeToolchain toolchain = NativeToolchainResolver.Resolve(true, Architecture.X64, overrideRoot, appBase);

            toolchain.RootPath.Should().Be(Path.GetFullPath(overrideRoot));
            toolchain.ExecutablePath.Should().Be(Path.GetFullPath(Path.Combine(overrideRoot, "bin", "clang.exe")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void InvalidOverrideShouldFailInsteadOfFallingBackToTheBundledToolchain()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            string appBase = Path.Combine(directory, "Sushi");
            CreateCompleteToolchain(Path.Combine(appBase, "toolchains", "win-x64", "llvm-mingw"));
            string invalidOverride = Path.Combine(directory, "missing override");

            Action resolve = () => NativeToolchainResolver.Resolve(true, Architecture.X64, invalidOverride, appBase);

            resolve.Should()
                .Throw<InvalidOperationException>()
                .WithMessage($"*{NativeToolchainResolver.OverrideEnvironmentVariable}*");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void MissingToolchainShouldExplainHowNuGetRestoreProvidesIt()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            Action resolve = () => NativeToolchainResolver.Resolve(true, Architecture.X64, null, directory);

            resolve.Should()
                .Throw<InvalidOperationException>()
                .WithMessage("*dotnet restore*");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void IncompleteToolchainShouldNameMissingStartupAndRuntimeFiles()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(Path.Combine(directory, "bin"));
            File.WriteAllText(Path.Combine(directory, "bin", "clang.exe"), string.Empty);

            Action validate = () => NativeToolchainResolver.ValidateToolchainRoot(directory, isOverride: true);

            InvalidOperationException exception = validate.Should()
                .Throw<InvalidOperationException>()
                .Which;
            exception.Message.Should().Contain("crt2.o");
            exception.Message.Should().Contain("libclang_rt.builtins-x86_64.a");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void WindowsArm64ShouldRejectToolchainResolution()
    {
        Action resolve = () => NativeToolchainResolver.Resolve(true, Architecture.Arm64, null, Path.GetTempPath());

        resolve.Should()
            .Throw<PlatformNotSupportedException>()
            .WithMessage("*Windows x64*");
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "Native Toolchain Resolver " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void CreateCompleteToolchain(string root)
    {
        string[] files =
        [
            "bin/clang.exe",
            "bin/ld.lld.exe",
            "x86_64-w64-mingw32/lib/crt2.o",
            "x86_64-w64-mingw32/lib/crtbegin.o",
            "x86_64-w64-mingw32/lib/crtend.o",
            "x86_64-w64-mingw32/lib/libmingw32.a",
            "lib/clang/22/lib/windows/libclang_rt.builtins-x86_64.a",
            "include/stdio.h"
        ];

        foreach (string path in files)
        {
            string fullPath = Path.Combine(root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, string.Empty);
        }
    }
}

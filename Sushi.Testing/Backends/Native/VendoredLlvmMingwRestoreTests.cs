using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using NUnit.Framework;
using Sushi.Backends.Native;

namespace Sushi.Testing.Backends.Native;

/// <summary>
/// Verifies that a clean checkout can reconstruct LLVM-MinGW solely from its committed chunks.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class VendoredLlvmMingwRestoreTests
{
    [Test]
    public async Task CleanDestinationShouldReconstructAndRepeatablyRestoreTheCommittedToolchain()
    {
        EnsureWindows();

        string repositoryRoot = FindRepositoryRoot();
        string scriptPath = Path.Combine(repositoryRoot, "tools", "RestoreVendoredLlvmMingw.ps1");
        string destinationRoot = Path.Combine(repositoryRoot, ".tools", "llvm-mingw");
        if (Directory.Exists(destinationRoot))
        {
            Directory.Delete(destinationRoot, recursive: true);
        }

        try
        {
            Directory.Exists(destinationRoot).Should().BeFalse();
            (await RunRestore(scriptPath, destinationRoot)).Should().Contain("Installed verified LLVM-MinGW");
            NativeToolchainResolver.ValidateToolchainRoot(destinationRoot, isOverride: true);

            (await RunRestore(scriptPath, destinationRoot)).Should().Contain("already exists");

            Directory.Delete(destinationRoot, recursive: true);
            (await RunRestore(scriptPath, destinationRoot)).Should().Contain("Installed verified LLVM-MinGW");
            NativeToolchainResolver.ValidateToolchainRoot(destinationRoot, isOverride: true);
        }
        finally
        {
            if (!Directory.Exists(destinationRoot))
            {
                await RunRestore(scriptPath, destinationRoot);
            }
        }
    }

    [Test]
    public async Task MissingAndCorruptedChunksShouldBeRejectedBeforeInstallation()
    {
        EnsureWindows();

        string repositoryRoot = FindRepositoryRoot();
        string scriptPath = Path.Combine(repositoryRoot, "tools", "RestoreVendoredLlvmMingw.ps1");
        string temporaryRoot = Path.Combine(Path.GetTempPath(), "Sushi Toolchain Chunk Validation 雪 " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryRoot);

        try
        {
            RestoreFixture missing = CreateFixture(Path.Combine(temporaryRoot, "missing"));
            File.Delete(Path.Combine(missing.ChunkDirectory, "archive.part000"));
            Func<Task> restoreMissing = async () => { await RunRestore(scriptPath, missing.DestinationRoot, missing.PinnedManifestPath, missing.ChunkDirectory); };
            await restoreMissing.Should()
                .ThrowAsync<InvalidOperationException>()
                .WithMessage("*chunk count mismatch*");
            Directory.Exists(missing.DestinationRoot).Should().BeFalse();

            RestoreFixture corrupted = CreateFixture(Path.Combine(temporaryRoot, "corrupted"));
            byte[] bytes = await File.ReadAllBytesAsync(Path.Combine(corrupted.ChunkDirectory, "archive.part000"));
            bytes[0] ^= 0xFF;
            await File.WriteAllBytesAsync(Path.Combine(corrupted.ChunkDirectory, "archive.part000"), bytes);
            Func<Task> restoreCorrupted = async () => { await RunRestore(scriptPath, corrupted.DestinationRoot, corrupted.PinnedManifestPath, corrupted.ChunkDirectory); };
            await restoreCorrupted.Should()
                .ThrowAsync<InvalidOperationException>()
                .WithMessage("*failed SHA-256 verification*");
            Directory.Exists(corrupted.DestinationRoot).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    [Test]
    public async Task IncorrectChunkAndCompleteArchiveHashesShouldBeRejected()
    {
        EnsureWindows();

        string repositoryRoot = FindRepositoryRoot();
        string scriptPath = Path.Combine(repositoryRoot, "tools", "RestoreVendoredLlvmMingw.ps1");
        string temporaryRoot = Path.Combine(Path.GetTempPath(), "Sushi Toolchain Hash Validation 雪 " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryRoot);

        try
        {
            RestoreFixture incorrectChunkHash = CreateFixture(Path.Combine(temporaryRoot, "chunk-hash"));
            JsonNode chunkManifest = JsonNode.Parse(await File.ReadAllTextAsync(incorrectChunkHash.ChunkManifestPath))!;
            chunkManifest["chunks"]![0]!["sha256"] = new string('0', 64);
            await File.WriteAllTextAsync(incorrectChunkHash.ChunkManifestPath, chunkManifest.ToJsonString());
            Func<Task> restoreIncorrectChunkHash = async () => { await RunRestore(scriptPath, incorrectChunkHash.DestinationRoot, incorrectChunkHash.PinnedManifestPath, incorrectChunkHash.ChunkDirectory); };
            await restoreIncorrectChunkHash.Should()
                .ThrowAsync<InvalidOperationException>()
                .WithMessage("*failed SHA-256 verification*");

            RestoreFixture incorrectArchiveHash = CreateFixture(Path.Combine(temporaryRoot, "archive-hash"));
            JsonNode pinnedManifest = JsonNode.Parse(await File.ReadAllTextAsync(incorrectArchiveHash.PinnedManifestPath))!;
            JsonNode sourceManifest = JsonNode.Parse(await File.ReadAllTextAsync(incorrectArchiveHash.ChunkManifestPath))!;
            string wrongHash = new('0', 64);
            pinnedManifest["sha256"] = wrongHash;
            sourceManifest["archiveSha256"] = wrongHash;
            await File.WriteAllTextAsync(incorrectArchiveHash.PinnedManifestPath, pinnedManifest.ToJsonString());
            await File.WriteAllTextAsync(incorrectArchiveHash.ChunkManifestPath, sourceManifest.ToJsonString());

            Func<Task> restoreIncorrectArchiveHash = async () => { await RunRestore(scriptPath, incorrectArchiveHash.DestinationRoot, incorrectArchiveHash.PinnedManifestPath, incorrectArchiveHash.ChunkDirectory); };
            await restoreIncorrectArchiveHash.Should()
                .ThrowAsync<InvalidOperationException>()
                .WithMessage("*reassembled LLVM-MinGW archive failed SHA-256 verification*");
            Directory.Exists(incorrectArchiveHash.DestinationRoot).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Ignore("The pinned native toolchain is supported on Windows x64 only.");
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "tools", "RestoreVendoredLlvmMingw.ps1"))
                && File.Exists(Path.Combine(directory.FullName, "tools", "llvm-mingw.json")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Sushi checkout containing the vendored LLVM-MinGW archive.");
    }

    private static async Task<string> RunRestore(string scriptPath, string destinationRoot, string? pinnedManifestPath = null, string? chunkDirectory = null)
    {
        ProcessStartInfo startInfo = new("powershell.exe")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("-DestinationRoot");
        startInfo.ArgumentList.Add(destinationRoot);
        if (pinnedManifestPath is not null)
        {
            startInfo.ArgumentList.Add("-PinnedManifestPath");
            startInfo.ArgumentList.Add(pinnedManifestPath);
        }
        if (chunkDirectory is not null)
        {
            startInfo.ArgumentList.Add("-ChunkDirectory");
            startInfo.ArgumentList.Add(chunkDirectory);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start Windows PowerShell for vendored toolchain reconstruction.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        string output = await standardOutput;
        string error = await standardError;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Vendored toolchain reconstruction failed with exit code {process.ExitCode}: {error}{output}");
        }

        return output;
    }

    private static RestoreFixture CreateFixture(string fixtureRoot)
    {
        string sourceRoot = Path.Combine(fixtureRoot, "llvm-mingw-fixture");
        string chunkDirectory = Path.Combine(fixtureRoot, "chunks");
        string destinationRoot = Path.Combine(fixtureRoot, "output", "llvm-mingw");
        Directory.CreateDirectory(sourceRoot);
        Directory.CreateDirectory(chunkDirectory);

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
        foreach (string relativePath in files)
        {
            string path = Path.Combine(sourceRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "fixture");
        }

        string archivePath = Path.Combine(fixtureRoot, "fixture.zip");
        ZipFile.CreateFromDirectory(sourceRoot, archivePath, CompressionLevel.Optimal, includeBaseDirectory: true);
        byte[] archiveBytes = File.ReadAllBytes(archivePath);
        string archiveHash = Convert.ToHexString(SHA256.HashData(archiveBytes)).ToLowerInvariant();
        string chunkName = "archive.part000";
        string chunkPath = Path.Combine(chunkDirectory, chunkName);
        File.WriteAllBytes(chunkPath, archiveBytes);
        string chunkHash = Convert.ToHexString(SHA256.HashData(archiveBytes)).ToLowerInvariant();
        long chunkLimit = Math.Max(archiveBytes.Length, 1024);

        const string toolchain = "LLVM-MinGW";
        const string version = "fixture";
        const string llvmVersion = "22.1.8";
        const string archiveFileName = "fixture.zip";
        const string hostArchitecture = "x86_64";
        const string targetArchitecture = "x86_64";
        const string runtime = "ucrt";
        const string targetTriple = "x86_64-w64-windows-gnu";
        string pinnedManifestPath = Path.Combine(fixtureRoot, "pinned.json");
        string chunkManifestPath = Path.Combine(chunkDirectory, "manifest.json");
        File.WriteAllText(pinnedManifestPath, JsonSerializer.Serialize(new
        {
            toolchain,
            version,
            llvmVersion,
            archiveFileName,
            sha256 = archiveHash,
            archiveSize = archiveBytes.LongLength,
            hostArchitecture,
            targetArchitecture,
            runtime,
            targetTriple
        }));
        File.WriteAllText(chunkManifestPath, JsonSerializer.Serialize(new
        {
            toolchain,
            version,
            llvmVersion,
            archiveFileName,
            archiveSize = archiveBytes.LongLength,
            archiveSha256 = archiveHash,
            hostArchitecture,
            targetArchitecture,
            runtime,
            targetTriple,
            chunkSizeLimit = chunkLimit,
            chunks = new[] { new { name = chunkName, size = archiveBytes.LongLength, sha256 = chunkHash } }
        }));

        return new RestoreFixture(destinationRoot, chunkDirectory, pinnedManifestPath, chunkManifestPath);
    }

    private sealed record RestoreFixture(string DestinationRoot, string ChunkDirectory, string PinnedManifestPath, string ChunkManifestPath);
}

[CmdletBinding()]
param(
    [string] $DestinationRoot,
    [string] $ChunkDirectory,
    [string] $PinnedManifestPath
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($PinnedManifestPath)) {
    $PinnedManifestPath = Join-Path $PSScriptRoot "llvm-mingw.json"
}
$pinned = Get-Content -LiteralPath $PinnedManifestPath -Raw | ConvertFrom-Json

if ([string]::IsNullOrWhiteSpace($ChunkDirectory)) {
    $ChunkDirectory = Join-Path $repositoryRoot ("third_party/llvm-mingw/win-x64/" + $pinned.version)
}
$ChunkDirectory = [IO.Path]::GetFullPath($ChunkDirectory)
$chunkManifestPath = Join-Path $ChunkDirectory "manifest.json"

if ([string]::IsNullOrWhiteSpace($DestinationRoot)) {
    $DestinationRoot = Join-Path $repositoryRoot ".tools/llvm-mingw"
}
$DestinationRoot = [IO.Path]::GetFullPath($DestinationRoot)
$destinationParent = Split-Path -Parent $DestinationRoot
$destinationName = Split-Path -Leaf $DestinationRoot
$null = New-Item -ItemType Directory -Path $destinationParent -Force
$markerName = ".sushi-llvm-mingw.json"
$lockPath = Join-Path $destinationParent (".$destinationName.restore.lock")
$archivePath = Join-Path $destinationParent (".$destinationName-" + [Guid]::NewGuid().ToString("N") + ".zip")
$stagingPath = Join-Path $destinationParent (".$destinationName-staging-" + [Guid]::NewGuid().ToString("N"))
$backupPath = Join-Path $destinationParent (".$destinationName-backup-" + [Guid]::NewGuid().ToString("N"))
$lock = $null

function Get-Sha256([string] $Path) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    $stream = [IO.File]::OpenRead($Path)
    try {
        $hash = $algorithm.ComputeHash($stream)
        return ([BitConverter]::ToString($hash)).Replace("-", "").ToLowerInvariant()
    }
    finally {
        $stream.Dispose()
        $algorithm.Dispose()
    }
}

function Test-CompleteToolchain([string] $Root) {
    $required = @(
        "bin/clang.exe",
        "bin/ld.lld.exe",
        "x86_64-w64-mingw32/lib/crt2.o",
        "x86_64-w64-mingw32/lib/crtbegin.o",
        "x86_64-w64-mingw32/lib/crtend.o",
        "x86_64-w64-mingw32/lib/libmingw32.a"
    )
    foreach ($relativePath in $required) {
        if (-not (Test-Path -LiteralPath (Join-Path $Root $relativePath) -PathType Leaf)) {
            return $false
        }
    }

    $runtimeRoot = Join-Path $Root "lib/clang"
    $builtins = if (Test-Path -LiteralPath $runtimeRoot -PathType Container) {
        Get-ChildItem -LiteralPath $runtimeRoot -Filter "libclang_rt.builtins-x86_64.a" -File -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    }
    $hasHeaders = (Test-Path -LiteralPath (Join-Path $Root "include") -PathType Container) -or (Test-Path -LiteralPath (Join-Path $Root "x86_64-w64-mingw32/include") -PathType Container)
    return ($null -ne $builtins) -and $hasHeaders
}

function Test-VerifiedExistingToolchain {
    if (-not (Test-Path -LiteralPath $DestinationRoot -PathType Container)) {
        return $false
    }

    $markerPath = Join-Path $DestinationRoot $markerName
    if (-not (Test-Path -LiteralPath $markerPath -PathType Leaf)) {
        return $false
    }

    try {
        $installed = Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json
        return ($installed.version -eq $pinned.version) -and ($installed.archiveSha256 -eq $pinned.sha256) -and (Test-CompleteToolchain $DestinationRoot)
    }
    catch {
        return $false
    }
}

function Get-ValidatedArchiveManifest {
    if (-not (Test-Path -LiteralPath $chunkManifestPath -PathType Leaf)) {
        throw "The source-controlled LLVM-MinGW chunk manifest is missing: $chunkManifestPath"
    }

    $archiveManifest = Get-Content -LiteralPath $chunkManifestPath -Raw | ConvertFrom-Json
    foreach ($property in @("toolchain", "version", "llvmVersion", "archiveFileName", "archiveSize", "archiveSha256", "hostArchitecture", "targetArchitecture", "runtime", "targetTriple", "chunkSizeLimit", "chunks")) {
        if ($null -eq $archiveManifest.$property) {
            throw "The LLVM-MinGW chunk manifest is missing required property '$property'."
        }
    }

    $matchesPinned = $archiveManifest.toolchain -eq $pinned.toolchain
    $matchesPinned = $matchesPinned -and $archiveManifest.version -eq $pinned.version
    $matchesPinned = $matchesPinned -and $archiveManifest.llvmVersion -eq $pinned.llvmVersion
    $matchesPinned = $matchesPinned -and $archiveManifest.archiveFileName -eq $pinned.archiveFileName
    $matchesPinned = $matchesPinned -and [long] $archiveManifest.archiveSize -eq [long] $pinned.archiveSize
    $matchesPinned = $matchesPinned -and $archiveManifest.archiveSha256 -eq $pinned.sha256
    $matchesPinned = $matchesPinned -and $archiveManifest.hostArchitecture -eq $pinned.hostArchitecture
    $matchesPinned = $matchesPinned -and $archiveManifest.targetArchitecture -eq $pinned.targetArchitecture
    $matchesPinned = $matchesPinned -and $archiveManifest.runtime -eq $pinned.runtime
    $matchesPinned = $matchesPinned -and $archiveManifest.targetTriple -eq $pinned.targetTriple
    if (-not $matchesPinned) {
        throw "The source-controlled LLVM-MinGW chunk manifest does not match tools/llvm-mingw.json."
    }

    if ([long] $archiveManifest.chunkSizeLimit -gt 24MB -or [long] $archiveManifest.chunkSizeLimit -le 0) {
        throw "The LLVM-MinGW chunk size limit is invalid or exceeds GitHub's 24 MiB per-file limit."
    }

    if (@($archiveManifest.chunks).Count -eq 0) {
        throw "The source-controlled LLVM-MinGW chunk manifest contains no archive chunks."
    }

    return $archiveManifest
}

function Reassemble-VerifiedArchive($ArchiveManifest) {
    $chunks = @($ArchiveManifest.chunks)
    $listedNames = @{}
    foreach ($chunk in $chunks) {
        if ([string]::IsNullOrWhiteSpace($chunk.name) -or $listedNames.ContainsKey($chunk.name)) {
            throw "The LLVM-MinGW manifest has an empty or duplicate chunk name."
        }

        if ($chunk.name -notmatch '^archive\.part[0-9]{3}$') {
            throw "The LLVM-MinGW manifest contains an unsupported chunk name: $($chunk.name)"
        }
        $listedNames[$chunk.name] = $true
    }

    $actualParts = @(Get-ChildItem -LiteralPath $ChunkDirectory -Filter "archive.part*" -File)
    if ($actualParts.Count -ne $chunks.Count) {
        throw "LLVM-MinGW chunk count mismatch. The manifest lists $($chunks.Count) chunks, but the checkout contains $($actualParts.Count)."
    }

    $inputTotal = [long] 0
    foreach ($chunk in $chunks) {
        $chunkPath = Join-Path $ChunkDirectory $chunk.name
        if (-not (Test-Path -LiteralPath $chunkPath -PathType Leaf)) {
            throw "LLVM-MinGW archive chunk is missing: $chunkPath"
        }

        $chunkInfo = Get-Item -LiteralPath $chunkPath
        if ([long] $chunkInfo.Length -gt [long] $ArchiveManifest.chunkSizeLimit) {
            throw "LLVM-MinGW archive chunk '$($chunk.name)' exceeds the manifest chunk size limit."
        }
        if ([long] $chunkInfo.Length -ne [long] $chunk.size) {
            throw "LLVM-MinGW archive chunk '$($chunk.name)' has size $($chunkInfo.Length), expected $($chunk.size)."
        }

        $actualHash = Get-Sha256 $chunkPath
        if ($actualHash -ne ([string] $chunk.sha256).ToLowerInvariant()) {
            throw "LLVM-MinGW archive chunk '$($chunk.name)' failed SHA-256 verification."
        }
        $inputTotal += [long] $chunkInfo.Length
    }

    if ($inputTotal -ne [long] $ArchiveManifest.archiveSize) {
        throw "LLVM-MinGW archive chunks total $inputTotal bytes, expected $($ArchiveManifest.archiveSize)."
    }

    $output = [IO.File]::Open($archivePath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        foreach ($chunk in $chunks) {
            $input = [IO.File]::OpenRead((Join-Path $ChunkDirectory $chunk.name))
            try {
                $input.CopyTo($output)
            }
            finally {
                $input.Dispose()
            }
        }
    }
    finally {
        $output.Dispose()
    }

    $archiveInfo = Get-Item -LiteralPath $archivePath
    if ([long] $archiveInfo.Length -ne [long] $ArchiveManifest.archiveSize) {
        throw "The reassembled LLVM-MinGW archive has an unexpected byte length."
    }

    $archiveHash = Get-Sha256 $archivePath
    if ($archiveHash -ne ([string] $pinned.sha256).ToLowerInvariant()) {
        throw "The reassembled LLVM-MinGW archive failed SHA-256 verification."
    }
}

function Expand-VerifiedArchive($ArchiveManifest) {
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $null = New-Item -ItemType Directory -Path $stagingPath
    $archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
    try {
        $stagingPrefix = [IO.Path]::GetFullPath($stagingPath).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
        foreach ($entry in $archive.Entries) {
            $entryPath = $entry.FullName.Replace('\', '/')
            if ([string]::IsNullOrWhiteSpace($entryPath) -or $entryPath.StartsWith("/", [StringComparison]::Ordinal) -or $entryPath -match '^[A-Za-z]:') {
                throw "The LLVM-MinGW archive contains an absolute or empty path: $($entry.FullName)"
            }

            $unixMode = ([int] $entry.ExternalAttributes -shr 16) -band 0xF000
            if ($unixMode -eq 0xA000) {
                throw "The LLVM-MinGW archive contains an unsupported symbolic link: $($entry.FullName)"
            }

            $destination = [IO.Path]::GetFullPath((Join-Path $stagingPath $entryPath.Replace('/', [IO.Path]::DirectorySeparatorChar)))
            if (-not $destination.StartsWith($stagingPrefix, [StringComparison]::OrdinalIgnoreCase)) {
                throw "The LLVM-MinGW archive contains a path outside its extraction directory: $($entry.FullName)"
            }

            if ($entry.FullName.EndsWith("/", [StringComparison]::Ordinal) -or [string]::IsNullOrEmpty($entry.Name)) {
                $null = New-Item -ItemType Directory -Path $destination -Force
                continue
            }

            $null = New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force
            $entryStream = $entry.Open()
            try {
                $output = [IO.File]::Open($destination, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
                try {
                    $entryStream.CopyTo($output)
                }
                finally {
                    $output.Dispose()
                }
            }
            finally {
                $entryStream.Dispose()
            }
        }
    }
    finally {
        $archive.Dispose()
    }

    $clangCandidates = @(Get-ChildItem -LiteralPath $stagingPath -Filter "clang.exe" -File -Recurse | Where-Object { $_.Directory.Name -eq "bin" })
    if ($clangCandidates.Count -ne 1) {
        throw "Expected one upstream bin/clang.exe, found $($clangCandidates.Count). The pinned archive layout may have changed."
    }

    $stagedToolchain = $clangCandidates[0].Directory.Parent.FullName
    if (-not (Test-CompleteToolchain $stagedToolchain)) {
        throw "The verified LLVM-MinGW archive is missing required Clang, LLD, startup objects, runtime libraries, or headers."
    }

    $marker = [ordered]@{
        toolchain = $pinned.toolchain
        version = $pinned.version
        llvmVersion = $pinned.llvmVersion
        archiveFileName = $pinned.archiveFileName
        archiveSize = [long] $ArchiveManifest.archiveSize
        archiveSha256 = $pinned.sha256
        hostArchitecture = $pinned.hostArchitecture
        targetArchitecture = $pinned.targetArchitecture
        runtime = $pinned.runtime
        targetTriple = $pinned.targetTriple
    } | ConvertTo-Json
    [IO.File]::WriteAllText((Join-Path $stagedToolchain $markerName), $marker, [Text.UTF8Encoding]::new($false))

    return $stagedToolchain
}

try {
    for ($attempt = 0; $attempt -lt 120 -and $null -eq $lock; $attempt++) {
        try {
            $lock = [IO.File]::Open($lockPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        }
        catch [IO.IOException] {
            Start-Sleep -Milliseconds 250
        }
    }
    if ($null -eq $lock) {
        throw "Timed out waiting for another LLVM-MinGW reconstruction to finish."
    }

    if (Test-VerifiedExistingToolchain) {
        Write-Host "Verified LLVM-MinGW $($pinned.version) already exists at $DestinationRoot"
        return
    }

    $archiveManifest = Get-ValidatedArchiveManifest
    Reassemble-VerifiedArchive $archiveManifest
    $stagedToolchain = Expand-VerifiedArchive $archiveManifest

    if (Test-Path -LiteralPath $DestinationRoot) {
        Move-Item -LiteralPath $DestinationRoot -Destination $backupPath
    }
    try {
        Move-Item -LiteralPath $stagedToolchain -Destination $DestinationRoot
    }
    catch {
        if (Test-Path -LiteralPath $backupPath) {
            Move-Item -LiteralPath $backupPath -Destination $DestinationRoot
        }
        throw
    }
    if (Test-Path -LiteralPath $backupPath) {
        Remove-Item -LiteralPath $backupPath -Recurse -Force
    }

    Write-Host "Installed verified LLVM-MinGW $($pinned.version) at $DestinationRoot"
}
catch {
    throw "Vendored LLVM-MinGW reconstruction failed: $($_.Exception.Message)"
}
finally {
    if ($null -ne $lock) {
        $lock.Dispose()
        Remove-Item -LiteralPath $lockPath -Force -ErrorAction SilentlyContinue
    }
    foreach ($temporaryPath in @($archivePath, $stagingPath, $backupPath)) {
        if (Test-Path -LiteralPath $temporaryPath) {
            Remove-Item -LiteralPath $temporaryPath -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

[CmdletBinding()]
param(
    [string] $DestinationRoot,
    [switch] $Force
)

$ErrorActionPreference = "Stop"
if ($env:SUSHI_BLOCK_UPSTREAM_TOOLCHAIN_DOWNLOADS -eq "1") {
    throw "LLVM-MinGW upstream archive download is blocked for consumer restore/build/test/publish operations."
}
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $repositoryRoot "tools/llvm-mingw.json"

if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Pinned LLVM-MinGW metadata is missing: $manifestPath"
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($DestinationRoot)) {
    $DestinationRoot = Join-Path $repositoryRoot ".tools/llvm-mingw"
}

$DestinationRoot = [IO.Path]::GetFullPath($DestinationRoot)
$destinationParent = Split-Path -Parent $DestinationRoot
$destinationName = Split-Path -Leaf $DestinationRoot
$null = New-Item -ItemType Directory -Path $destinationParent -Force
$archivePath = Join-Path $destinationParent ("." + $destinationName + "-" + [Guid]::NewGuid().ToString("N") + ".zip")
$stagingPath = Join-Path $destinationParent ("." + $destinationName + "-staging-" + [Guid]::NewGuid().ToString("N"))
$backupPath = Join-Path $destinationParent ("." + $destinationName + "-backup-" + [Guid]::NewGuid().ToString("N"))
$markerName = ".sushi-llvm-mingw.json"

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

    $builtins = Get-ChildItem -LiteralPath (Join-Path $Root "lib/clang") -Filter "libclang_rt.builtins-x86_64.a" -File -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    $hasHeaders = (Test-Path -LiteralPath (Join-Path $Root "include") -PathType Container) -or (Test-Path -LiteralPath (Join-Path $Root "x86_64-w64-mingw32/include") -PathType Container)
    return ($null -ne $builtins) -and $hasHeaders
}

try {
    if (-not $Force -and (Test-Path -LiteralPath $DestinationRoot -PathType Container)) {
        $markerPath = Join-Path $DestinationRoot $markerName
        if (Test-Path -LiteralPath $markerPath -PathType Leaf) {
            $installed = Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json
            if ($installed.version -eq $manifest.version -and $installed.sha256 -eq $manifest.sha256 -and (Test-CompleteToolchain $DestinationRoot)) {
                Write-Host "Verified LLVM-MinGW $($manifest.version) already exists at $DestinationRoot"
                return
            }
        }
    }

    Write-Host "Package creation: downloading pinned LLVM-MinGW $($manifest.version) from $($manifest.archiveUrl)"
    Invoke-WebRequest -Uri $manifest.archiveUrl -OutFile $archivePath
    $actualHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $expectedHash = ([string] $manifest.sha256).ToLowerInvariant()
    if ($actualHash -ne $expectedHash) {
        throw "LLVM-MinGW archive SHA-256 mismatch. Expected $expectedHash but received $actualHash."
    }

    $null = New-Item -ItemType Directory -Path $stagingPath
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
    try {
        $stagingPrefix = [IO.Path]::GetFullPath($stagingPath).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
        foreach ($entry in $archive.Entries) {
            $entryPath = $entry.FullName.Replace('\', '/')
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
        throw "The downloaded LLVM-MinGW archive did not contain the required compiler, linker, startup objects, runtime, and headers."
    }

    $marker = [ordered]@{
        toolchain = $manifest.toolchain
        version = $manifest.version
        llvmVersion = $manifest.llvmVersion
        sha256 = $manifest.sha256
        targetTriple = $manifest.targetTriple
    } | ConvertTo-Json
    [IO.File]::WriteAllText((Join-Path $stagedToolchain $markerName), $marker, [Text.UTF8Encoding]::new($false))

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

    Write-Host "Installed verified LLVM-MinGW $($manifest.version) at $DestinationRoot"
}
catch {
    throw "LLVM-MinGW bootstrap failed: $($_.Exception.Message)"
}
finally {
    if (Test-Path -LiteralPath $archivePath) {
        Remove-Item -LiteralPath $archivePath -Force
    }

    if (Test-Path -LiteralPath $stagingPath) {
        Remove-Item -LiteralPath $stagingPath -Recurse -Force
    }
}

[CmdletBinding()]
param(
    [string] $PackageOutputDirectory = "artifacts/nuget",
    [string] $ToolchainRoot
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot "llvm-mingw.json") -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($ToolchainRoot)) {
    $ToolchainRoot = Join-Path $repositoryRoot ".tools/llvm-mingw"
}
$ToolchainRoot = [IO.Path]::GetFullPath($ToolchainRoot)
if ([IO.Path]::IsPathRooted($PackageOutputDirectory)) {
    $PackageOutputDirectory = [IO.Path]::GetFullPath($PackageOutputDirectory)
}
else {
    $PackageOutputDirectory = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $PackageOutputDirectory))
}
$packageProject = Join-Path $PSScriptRoot "LlvmMingwPackage/LlvmMingwPackage.csproj"
$maximumPackageBytes = 250000000

if ($env:SUSHI_BLOCK_UPSTREAM_TOOLCHAIN_DOWNLOADS -eq "1") {
    throw "Package creation cannot acquire LLVM-MinGW while consumer-download blocking is enabled."
}

& (Join-Path $PSScriptRoot "RestoreLlvmMingw.ps1") -DestinationRoot $ToolchainRoot
if (-not (Test-Path -LiteralPath (Join-Path $ToolchainRoot "bin/clang.exe") -PathType Leaf)) {
    throw "Pinned LLVM-MinGW acquisition did not produce a complete toolchain."
}

$null = New-Item -ItemType Directory -Path $PackageOutputDirectory -Force
dotnet pack $packageProject -c Release -o $PackageOutputDirectory "-p:LlvmMingwRoot=$ToolchainRoot" "-p:PackageVersion=$($manifest.packageVersion)"
if ($LASTEXITCODE -ne 0) {
    throw "LLVM-MinGW NuGet packaging failed with exit code $LASTEXITCODE."
}

$packagePath = Join-Path $PackageOutputDirectory "$($manifest.packageId).$($manifest.packageVersion).nupkg"
if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
    throw "NuGet packaging did not produce the expected package: $packagePath"
}
$packageLength = (Get-Item -LiteralPath $packagePath).Length
if ($packageLength -gt $maximumPackageBytes) {
    throw "The complete toolchain package is $packageLength bytes, exceeding NuGet.org's 250 MiB package limit. Split it into complete cohesive packages; do not omit toolchain files."
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($packagePath)
try {
    $requiredEntries = @(
        "contentFiles/any/any/toolchain/bin/clang.exe",
        "contentFiles/any/any/toolchain/bin/ld.lld.exe",
        "contentFiles/any/any/toolchain/x86_64-w64-mingw32/lib/crt2.o",
        "THIRD-PARTY-NOTICES.md"
    )
    foreach ($entryName in $requiredEntries) {
        if ($null -eq $archive.GetEntry($entryName)) {
            throw "The generated NuGet package is missing required entry $entryName."
        }
    }
    if (-not ($archive.Entries | Where-Object { $_.FullName -match 'contentFiles/any/any/toolchain/.+(LICENSE|COPYING|NOTICE)' })) {
        throw "The package does not retain the upstream license and notice files."
    }
}
finally {
    $archive.Dispose()
}

Write-Host "Validated $($manifest.packageId) $($manifest.packageVersion): $packageLength bytes at $packagePath"

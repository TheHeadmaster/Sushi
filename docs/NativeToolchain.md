# Windows Native Toolchain

Sushi emits LLVM object files and links them through Clang. On Windows x64, the compiler consumes a complete, pinned LLVM-MinGW distribution delivered as a NuGet package. This avoids dependence on machine-wide Clang, Visual Studio C++ Build Tools, MSVC linker libraries, and the Windows SDK.

## Pinned package

The lock metadata in [tools/llvm-mingw.json](../tools/llvm-mingw.json) identifies the complete toolchain package:

- NuGet package: `Sushi.NativeToolchain.LlvmMingw.WinX64` version `20260616`
- LLVM-MinGW release: 20260616; LLVM 22.1.8
- Runtime: UCRT
- Host and target architecture: x86_64
- Target triple: `x86_64-w64-windows-gnu`
- Upstream archive SHA-256: `b9b68a4d276e16fa25802aaba458e4638f64b3884c290aaccdc2d87083b6ca35`

The NuGet package preserves the complete upstream archive layout, including Clang, LLD, compiler runtime libraries, startup objects, headers, target libraries, and license notices. The Windows GNU target uses GNU startup objects and MinGW runtime libraries. The UCRT distribution targets Windows 10 or newer and does not require a Visual C++ Redistributable for the supported executable subset.

LLVM-MinGW is distributed by Martin Storsjö and contributors. The package retains upstream license and notice files and includes Sushi's third-party redistribution notice. Refer to those files for component-specific terms.

## Development

After the package is published to a NuGet feed available to the developer, the standard workflow is:

```powershell
git pull
dotnet restore Sushi.slnx
dotnet build Sushi.slnx
dotnet test Sushi.Testing/Sushi.Testing.csproj
```

No standalone toolchain bootstrap is part of local restore, build, test, or publish. If the package is unavailable, NuGet reports the ordinary package restore failure.

The [explicit package workflow](../.github/workflows/publish-native-toolchain.yml) builds the package from the pinned upstream archive and can optionally publish it through NuGet.org Trusted Publishing. Ordinary repository pushes never publish the package. The NuGet.org package is not considered available until it has been verified through the feed. Publication requires the NuGet package owner to configure a trusted-publishing policy and the repository Actions variable `NUGET_USER`.

## Package creation and validation

The packaging workflow invokes [RestoreLlvmMingw.ps1](../tools/RestoreLlvmMingw.ps1) only to acquire the upstream archive for package creation. It verifies the pinned SHA-256 before safe extraction, validates required compiler/runtime files, packages the complete extracted tree, preserves upstream notices, and rejects packages larger than NuGet.org's 250 MB limit.

Consumer CI restores the generated nupkg through NuGet from a local package source, then sets `SUSHI_BLOCK_UPSTREAM_TOOLCHAIN_DOWNLOADS=1` for consumer restore/build/test/publish operations. The upstream acquisition helper refuses to access the release when this guard is active. Developers restore the published dependency from their configured NuGet feed; the CI package artifact is not the developer distribution channel.

## Published compiler

On Windows x64, publish with:

```powershell
dotnet publish Compiler/Sushi.csproj -c Release -r win-x64 --self-contained true -o artifacts/publish/win-x64
```

The publish target stages the full restored package payload under `toolchains/win-x64/llvm-mingw` beside Sushi. The distribution remains usable after it is moved or extracted elsewhere, without access to the NuGet cache. Programs produced by Sushi do not include the toolchain.

## Runtime resolution

On Windows x64, Sushi resolves the toolchain in this order:

1. `SUSHI_NATIVE_TOOLCHAIN_ROOT`, when explicitly set to a complete toolchain root.
2. The published `toolchains/win-x64/llvm-mingw` directory beside the compiler.
3. The exact toolchain directory inside the restored NuGet package cache, identified at build time by NuGet's generated package path.

The selected root must contain Clang, LLD, Windows x64 GNU startup/runtime files, compiler builtins, and headers. Invalid explicit overrides fail directly. The linker runs the selected absolute `clang.exe`, passes the pinned target triple, and removes inherited MSVC/Windows SDK variables and unrelated PATH entries.

Bundled support currently targets Windows x64 only. Other operating systems retain the existing PATH-based Clang behavior. Updating LLVM-MinGW requires an explicit change to the pinned metadata, checksum, package version, and verification results.

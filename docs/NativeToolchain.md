# Windows Native Toolchain

Sushi's native backend uses LLVM to emit object files and the Clang driver to link them. On Windows x64, Sushi uses a pinned LLVM-MinGW distribution instead of relying on a machine-wide Clang, Visual Studio C++ Build Tools, MSVC linker libraries, or the Windows SDK.

## Pinned distribution

The toolchain metadata in [tools/llvm-mingw.json](../tools/llvm-mingw.json) is authoritative:

- LLVM-MinGW release: 20260616
- LLVM version: 22.1.8
- Runtime: UCRT
- Host and target architecture: x86_64
- Target triple: `x86_64-w64-windows-gnu`
- Upstream archive: [llvm-mingw 20260616 UCRT x86_64](https://github.com/mstorsjo/llvm-mingw/releases/download/20260616/llvm-mingw-20260616-ucrt-x86_64.zip)

The Windows GNU target is required because LLVM-MinGW supplies the GNU startup objects and MinGW runtime libraries. The UCRT package targets Windows 10 and later, using Windows-provided Universal C Runtime components rather than requiring the Visual C++ Redistributable. The generated native program does not include the toolchain itself.

LLVM-MinGW is distributed by Martin Storsjö and contributors. It bundles LLVM, Clang, LLD, compiler runtime components, and mingw-w64 headers/libraries under their respective upstream licenses. Preserve the notices included in the complete archive when redistributing the toolchain. See the [LLVM-MinGW project](https://github.com/mstorsjo/llvm-mingw) and its included license files for attribution and component-specific terms.

## Development setup

From the repository root, run:

```powershell
./tools/RestoreLlvmMingw.ps1
dotnet restore Sushi.slnx
dotnet build Sushi.slnx --no-restore
dotnet test Sushi.Testing/Sushi.Testing.csproj --no-build
```

The script downloads only the pinned archive, verifies its full SHA-256 before extraction, rejects unsafe archive paths, and installs the complete upstream directory layout under `.tools/llvm-mingw`. The directory is ignored by Git. Re-running the script reuses a complete extraction with a matching version and checksum marker. Use `-Force` to replace it.

No toolchain download occurs when Sushi compiles a program. Missing or incomplete native toolchain files produce an actionable error.

## Publish a portable compiler

On Windows x64 with the .NET 10 SDK:

```powershell
dotnet publish Compiler/Sushi.csproj -c Release -r win-x64 --self-contained true -o artifacts/publish/win-x64
```

The publish target restores and verifies the pinned toolchain, then stages the complete directory under `toolchains/win-x64/llvm-mingw` beside Sushi. The resulting directory is relocatable; copy or extract it elsewhere and invoke `Sushi.exe --project <project-or-folder>`.

## Runtime resolution

On Windows x64, Sushi checks `SUSHI_NATIVE_TOOLCHAIN_ROOT` first. This development and test override must name the complete LLVM-MinGW root, containing `bin/clang.exe`, `bin/ld.lld.exe`, target startup objects, compiler runtime libraries, and headers. If the variable is unset, Sushi resolves the bundled toolchain relative to `AppContext.BaseDirectory`:

```text
toolchains/win-x64/llvm-mingw
```

An invalid explicit override fails directly; Sushi does not fall back to the bundled copy or system PATH. Normal published use does not require the override. The linker runs the selected absolute `clang.exe`, passes the Windows GNU target, and sanitizes inherited MSVC/SDK include and library variables and PATH entries so unrelated installations cannot redirect linking.

Other operating systems retain the existing PATH-based `clang` behavior. Bundled portability is currently supported only for Windows x64; other Windows architectures are rejected. Updates to LLVM-MinGW must be explicit: change the single metadata file after reviewing the upstream release, LLVM compatibility, archive checksum, runtime, layout, and licenses.

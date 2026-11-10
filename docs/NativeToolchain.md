# Native Toolchain

Sushi emits Windows x64 GNU-targeted native objects with LLVM and links them through a portable LLVM-MinGW distribution. This fallback branch stores the pinned upstream archive as ordinary Git content because the development environment cannot access GitHub Releases reliably and the dedicated NuGet package is not yet available through NuGet restore. The NuGet-based approach remains on `v0.2.0-alpha-project-structure` for later reconsideration.

## Pinned distribution

- Upstream: [LLVM-MinGW](https://github.com/mstorsjo/llvm-mingw), release `20260616`.
- LLVM: `22.1.8`; UCRT runtime; Windows x64 host and target.
- Target triple: `x86_64-w64-windows-gnu`.
- Original archive: `llvm-mingw-20260616-ucrt-x86_64.zip`.
- SHA-256: `b9b68a4d276e16fa25802aaba458e4638f64b3884c290aaccdc2d87083b6ca35`.
- Source-controlled chunks: `third_party/llvm-mingw/win-x64/20260616`.

The archive is stored as nine consecutive, deterministic byte chunks, each no larger than 20 MiB. `.gitattributes` disables text normalization for these files. Git LFS is not used, and no extracted toolchain binaries are committed. The chunks can be reconstructed using only a normal Git checkout.

## Development

On Windows x64, the normal `dotnet build` invokes `tools/RestoreVendoredLlvmMingw.ps1` before compilation. The script validates every chunk's name, size, and SHA-256, reconstructs and checks the original archive, safely extracts the archive into a staging directory, validates the Clang driver, LLD, startup objects, runtime, and headers, then atomically installs the verified tree at `.tools/llvm-mingw`. Valid extractions are reused. No toolchain acquisition occurs during restore, build, test, or publish.

To reconstruct manually:

```powershell
./tools/RestoreVendoredLlvmMingw.ps1
```

The complete root may be overridden for development or testing with `SUSHI_NATIVE_TOOLCHAIN_ROOT`. An invalid explicit override fails instead of falling back. Windows x64 resolution prefers the explicit override, then the complete published toolchain, then the build-established repository extraction. Other operating systems retain their existing Clang-on-PATH behavior; portable LLVM-MinGW support currently targets Windows x64 only.

## Publish a portable compiler

```powershell
dotnet restore Sushi.slnx
dotnet publish Compiler/Sushi.csproj -c Release -r win-x64 --self-contained true -o artifacts/publish/win-x64
```

The publish target stages the complete verified extraction beside Sushi at `toolchains/win-x64/llvm-mingw`. The distribution can be relocated and does not need access to the repository or NuGet cache. Generated native programs do not include the compiler toolchain.

## Updating the pinned distribution

Toolchain changes are deliberate, versioned operations. Update `tools/llvm-mingw.json`, import and verify the new upstream archive, regenerate the chunk manifest and chunks, and rerun clean reconstruction, native linking, relocation, and executable tests. Preserve the upstream archive's notices and license files in the extracted distribution. LLVM-MinGW and included components remain subject to their respective upstream licenses; attribution and notices are distributed with the upstream archive. Do not change LLVM-MinGW versions automatically.

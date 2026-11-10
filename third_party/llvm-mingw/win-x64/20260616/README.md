# LLVM-MinGW source archive

This directory contains the unmodified LLVM-MinGW 20260616 UCRT Windows x64 archive, split into deterministic 20 MiB byte chunks for ordinary Git storage. The original archive SHA-256 is $(@{toolchain=LLVM-MinGW; version=20260616; llvmVersion=22.1.8; archiveFileName=llvm-mingw-20260616-ucrt-x86_64.zip; archiveUrl=https://github.com/mstorsjo/llvm-mingw/releases/download/20260616/llvm-mingw-20260616-ucrt-x86_64.zip; sha256=b9b68a4d276e16fa25802aaba458e4638f64b3884c290aaccdc2d87083b6ca35; hostArchitecture=x86_64; targetArchitecture=x86_64; runtime=ucrt; targetTriple=x86_64-w64-windows-gnu}.sha256).

Reconstruct and verify it with 	ools/RestoreVendoredLlvmMingw.ps1. Upstream project: https://github.com/mstorsjo/llvm-mingw. See the upstream distribution's license and notice files in the archive.

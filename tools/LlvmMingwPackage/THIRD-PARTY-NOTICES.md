# Third-Party Notices

This package redistributes the unmodified LLVM-MinGW 20260616 UCRT x86_64 distribution from Martin Storsjö's LLVM-MinGW project:

- Upstream project: https://github.com/mstorsjo/llvm-mingw
- Upstream archive: https://github.com/mstorsjo/llvm-mingw/releases/download/20260616/llvm-mingw-20260616-ucrt-x86_64.zip
- Verified SHA-256: b9b68a4d276e16fa25802aaba458e4638f64b3884c290aaccdc2d87083b6ca35

LLVM-MinGW bundles LLVM, Clang, LLD, mingw-w64 headers and libraries, compiler runtime components, and related tools. Their licenses and notices are retained under `toolchain/`. The bundled components are governed by their respective licenses; consult the retained upstream files before redistribution or modification. LLVM components generally use Apache License 2.0 with LLVM exceptions, while other bundled components may use different licenses.

Sushi's packaging of this dependency does not change or supersede any upstream license terms.

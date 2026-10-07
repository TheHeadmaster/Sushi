using LLVMSharp.Interop;
using LLVMNative = LLVMSharp.Interop.LLVM;

namespace Sushi.Backends.LLVM;

/// <summary>
/// Provides process-wide initialization and host target-machine creation for native LLVM code generation.
/// </summary>
internal static class LLVMNativeTarget
{
    private static readonly Lazy<bool> initialization = new(Initialize, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Creates an LLVM target machine for the compiler host.
    /// </summary>
    /// <returns>
    /// A native target-machine handle owned by the caller.
    /// </returns>
    public static LLVMTargetMachineRef CreateHostTargetMachine()
    {
        _ = initialization.Value;

        string triple = LLVMTargetRef.DefaultTriple;

        if (string.IsNullOrWhiteSpace(triple))
        {
            throw new InvalidOperationException("LLVM did not provide a default host target triple.");
        }

        LLVMTargetRef target = LLVMTargetRef.GetTargetFromTriple(triple);

        LLVMTargetMachineRef targetMachine = target.CreateTargetMachine(
            triple,
            cpu: "generic",
            features: string.Empty,
            LLVMCodeGenOptLevel.LLVMCodeGenLevelDefault,
            LLVMRelocMode.LLVMRelocDefault,
            LLVMCodeModel.LLVMCodeModelDefault);

        if (targetMachine.Handle == IntPtr.Zero)
        {
            throw new InvalidOperationException($"LLVM could not create a target machine for \"{triple}\".");
        }

        return targetMachine;
    }

    private static bool Initialize()
    {
        if (LLVMNative.InitializeNativeTarget() != 0)
        {
            throw new InvalidOperationException("LLVM could not initialize the native target.");
        }

        if (LLVMNative.InitializeNativeAsmPrinter() != 0)
        {
            throw new InvalidOperationException("LLVM could not initialize the native assembly printer.");
        }

        return true;
    }
}
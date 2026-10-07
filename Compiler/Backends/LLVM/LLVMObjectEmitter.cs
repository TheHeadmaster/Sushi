using LLVMSharp.Interop;
using Sushi.Backends.Native;
using LLVMNative = LLVMSharp.Interop.LLVM;

namespace Sushi.Backends.LLVM;

/// <summary>
/// Emits verified LLVM modules as native object files for the compiler host.
/// </summary>
public sealed class LLVMObjectEmitter
{
    /// <summary>
    /// Emits an LLVM module as a host-native object file.
    /// </summary>
    /// <param name="artifact">
    /// The owned LLVM module to emit.
    /// </param>
    /// <param name="outputPath">
    /// The destination object-file path.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel object emission.
    /// </param>
    /// <returns>
    /// The emitted native object artifact.
    /// </returns>
    public NativeObjectArtifact EmitForHost(LLVMModuleArtifact artifact, string outputPath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentException.ThrowIfNullOrEmpty(outputPath);

        cancellationToken.ThrowIfCancellationRequested();

        string fullOutputPath = Path.GetFullPath(outputPath);
        string? directory = Path.GetDirectoryName(fullOutputPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        LLVMTargetMachineRef targetMachine = default;
        LLVMTargetDataRef targetData = default;
        LLVMModuleRef module = artifact.Module;
        
        try
        {
            targetMachine = LLVMNativeTarget.CreateHostTargetMachine();
            targetData = targetMachine.CreateTargetDataLayout();

            if (targetData.Handle == IntPtr.Zero)
            {
                throw new InvalidOperationException("LLVM could not create the host target data layout.");
            }

            string targetTriple = targetMachine.Triple;

            if (string.IsNullOrWhiteSpace(targetTriple))
            {
                throw new InvalidOperationException("LLVM target machine did not provide a target triple.");
            }

            module.Target = targetTriple;
            module.DataLayout = targetData.StringRepresentation;

            VerifyModule(artifact.Module);

            this.EmitObjectFile(targetMachine, artifact.Module, fullOutputPath, cancellationToken);

            return new NativeObjectArtifact(fullOutputPath, targetTriple);
        }
        finally
        {
            DisposeTargetData(targetData);
            DisposeTargetMachine(targetMachine);
        }
    }

    private void EmitObjectFile(LLVMTargetMachineRef targetMachine, LLVMModuleRef module, string outputPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string temporaryPath = $"{outputPath}.{Guid.NewGuid():N}.tmp";

        try
        {
            if (!targetMachine.TryEmitToFile(module, temporaryPath, LLVMCodeGenFileType.LLVMObjectFile, out string message))
            {
                throw new InvalidOperationException($"LLVM could not emit the native object file: {message}");
            }

            cancellationToken.ThrowIfCancellationRequested();

            File.Move(temporaryPath, outputPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void VerifyModule(LLVMModuleRef module)
    {
        if (module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message))
        {
            return;
        }

        throw new InvalidOperationException($"LLVM rejected the target-configured module: {message}");
    }

    private static unsafe void DisposeTargetData(LLVMTargetDataRef targetData)
    {
        if (targetData.Handle != IntPtr.Zero)
        {
            LLVMNative.DisposeTargetData(targetData);
        }
    }

    private static unsafe void DisposeTargetMachine(LLVMTargetMachineRef targetMachine)
    {
        if (targetMachine.Handle != IntPtr.Zero)
        {
            LLVMNative.DisposeTargetMachine(targetMachine);
        }
    }
}
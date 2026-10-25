using LLVMSharp.Interop;
using Sushi.Intermediate;

namespace Sushi.Backends.LLVM;

/// <summary>
/// Synthesizes the temporary native process entry shim used by the executable MVP.
/// This does not define Sushi source-level entry-point semantics.
/// </summary>
internal static class TemporaryMVPEntryShim
{
    private const string NativeEntrySymbol = "main";
    private const string SushiEntryTargetSymbol = "__sushi_mvp_entry_target";

    /// <summary>
    /// Converts one explicitly selected Sushi function into the target of a generated C-compatible
    /// native entry function. Boolean results are widened to int32 solely for process-exit testing.
    /// </summary>
    /// <param name="artifact">
    /// The LLVM module containing the selected Sushi function.
    /// </param>
    /// <param name="selectedFunctionName">
    /// The semantic name of the Sushi function selected by the MVP compiler pipeline.
    /// </param>
    /// <param name="selectedReturnType">
    /// The lowered return type of the selected function.
    /// </param>
    public static void Add(LLVMModuleArtifact artifact, string selectedFunctionName, IRType selectedReturnType)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentException.ThrowIfNullOrEmpty(selectedFunctionName);

        LLVMModuleRef module = artifact.Module;
        LLVMContextRef context = module.Context;

        LLVMValueRef selectedFunction = module.GetNamedFunction(selectedFunctionName);

        if (selectedFunction.Handle == IntPtr.Zero)
        {
            throw new InvalidOperationException($"The selected MVP entry function \"{selectedFunctionName}\" does not exist in the LLVM module.");
        }

        if (selectedFunctionName == SushiEntryTargetSymbol || module.GetNamedFunction(SushiEntryTargetSymbol).Handle != IntPtr.Zero)
        {
            throw new InvalidOperationException($"The LLVM module already contains the reserved MVP symbol \"{SushiEntryTargetSymbol}\".");
        }

        LLVMTypeRef sushiReturnType = selectedReturnType switch
        {
            IRType.Int32 => context.Int32Type,
            IRType.Bool => context.Int1Type,
            _ => throw new NotSupportedException($"MVP entry return type \"{selectedReturnType}\" is not supported.")
        };

        selectedFunction.Name = SushiEntryTargetSymbol;
        selectedFunction.Linkage = LLVMLinkage.LLVMInternalLinkage;

        if (module.GetNamedFunction(NativeEntrySymbol).Handle != IntPtr.Zero)
        {
            throw new InvalidOperationException($"The LLVM module already contains the reserved native entry symbol \"{NativeEntrySymbol}\".");
        }

        LLVMTypeRef sushiFunctionType = LLVMTypeRef.CreateFunction(sushiReturnType, []);
        LLVMTypeRef nativeEntryType = LLVMTypeRef.CreateFunction(context.Int32Type, [context.Int32Type, context.CreatePointerType(0)]);

        LLVMValueRef nativeEntry = module.AddFunction(NativeEntrySymbol, nativeEntryType);

        nativeEntry.GetParam(0).Name = "argc";
        nativeEntry.GetParam(1).Name = "argv";

        LLVMBasicBlockRef entryBlock = nativeEntry.AppendBasicBlock("entry");

        using LLVMBuilderRef builder = context.CreateBuilder();

        builder.PositionAtEnd(entryBlock);

        LLVMValueRef result = builder.BuildCall2(sushiFunctionType, selectedFunction, Array.Empty<LLVMValueRef>(), "sushi.result");

        LLVMValueRef exitCode = selectedReturnType is IRType.Bool
            ? builder.BuildZExt(result, context.Int32Type, "sushi.exitcode")
            : result;

        builder.BuildRet(exitCode);

        if (!module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string verificationMessage))
        {
            throw new InvalidOperationException($"LLVM rejected the generated MVP entry shim: {verificationMessage}");
        }
    }
}
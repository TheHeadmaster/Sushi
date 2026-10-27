using LLVMSharp.Interop;
using Sushi.Intermediate;

namespace Sushi.Backends.LLVM;

/// <inheritdoc cref="IEntryShim"/>
internal class TemporaryMVPEntryShim : IEntryShim
{
    private const string NativeEntrySymbol = "main";
    private const string SushiEntryTargetSymbol = "__sushi_mvp_entry_target";

    /// <inheritdoc />
    public void Add(IModuleArtifact artifact, string selectedFunctionName, IRType selectedReturnType)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentException.ThrowIfNullOrEmpty(selectedFunctionName);

        if (artifact is not LLVMModuleArtifact llvmArtifact)
        {
            throw new ArgumentException("non-LLVMModuleArtifact objects are not supported in TemporaryMVPEntryShim.");
        }

        LLVMModuleRef module = llvmArtifact.Module;
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

using LLVMSharp.Interop;

namespace Sushi.Backends.LLVM;

/// <summary>
/// Generates the native entry point for the currently supported synchronous task-root lifecycle.
/// </summary>
/// <remarks>
/// The initial field-free root requires no instance storage. Entry completion is followed by
/// normal stopping and optional stop invocation. Activity state, mailboxes, and persistent
/// closure are outside this bootstrap subset.
/// </remarks>
internal static class RootActivityEntryShim
{
    private const string NativeEntrySymbol = "main";

    /// <summary>
    /// Adds the native startup sequence for a compiled task root.
    /// </summary>
    /// <param name="artifact">
    /// The module containing both lifecycle methods.
    /// </param>
    /// <param name="entrySymbol">
    /// The internal symbol for the root entry method.
    /// </param>
    /// <param name="stopSymbol">
    /// The optional internal symbol for the stop method.
    /// </param>
    public static void Add(IModuleArtifact artifact, string entrySymbol, string? stopSymbol)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentException.ThrowIfNullOrEmpty(entrySymbol);

        if (artifact is not LLVMModuleArtifact llvmArtifact)
        {
            throw new ArgumentException("Root activity bootstrap requires an LLVM module.", nameof(artifact));
        }

        if (stopSymbol is not null && (stopSymbol.Length == 0 || stopSymbol == entrySymbol))
        {
            throw new ArgumentException("The stop hook must have a distinct, nonempty symbol.", nameof(stopSymbol));
        }

        LLVMModuleRef module = llvmArtifact.Module;
        LLVMContextRef context = module.Context;

        LLVMValueRef entry = GetLifecycleFunction(module, entrySymbol);
        LLVMValueRef stop = stopSymbol is null ? default : GetLifecycleFunction(module, stopSymbol);

        if (module.GetNamedFunction(NativeEntrySymbol).Handle != IntPtr.Zero)
        {
            throw new InvalidOperationException("The native entry symbol is already defined.");
        }

        LLVMTypeRef lifecycleType = LLVMTypeRef.CreateFunction(context.VoidType, []);
        LLVMTypeRef nativeEntryType = LLVMTypeRef.CreateFunction(
            context.Int32Type,
            [context.Int32Type, context.CreatePointerType(0)]);

        LLVMValueRef nativeEntry = module.AddFunction(NativeEntrySymbol, nativeEntryType);

        nativeEntry.GetParam(0).Name = "argc";
        nativeEntry.GetParam(1).Name = "argv";

        LLVMBasicBlockRef entryBlock = nativeEntry.AppendBasicBlock("entry");

        using LLVMBuilderRef builder = context.CreateBuilder();

        builder.PositionAtEnd(entryBlock);

        builder.BuildCall2(lifecycleType, entry, Array.Empty<LLVMValueRef>(), string.Empty);

        if (stopSymbol is not null)
        {
            builder.BuildCall2(lifecycleType, stop, Array.Empty<LLVMValueRef>(), string.Empty);
        }

        builder.BuildRet(LLVMValueRef.CreateConstInt(context.Int32Type, 0UL));

        if (!module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message))
        {
            throw new InvalidOperationException($"LLVM rejected the root activity bootstrap: {message}");
        }
    }

    private static LLVMValueRef GetLifecycleFunction(LLVMModuleRef module, string symbol)
    {
        LLVMValueRef function = module.GetNamedFunction(symbol);

        if (function.Handle == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Root lifecycle function \"{symbol}\" does not exist.");
        }

        return function;
    }
}
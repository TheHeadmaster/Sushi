using LLVMSharp.Interop;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;

namespace Sushi.Backends.LLVM;

/// <summary>
/// Translates lowered Sushi control-flow IR into LLVM IR.
/// </summary>
public sealed class LLVMModuleEmitter
{
    /// <summary>
    /// Emits one lowered Sushi function into a new verified LLVM module.
    /// </summary>
    /// <param name="moduleName">
    /// The diagnostic identity assigned to the generated LLVM module.
    /// </param>
    /// <param name="function">
    /// The lowered function to translate.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel LLVM translation.
    /// </param>
    /// <returns>
    /// An owned LLVM module artifact. The caller is responsible for disposing it.
    /// </returns>
    public LLVMModuleArtifact Emit(string moduleName, LoweredFunction function, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(moduleName);
        ArgumentNullException.ThrowIfNull(function);

        cancellationToken.ThrowIfCancellationRequested();

        LLVMContextRef context = LLVMContextRef.Create();
        LLVMModuleRef module = default;

        try
        {
            module = context.CreateModuleWithName(moduleName);

            using LLVMBuilderRef builder = context.CreateBuilder();

            this.EmitFunction(module, builder, function, cancellationToken);
            VerifyModule(module);

            return new LLVMModuleArtifact(context, module);
        }
        catch
        {
            if (module.Handle != IntPtr.Zero)
            {
                module.Dispose();
            }

            context.Dispose();
            throw;
        }
    }

    private void EmitFunction(LLVMModuleRef module, LLVMBuilderRef builder, LoweredFunction function, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        LLVMTypeRef returnType = ConvertType(module.Context, function.ReturnType);
        LLVMTypeRef functionType = LLVMTypeRef.CreateFunction(returnType, []);
        LLVMValueRef llvmFunction = module.AddFunction(function.Name, functionType);

        foreach (LoweredBasicBlock block in function.Blocks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            this.EmitBlock(module.Context, llvmFunction, builder, block, cancellationToken);
        }
    }

    private void EmitBlock(LLVMContextRef context, LLVMValueRef function, LLVMBuilderRef builder, LoweredBasicBlock block, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        LLVMBasicBlockRef llvmBlock = function.AppendBasicBlock(block.Name);

        builder.PositionAtEnd(llvmBlock);

        foreach (LoweredInstruction instruction in block.Instructions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            this.EmitInstruction(instruction);
        }

        this.EmitTerminator(context, builder, block.Terminator, cancellationToken);
    }

    private void EmitInstruction(LoweredInstruction instruction)
        => throw new NotSupportedException($"Lowered instruction type \"{instruction.GetType().Name}\" is not supported by the LLVM backend.");

    private void EmitTerminator(LLVMContextRef context, LLVMBuilderRef builder, LoweredTerminator terminator, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (terminator)
        {
            case LoweredReturnTerminator returnTerminator:
                builder.BuildRet(this.EmitValue(context, returnTerminator.Value));
                return;

            default:
                throw new NotSupportedException($"Lowered terminator type \"{terminator.GetType().Name}\" is not supported by the LLVM backend.");
        }
    }

    private LLVMValueRef EmitValue(LLVMContextRef context, LoweredValue value)
        => value switch
        {
            LoweredIntegerConstant integerConstant => LLVMValueRef.CreateConstInt(
                ConvertType(context, integerConstant.Type),
                unchecked((ulong)(uint)integerConstant.Value)),
            _ => throw new NotSupportedException($"Lowered value type \"{value.GetType().Name}\" is not supported by the LLVM backend.")
        };

    private static LLVMTypeRef ConvertType(LLVMContextRef context, IRType type)
        => type switch
        {
            IRType.Int32 => context.Int32Type,
            _ => throw new NotSupportedException($"IR type \"{type}\" is not supported by the LLVM backend.")
        };

    private static void VerifyModule(LLVMModuleRef module)
    {
        if (module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message))
        {
            return;
        }

        throw new InvalidOperationException($"LLVM rejected the generated module: {message}");
    }
}
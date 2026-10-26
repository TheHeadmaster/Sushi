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
        LLVMTypeRef[] parameterTypes = [.. function.Parameters.Select(parameter => ConvertType(module.Context, parameter.Type))];
        LLVMTypeRef functionType = LLVMTypeRef.CreateFunction(returnType, parameterTypes);
        LLVMValueRef llvmFunction = module.AddFunction(function.Name, functionType);

        foreach (IRParameter parameter in function.Parameters)
        {
            llvmFunction.GetParam((uint)parameter.Index).Name = parameter.Name;
        }

        foreach (LoweredBasicBlock block in function.Blocks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            this.EmitBlock(module.Context, llvmFunction, function, builder, block, cancellationToken);
        }
    }

    private void EmitBlock(LLVMContextRef context, LLVMValueRef function, LoweredFunction loweredFunction, LLVMBuilderRef builder, LoweredBasicBlock block, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        LLVMBasicBlockRef llvmBlock = function.AppendBasicBlock(block.Name);

        builder.PositionAtEnd(llvmBlock);

        foreach (LoweredInstruction instruction in block.Instructions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            this.EmitInstruction(instruction);
        }

        EmitTerminator(context, function, loweredFunction, builder, block.Terminator, cancellationToken);
    }

    private void EmitInstruction(LoweredInstruction instruction)
        => throw new NotSupportedException($"Lowered instruction type \"{instruction.GetType().Name}\" is not supported by the LLVM backend.");

    private static void EmitTerminator(LLVMContextRef context, LLVMValueRef function, LoweredFunction loweredFunction, LLVMBuilderRef builder, LoweredTerminator terminator, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (terminator)
        {
            case LoweredReturnTerminator returnTerminator:
                builder.BuildRet(EmitValue(context, function, loweredFunction, returnTerminator.Value));
                return;

            default:
                throw new NotSupportedException($"Lowered terminator type \"{terminator.GetType().Name}\" is not supported by the LLVM backend.");
        }
    }

    private static LLVMValueRef EmitValue(LLVMContextRef context, LLVMValueRef function, LoweredFunction loweredFunction, LoweredValue value)
        => value switch
        {
            LoweredIntegerConstant integerConstant => LLVMValueRef.CreateConstInt(
                ConvertType(context, integerConstant.Type),
                unchecked((ulong)(uint)integerConstant.Value)),

            LoweredBooleanConstant booleanConstant => LLVMValueRef.CreateConstInt(
                ConvertType(context, booleanConstant.Type),
                booleanConstant.Value ? 1UL : 0UL),

            LoweredParameterValue parameter => EmitParameterValue(function, loweredFunction, parameter),

            _ => throw new NotSupportedException($"Lowered value type \"{value.GetType().Name}\" is not supported by the LLVM backend.")
        };

    private static LLVMValueRef EmitParameterValue(LLVMValueRef function, LoweredFunction loweredFunction, LoweredParameterValue parameter)
    {
        if (parameter.Index >= loweredFunction.Parameters.Count || loweredFunction.Parameters[parameter.Index].Type != parameter.Type)
        {
            throw new InvalidOperationException("The lowered parameter reference is outside its function signature or has the wrong type.");
        }

        LLVMValueRef argument = function.GetParam((uint)parameter.Index);

        if (argument.Handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("LLVM did not provide the requested function parameter.");
        }

        return argument;
    }

    private static LLVMTypeRef ConvertType(LLVMContextRef context, IRType type)
        => type switch
        {
            IRType.Int32 => context.Int32Type,
            IRType.Bool => context.Int1Type,
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
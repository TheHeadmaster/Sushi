using LLVMSharp.Interop;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;

namespace Sushi.Backends.LLVM;

/// <summary>
/// Translates lowered Sushi functions into verified LLVM IR, including
/// SSA values, conditional control flow, and PHI-based value merging.
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

            EmitFunction(module, builder, function, cancellationToken);
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

    private static void EmitFunction(LLVMModuleRef module, LLVMBuilderRef builder, LoweredFunction function, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        LLVMTypeRef returnType = ConvertType(module.Context, function.ReturnType);
        LLVMTypeRef[] parameterTypes = [.. function.Parameters.Select(parameter => ConvertType(module.Context, parameter.Type))];
        LLVMTypeRef functionType = LLVMTypeRef.CreateFunction(returnType, parameterTypes);
        LLVMValueRef llvmFunction = module.AddFunction(function.Name, functionType);

        foreach (IRParameter parameter in function.Parameters)
        {
            cancellationToken.ThrowIfCancellationRequested();
            llvmFunction.GetParam((uint)parameter.Index).Name = parameter.Name;
        }

        FunctionEmitter emitter = new(module.Context, llvmFunction, function, builder, cancellationToken);
        emitter.Emit();
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

    private sealed class FunctionEmitter
    {
        private readonly LLVMContextRef context;
        private readonly LLVMValueRef function;
        private readonly LoweredFunction loweredFunction;
        private readonly LLVMBuilderRef builder;
        private readonly CancellationToken cancellationToken;
        private readonly Dictionary<string, LLVMBasicBlockRef> blocks = new(StringComparer.Ordinal);
        private readonly Dictionary<int, (LLVMValueRef Value, IRType Type)> temporaries = [];
        private readonly List<(LLVMValueRef Phi, LoweredBooleanPhiInstruction Instruction)> pendingPhis = [];

        public FunctionEmitter(LLVMContextRef context, LLVMValueRef function, LoweredFunction loweredFunction, LLVMBuilderRef builder, CancellationToken cancellationToken)
        {
            this.context = context;
            this.function = function;
            this.loweredFunction = loweredFunction;
            this.builder = builder;
            this.cancellationToken = cancellationToken;
        }

        public void Emit()
        {
            foreach (LoweredBasicBlock block in this.loweredFunction.Blocks)
            {
                this.cancellationToken.ThrowIfCancellationRequested();
                this.blocks.Add(block.Name, this.function.AppendBasicBlock(block.Name));
            }

            foreach (LoweredBasicBlock block in this.loweredFunction.Blocks)
            {
                this.cancellationToken.ThrowIfCancellationRequested();
                this.EmitBlock(block);
            }

            this.ConnectPhiIncomingValues();
        }

        private void EmitBlock(LoweredBasicBlock block)
        {
            this.builder.PositionAtEnd(this.GetBlock(block.Name));

            bool ordinaryInstructionSeen = false;

            foreach (LoweredInstruction instruction in block.Instructions)
            {
                this.cancellationToken.ThrowIfCancellationRequested();

                if (instruction is LoweredBooleanPhiInstruction phi)
                {
                    if (ordinaryInstructionSeen)
                    {
                        throw new InvalidOperationException("PHI instructions must precede ordinary instructions in a basic block.");
                    }

                    this.EmitPhi(phi);
                    continue;
                }

                ordinaryInstructionSeen = true;
                this.EmitInstruction(instruction);
            }

            this.EmitTerminator(block.Terminator);
        }

        private void EmitPhi(LoweredBooleanPhiInstruction instruction)
        {
            LLVMValueRef phi = this.builder.BuildPhi(this.context.Int1Type, $"sushi.bool.{instruction.Destination.Index}");

            this.RegisterTemporary(instruction.Destination, phi);
            this.pendingPhis.Add((phi, instruction));
        }

        private void EmitInstruction(LoweredInstruction instruction)
        {
            switch (instruction)
            {
                case LoweredBooleanNotInstruction negation:
                {
                    LLVMValueRef operand = this.EmitValue(negation.Operand);
                    LLVMValueRef trueValue = LLVMValueRef.CreateConstInt(this.context.Int1Type, 1UL);
                    LLVMValueRef result = this.builder.BuildXor(operand, trueValue, $"sushi.bool.{negation.Destination.Index}");

                    this.RegisterTemporary(negation.Destination, result);
                    return;
                }

                case LoweredBooleanBinaryInstruction binary:
                {
                    LLVMValueRef left = this.EmitValue(binary.Left);
                    LLVMValueRef right = this.EmitValue(binary.Right);
                    string name = $"sushi.bool.{binary.Destination.Index}";

                    LLVMValueRef result = binary.Operator switch
                    {
                        IRBooleanBinaryOperator.EagerAnd => this.builder.BuildAnd(left, right, name),
                        IRBooleanBinaryOperator.EagerOr => this.builder.BuildOr(left, right, name),
                        IRBooleanBinaryOperator.EagerXor => this.builder.BuildXor(left, right, name),
                        _ => throw new NotSupportedException($"Boolean operator \"{binary.Operator}\" requires different LLVM lowering.")
                    };

                    this.RegisterTemporary(binary.Destination, result);
                    return;
                }

                default:
                    throw new NotSupportedException($"Lowered instruction type \"{instruction.GetType().Name}\" is not supported by the LLVM backend.");
            }
        }

        private void EmitTerminator(LoweredTerminator terminator)
        {
            this.cancellationToken.ThrowIfCancellationRequested();

            switch (terminator)
            {
                case LoweredReturnTerminator result:
                    this.builder.BuildRet(this.EmitValue(result.Value));
                    return;

                case LoweredBranchTerminator branch:
                    this.builder.BuildBr(this.GetBlock(branch.Target));
                    return;

                case LoweredConditionalBranchTerminator branch:
                    this.builder.BuildCondBr(this.EmitValue(branch.Condition), this.GetBlock(branch.TrueTarget), this.GetBlock(branch.FalseTarget));
                    return;

                default:
                    throw new NotSupportedException($"Lowered terminator type \"{terminator.GetType().Name}\" is not supported by the LLVM backend.");
            }
        }

        private LLVMValueRef EmitValue(LoweredValue value)
            => value switch
            {
                LoweredIntegerConstant integer => LLVMValueRef.CreateConstInt(
                    ConvertType(this.context, integer.Type),
                    unchecked((ulong)(uint)integer.Value)),

                LoweredBooleanConstant boolean => LLVMValueRef.CreateConstInt(
                    this.context.Int1Type,
                    boolean.Value ? 1UL : 0UL),

                LoweredParameterValue parameter => this.EmitParameterValue(parameter),
                LoweredTemporaryValue temporary => this.EmitTemporaryValue(temporary),
                _ => throw new NotSupportedException($"Lowered value type \"{value.GetType().Name}\" is not supported by the LLVM backend.")
            };

        private LLVMValueRef EmitParameterValue(LoweredParameterValue parameter)
        {
            if (parameter.Index >= this.loweredFunction.Parameters.Count || this.loweredFunction.Parameters[parameter.Index].Type != parameter.Type)
            {
                throw new InvalidOperationException("The lowered parameter reference is outside its function signature or has the wrong type.");
            }

            LLVMValueRef argument = this.function.GetParam((uint)parameter.Index);

            if (argument.Handle == IntPtr.Zero)
            {
                throw new InvalidOperationException("LLVM did not provide the requested function parameter.");
            }

            return argument;
        }

        private LLVMValueRef EmitTemporaryValue(LoweredTemporaryValue temporary)
        {
            if (!this.temporaries.TryGetValue(temporary.Index, out (LLVMValueRef Value, IRType Type) resolved))
            {
                throw new InvalidOperationException($"Temporary value {temporary.Index} was used before its definition was emitted.");
            }

            if (resolved.Type != temporary.Type)
            {
                throw new InvalidOperationException($"Temporary value {temporary.Index} was referenced with an inconsistent type.");
            }

            return resolved.Value;
        }

        private void RegisterTemporary(LoweredTemporaryValue destination, LLVMValueRef value)
        {
            if (!this.temporaries.TryAdd(destination.Index, (value, destination.Type)))
            {
                throw new InvalidOperationException($"Temporary value {destination.Index} is defined more than once within the function.");
            }
        }

        private LLVMBasicBlockRef GetBlock(string name)
        {
            if (!this.blocks.TryGetValue(name, out LLVMBasicBlockRef block))
            {
                throw new InvalidOperationException($"Lowered control flow references unknown basic block \"{name}\".");
            }

            return block;
        }

        private void ConnectPhiIncomingValues()
        {
            foreach ((LLVMValueRef phi, LoweredBooleanPhiInstruction instruction) in this.pendingPhis)
            {
                this.cancellationToken.ThrowIfCancellationRequested();

                LLVMValueRef[] values = [.. instruction.Incoming.Select(incoming => this.EmitValue(incoming.Value))];
                LLVMBasicBlockRef[] predecessors = [.. instruction.Incoming.Select(incoming => this.GetBlock(incoming.Predecessor))];

                phi.AddIncoming(values, predecessors, (uint)values.Length);
            }
        }
    }
}
using LLVMSharp.Interop;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;

namespace Sushi.Backends.LLVM;

/// <summary>
/// Translates lowered Sushi functions into verified LLVM IR, including
/// checked arithmetic, integer comparisons, fixed-width bitwise operations,
/// guarded shifts, conditional control flow, and SSA merging.
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

        FunctionEmitter emitter = new(module, llvmFunction, function, builder, cancellationToken);
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
        private const int Int32Width = 32;

        private readonly LLVMModuleRef module;
        private readonly LLVMContextRef context;
        private readonly LLVMValueRef function;
        private readonly LoweredFunction loweredFunction;
        private readonly LLVMBuilderRef builder;
        private readonly CancellationToken cancellationToken;
        private readonly Dictionary<string, LLVMBasicBlockRef> blocks = new(StringComparer.Ordinal);
        private readonly Dictionary<string, LLVMBasicBlockRef> exitBlocks = new(StringComparer.Ordinal);
        private readonly Dictionary<int, (LLVMValueRef Value, IRType Type)> temporaries = [];
        private readonly List<(LLVMValueRef Phi, LoweredBooleanPhiInstruction Instruction)> pendingPhis = [];

        private LLVMBasicBlockRef arithmeticFailureBlock;
        private LLVMBasicBlockRef shiftFailureBlock;
        private int nextArithmeticBlockIndex;
        private int nextShiftBlockIndex;

        public FunctionEmitter(LLVMModuleRef module, LLVMValueRef function, LoweredFunction loweredFunction, LLVMBuilderRef builder, CancellationToken cancellationToken)
        {
            this.module = module;
            this.context = module.Context;
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

            this.EmitFailureBlock(this.arithmeticFailureBlock);
            this.EmitFailureBlock(this.shiftFailureBlock);
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
            this.exitBlocks.Add(block.Name, this.builder.InsertBlock);
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
                    this.EmitBooleanNegation(negation);
                    return;

                case LoweredBooleanBinaryInstruction binary:
                    this.EmitBooleanBinary(binary);
                    return;

                case LoweredIntegerComparisonInstruction comparison:
                    this.EmitIntegerComparison(comparison);
                    return;

                case LoweredIntegerNegateInstruction negation:
                    this.EmitIntegerNegation(negation);
                    return;

                case LoweredIntegerArithmeticInstruction arithmetic:
                    this.EmitIntegerArithmetic(arithmetic);
                    return;

                case LoweredIntegerComplementInstruction complement:
                    this.EmitIntegerComplement(complement);
                    return;

                case LoweredIntegerBitwiseInstruction bitwise:
                    this.EmitIntegerBitwise(bitwise);
                    return;

                case LoweredIntegerShiftInstruction shift:
                    this.EmitIntegerShift(shift);
                    return;

                default:
                    throw new NotSupportedException($"Lowered instruction type \"{instruction.GetType().Name}\" is not supported by the LLVM backend.");
            }
        }

        private void EmitBooleanNegation(LoweredBooleanNotInstruction instruction)
        {
            LLVMValueRef operand = this.EmitValue(instruction.Operand);
            LLVMValueRef trueValue = LLVMValueRef.CreateConstInt(this.context.Int1Type, 1UL);
            LLVMValueRef result = this.builder.BuildXor(operand, trueValue, $"sushi.bool.{instruction.Destination.Index}");

            this.RegisterTemporary(instruction.Destination, result);
        }

        private void EmitBooleanBinary(LoweredBooleanBinaryInstruction instruction)
        {
            LLVMValueRef left = this.EmitValue(instruction.Left);
            LLVMValueRef right = this.EmitValue(instruction.Right);
            string name = $"sushi.bool.{instruction.Destination.Index}";

            LLVMValueRef result = instruction.Operator switch
            {
                IRBooleanBinaryOperator.EagerAnd => this.builder.BuildAnd(left, right, name),
                IRBooleanBinaryOperator.EagerOr => this.builder.BuildOr(left, right, name),
                IRBooleanBinaryOperator.EagerXor => this.builder.BuildXor(left, right, name),
                _ => throw new NotSupportedException($"Boolean operator \"{instruction.Operator}\" requires different LLVM lowering.")
            };

            this.RegisterTemporary(instruction.Destination, result);
        }

        private void EmitIntegerComparison(LoweredIntegerComparisonInstruction instruction)
        {
            LLVMValueRef left = this.EmitValue(instruction.Left);
            LLVMValueRef right = this.EmitValue(instruction.Right);

            LLVMIntPredicate predicate = instruction.Operator switch
            {
                IRIntegerComparisonOperator.Equal => LLVMIntPredicate.LLVMIntEQ,
                IRIntegerComparisonOperator.NotEqual => LLVMIntPredicate.LLVMIntNE,
                IRIntegerComparisonOperator.LessThan => LLVMIntPredicate.LLVMIntSLT,
                IRIntegerComparisonOperator.LessThanOrEqual => LLVMIntPredicate.LLVMIntSLE,
                IRIntegerComparisonOperator.GreaterThan => LLVMIntPredicate.LLVMIntSGT,
                IRIntegerComparisonOperator.GreaterThanOrEqual => LLVMIntPredicate.LLVMIntSGE,
                _ => throw new NotSupportedException($"Integer comparison \"{instruction.Operator}\" is not supported by LLVM emission.")
            };

            LLVMValueRef result = this.builder.BuildICmp(predicate, left, right, $"sushi.bool.{instruction.Destination.Index}");

            this.RegisterTemporary(instruction.Destination, result);
        }

        private void EmitIntegerNegation(LoweredIntegerNegateInstruction instruction)
        {
            LLVMValueRef operand = this.ExtendInteger(this.EmitValue(instruction.Operand), $"sushi.integer.operand.{instruction.Destination.Index}");
            LLVMValueRef zero = LLVMValueRef.CreateConstInt(this.context.Int64Type, 0UL);
            LLVMValueRef exact = this.builder.BuildSub(zero, operand, $"sushi.integer.exact.{instruction.Destination.Index}");
            LLVMValueRef result = this.CheckIntegerResult(exact, instruction.Destination.Index);

            this.RegisterTemporary(instruction.Destination, result);
        }

        private void EmitIntegerArithmetic(LoweredIntegerArithmeticInstruction instruction)
        {
            LLVMValueRef left = this.ExtendInteger(this.EmitValue(instruction.Left), $"sushi.integer.left.{instruction.Destination.Index}");
            LLVMValueRef right = this.ExtendInteger(this.EmitValue(instruction.Right), $"sushi.integer.right.{instruction.Destination.Index}");

            if (instruction.Operator is IRIntegerBinaryOperator.Divide or IRIntegerBinaryOperator.Remainder)
            {
                LLVMValueRef zero = LLVMValueRef.CreateConstInt(this.context.Int64Type, 0UL);
                LLVMValueRef divisorIsZero = this.builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, right, zero, $"sushi.integer.zero.{instruction.Destination.Index}");

                this.GuardArithmeticFailure(divisorIsZero);
            }

            string name = $"sushi.integer.exact.{instruction.Destination.Index}";

            LLVMValueRef exact = instruction.Operator switch
            {
                IRIntegerBinaryOperator.Add => this.builder.BuildAdd(left, right, name),
                IRIntegerBinaryOperator.Subtract => this.builder.BuildSub(left, right, name),
                IRIntegerBinaryOperator.Multiply => this.builder.BuildMul(left, right, name),
                IRIntegerBinaryOperator.Divide => this.builder.BuildSDiv(left, right, name),
                IRIntegerBinaryOperator.Remainder => this.builder.BuildSRem(left, right, name),
                _ => throw new NotSupportedException($"Integer operator \"{instruction.Operator}\" is not supported by arithmetic emission.")
            };

            LLVMValueRef result = instruction.Operator is IRIntegerBinaryOperator.Remainder
                ? this.builder.BuildTrunc(exact, this.context.Int32Type, $"sushi.integer.{instruction.Destination.Index}")
                : this.CheckIntegerResult(exact, instruction.Destination.Index);

            this.RegisterTemporary(instruction.Destination, result);
        }

        private void EmitIntegerComplement(LoweredIntegerComplementInstruction instruction)
        {
            LLVMValueRef operand = this.EmitValue(instruction.Operand);
            LLVMValueRef mask = LLVMValueRef.CreateConstInt(this.context.Int32Type, uint.MaxValue);
            LLVMValueRef result = this.builder.BuildXor(operand, mask, $"sushi.integer.{instruction.Destination.Index}");

            this.RegisterTemporary(instruction.Destination, result);
        }

        private void EmitIntegerBitwise(LoweredIntegerBitwiseInstruction instruction)
        {
            LLVMValueRef left = this.EmitValue(instruction.Left);
            LLVMValueRef right = this.EmitValue(instruction.Right);
            string name = $"sushi.integer.{instruction.Destination.Index}";

            LLVMValueRef result = instruction.Operator switch
            {
                IRIntegerBinaryOperator.BitwiseAnd => this.builder.BuildAnd(left, right, name),
                IRIntegerBinaryOperator.BitwiseOr => this.builder.BuildOr(left, right, name),
                IRIntegerBinaryOperator.BitwiseXor => this.builder.BuildXor(left, right, name),
                _ => throw new NotSupportedException($"Integer operator \"{instruction.Operator}\" is not supported by bitwise emission.")
            };

            this.RegisterTemporary(instruction.Destination, result);
        }

        private void EmitIntegerShift(LoweredIntegerShiftInstruction instruction)
        {
            LLVMValueRef left = this.EmitValue(instruction.Left);
            LLVMValueRef count = this.EmitValue(instruction.Right);

            this.GuardShiftCount(count, instruction.Destination.Index);

            LLVMValueRef result;

            if (instruction.Operator is IRIntegerBinaryOperator.ShiftLeft)
            {
                LLVMValueRef wideLeft = this.ExtendInteger(left, $"sushi.shift.left.{instruction.Destination.Index}");
                LLVMValueRef wideCount = this.builder.BuildZExt(count, this.context.Int64Type, $"sushi.shift.count.{instruction.Destination.Index}");
                LLVMValueRef exact = this.builder.BuildShl(wideLeft, wideCount, $"sushi.shift.exact.{instruction.Destination.Index}");

                result = this.CheckIntegerResult(exact, instruction.Destination.Index);
            }
            else
            {
                result = this.builder.BuildAShr(left, count, $"sushi.integer.{instruction.Destination.Index}");
            }

            this.RegisterTemporary(instruction.Destination, result);
        }

        private void GuardShiftCount(LLVMValueRef count, int index)
        {
            LLVMValueRef zero = LLVMValueRef.CreateConstInt(this.context.Int32Type, 0UL);
            LLVMValueRef width = LLVMValueRef.CreateConstInt(this.context.Int32Type, Int32Width);

            LLVMValueRef negative = this.builder.BuildICmp(LLVMIntPredicate.LLVMIntSLT, count, zero, $"sushi.shift.negative.{index}");
            LLVMValueRef tooLarge = this.builder.BuildICmp(LLVMIntPredicate.LLVMIntSGE, count, width, $"sushi.shift.toolarge.{index}");
            LLVMValueRef invalid = this.builder.BuildOr(negative, tooLarge, $"sushi.shift.invalid.{index}");

            this.GuardShiftFailure(invalid);
        }

        private LLVMValueRef ExtendInteger(LLVMValueRef value, string name)
            => this.builder.BuildSExt(value, this.context.Int64Type, name);

        private LLVMValueRef CheckIntegerResult(LLVMValueRef exact, int index)
        {
            LLVMValueRef minimum = LLVMValueRef.CreateConstInt(this.context.Int64Type, unchecked((ulong)(long)int.MinValue));
            LLVMValueRef maximum = LLVMValueRef.CreateConstInt(this.context.Int64Type, (ulong)int.MaxValue);

            LLVMValueRef belowMinimum = this.builder.BuildICmp(LLVMIntPredicate.LLVMIntSLT, exact, minimum, $"sushi.integer.underflow.{index}");
            LLVMValueRef aboveMaximum = this.builder.BuildICmp(LLVMIntPredicate.LLVMIntSGT, exact, maximum, $"sushi.integer.overflow.{index}");
            LLVMValueRef invalid = this.builder.BuildOr(belowMinimum, aboveMaximum, $"sushi.integer.outofrange.{index}");

            this.GuardArithmeticFailure(invalid);

            return this.builder.BuildTrunc(exact, this.context.Int32Type, $"sushi.integer.{index}");
        }

        private void GuardArithmeticFailure(LLVMValueRef invalid)
            => this.GuardRuntimeFailure(invalid, ref this.arithmeticFailureBlock, "sushi.arithmetic.failure", $"sushi.arithmetic.continue.{this.nextArithmeticBlockIndex++}");

        private void GuardShiftFailure(LLVMValueRef invalid)
            => this.GuardRuntimeFailure(invalid, ref this.shiftFailureBlock, "sushi.shift.failure", $"sushi.shift.continue.{this.nextShiftBlockIndex++}");

        private void GuardRuntimeFailure(LLVMValueRef invalid, ref LLVMBasicBlockRef failureBlock, string failureName, string continuationName)
        {
            if (failureBlock.Handle == IntPtr.Zero)
            {
                failureBlock = this.function.AppendBasicBlock(failureName);
            }

            LLVMBasicBlockRef continuation = this.function.AppendBasicBlock(continuationName);

            this.builder.BuildCondBr(invalid, failureBlock, continuation);
            this.builder.PositionAtEnd(continuation);
        }

        private void EmitFailureBlock(LLVMBasicBlockRef failureBlock)
        {
            if (failureBlock.Handle == IntPtr.Zero)
            {
                return;
            }

            this.cancellationToken.ThrowIfCancellationRequested();
            this.builder.PositionAtEnd(failureBlock);

            LLVMTypeRef trapType = LLVMTypeRef.CreateFunction(this.context.VoidType, []);
            LLVMValueRef trap = this.module.GetNamedFunction("llvm.trap");

            if (trap.Handle == IntPtr.Zero)
            {
                trap = this.module.AddFunction("llvm.trap", trapType);
            }

            this.builder.BuildCall2(trapType, trap, Array.Empty<LLVMValueRef>());
            this.builder.BuildUnreachable();
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

        private LLVMBasicBlockRef GetExitBlock(string name)
        {
            if (!this.exitBlocks.TryGetValue(name, out LLVMBasicBlockRef block))
            {
                throw new InvalidOperationException($"Lowered control flow references an unknown predecessor block \"{name}\".");
            }

            return block;
        }

        private void ConnectPhiIncomingValues()
        {
            foreach ((LLVMValueRef phi, LoweredBooleanPhiInstruction instruction) in this.pendingPhis)
            {
                this.cancellationToken.ThrowIfCancellationRequested();

                LLVMValueRef[] values = [.. instruction.Incoming.Select(incoming => this.EmitValue(incoming.Value))];
                LLVMBasicBlockRef[] predecessors = [.. instruction.Incoming.Select(incoming => this.GetExitBlock(incoming.Predecessor))];

                phi.AddIncoming(values, predecessors, (uint)values.Length);
            }
        }
    }
}
namespace Sushi.Intermediate.Lowering;

/// <summary>
/// Lowers structured Sushi IR into explicit control flow while preserving
/// conditional evaluation, checked arithmetic, integer comparisons,
/// fixed-width operations, and function-call argument order.
/// </summary>
public sealed class IRFunctionLowerer
{
    /// <summary>
    /// Converts a structured function into lowered control-flow representation.
    /// </summary>
    /// <param name="function">
    /// The structured function to lower.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels lowering.
    /// </param>
    /// <returns>
    /// A function with ordered basic blocks, typed SSA values, and explicit edges.
    /// </returns>
    public static LoweredFunction Lower(IRFunction function, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(function);
        cancellationToken.ThrowIfCancellationRequested();

        LoweringContext context = new(cancellationToken);
        return context.Lower(function);
    }

    private sealed class LoweringContext
    {
        private readonly CancellationToken cancellationToken;
        private readonly List<BlockBuilder> blocks = [];
        private BlockBuilder current;
        private int nextBlockIndex;
        private int nextTemporaryIndex;

        public LoweringContext(CancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;
            this.current = this.CreateBlock("entry");
        }

        public LoweredFunction Lower(IRFunction function)
        {
            this.cancellationToken.ThrowIfCancellationRequested();

            if (function.ReturnType is IRType.Void)
            {
                this.LowerVoidBody(function.Body);
            }
            else
            {
                this.LowerValueReturningBody(function.Body);
            }

            LoweredBasicBlock[] loweredBlocks = [.. this.blocks.Select(block => block.Complete())];
            return new LoweredFunction(function.Accessibility, function.Name, function.ReturnType, function.Parameters, loweredBlocks);
        }

        private void LowerVoidBody(IRBlock body)
        {
            this.LowerVoidStatements(body);

            if (!this.current.IsTerminated)
            {
                this.current.Terminate(new LoweredReturnTerminator());
            }
        }

        private void LowerVoidStatements(IRBlock body)
        {
            foreach (IRStatement statement in body.Statements)
            {
                this.cancellationToken.ThrowIfCancellationRequested();

                if (this.current.IsTerminated)
                {
                    break;
                }

                switch (statement)
                {
                    case IRReturnStatement { Expression: null }:
                        this.current.Terminate(new LoweredReturnTerminator());
                        break;

                    case IRIfStatement conditional:
                        this.LowerVoidConditional(conditional);
                        break;

                    case IRVoidCallStatement call:
                        this.LowerVoidCall(call);
                        break;

                    default:
                        throw new NotSupportedException($"Statement type \"{statement.GetType().Name}\" is not supported in the current void lowering subset.");
                }
            }
        }

        private void LowerVoidCall(IRVoidCallStatement call)
        {
            List<LoweredCallArgument> arguments = this.LowerCallArguments(call.Arguments);
            this.current.Instructions.Add(new LoweredVoidCallInstruction(call.Symbol, call.ParameterTypes, arguments));
        }

        private LoweredValue LowerValueCall(IRCallExpression call)
        {
            List<LoweredCallArgument> arguments = this.LowerCallArguments(call.Arguments);
            LoweredTemporaryValue destination = this.CreateTemporary(call.Type);

            this.current.Instructions.Add(new LoweredValueCallInstruction(destination, call.Symbol, call.ParameterTypes, arguments));
            return destination;
        }

        private List<LoweredCallArgument> LowerCallArguments(IReadOnlyList<IRCallArgument> arguments)
        {
            List<LoweredCallArgument> lowered = [with(arguments.Count)];

            foreach (IRCallArgument argument in arguments)
            {
                this.cancellationToken.ThrowIfCancellationRequested();
                LoweredValue value = this.LowerExpression(argument.Expression);
                lowered.Add(new LoweredCallArgument(argument.ParameterIndex, value));
            }

            return lowered;
        }

        private void LowerVoidConditional(IRIfStatement conditional)
        {
            LoweredValue condition = this.LowerExpression(conditional.Condition);

            BlockBuilder thenBlock = this.CreateBlock("if.then");
            BlockBuilder? elseBlock = conditional.Else is null ? null : this.CreateBlock("if.else");
            BlockBuilder mergeBlock = this.CreateBlock("if.merge");

            this.current.Terminate(new LoweredConditionalBranchTerminator(
                condition, thenBlock.Name, elseBlock?.Name ?? mergeBlock.Name));

            this.current = thenBlock;
            this.LowerVoidStatements(conditional.Then);

            if (!this.current.IsTerminated)
            {
                this.current.Terminate(new LoweredBranchTerminator(mergeBlock.Name));
            }

            if (elseBlock is not null)
            {
                this.current = elseBlock;
                this.LowerVoidStatements(conditional.Else!);

                if (!this.current.IsTerminated)
                {
                    this.current.Terminate(new LoweredBranchTerminator(mergeBlock.Name));
                }
            }

            this.current = mergeBlock;
        }

        private void LowerValueReturningBody(IRBlock body)
        {
            if (body.Statements.Count != 1 || body.Statements[0] is not IRReturnStatement { Expression: not null } returnStatement)
            {
                throw new NotSupportedException("The current lowering pass requires exactly one value-returning statement in a non-void function.");
            }

            LoweredValue value = this.LowerExpression(returnStatement.Expression);
            this.current.Terminate(new LoweredReturnTerminator(value));
        }

        private LoweredValue LowerExpression(IRExpression expression)
        {
            this.cancellationToken.ThrowIfCancellationRequested();

            return expression switch
            {
                IRIntegerConstant integer => new LoweredIntegerConstant(integer.Value),
                IRBooleanConstant boolean => new LoweredBooleanConstant(boolean.Value),
                IRParameterReferenceExpression parameter => new LoweredParameterValue(parameter.Index, parameter.Type),
                IRCallExpression call => this.LowerValueCall(call),
                IRBooleanNotExpression negation => this.LowerBooleanNegation(negation),
                IRBooleanBinaryExpression binary => this.LowerBooleanBinary(binary),
                IRIntegerUnaryExpression unary => this.LowerIntegerUnary(unary),
                IRIntegerBinaryExpression binary => this.LowerIntegerBinary(binary),
                IRIntegerComparisonExpression comparison => this.LowerIntegerComparison(comparison),
                IRIntegerRangeComparisonExpression range => this.LowerIntegerRangeComparison(range),
                _ => throw new NotSupportedException($"Structured IR expression type \"{expression.GetType().Name}\" is not supported by lowering.")
            };
        }

        private LoweredValue LowerBooleanNegation(IRBooleanNotExpression expression)
        {
            LoweredValue operand = this.LowerExpression(expression.Operand);

            if (operand is LoweredBooleanConstant constant)
            {
                return new LoweredBooleanConstant(!constant.Value);
            }

            LoweredTemporaryValue destination = this.CreateTemporary(IRType.Bool);
            this.current.Instructions.Add(new LoweredBooleanNotInstruction(destination, operand));

            return destination;
        }

        private LoweredValue LowerBooleanBinary(IRBooleanBinaryExpression expression)
            => expression.Operator switch
            {
                IRBooleanBinaryOperator.LogicalAnd or IRBooleanBinaryOperator.LogicalOr => this.LowerLogical(expression),
                IRBooleanBinaryOperator.EagerAnd or IRBooleanBinaryOperator.EagerOr or IRBooleanBinaryOperator.EagerXor => this.LowerEager(expression),
                _ => throw new NotSupportedException($"Boolean operator \"{expression.Operator}\" is not supported by lowering.")
            };

        private LoweredValue LowerLogical(IRBooleanBinaryExpression expression)
        {
            LoweredValue left = this.LowerExpression(expression.Left);

            if (left is LoweredBooleanConstant constant)
            {
                if (expression.Operator is IRBooleanBinaryOperator.LogicalAnd && !constant.Value)
                {
                    return new LoweredBooleanConstant(false);
                }

                if (expression.Operator is IRBooleanBinaryOperator.LogicalOr && constant.Value)
                {
                    return new LoweredBooleanConstant(true);
                }

                return this.LowerExpression(expression.Right);
            }

            bool shortCircuitValue = expression.Operator is IRBooleanBinaryOperator.LogicalOr;

            BlockBuilder rightBlock = this.CreateBlock("logical.right");
            BlockBuilder skipBlock = this.CreateBlock("logical.skip");
            BlockBuilder mergeBlock = this.CreateBlock("logical.merge");

            string trueTarget = shortCircuitValue ? skipBlock.Name : rightBlock.Name;
            string falseTarget = shortCircuitValue ? rightBlock.Name : skipBlock.Name;

            this.current.Terminate(new LoweredConditionalBranchTerminator(left, trueTarget, falseTarget));
            skipBlock.Terminate(new LoweredBranchTerminator(mergeBlock.Name));

            this.current = rightBlock;

            LoweredValue right = this.LowerExpression(expression.Right);
            BlockBuilder rightPredecessor = this.current;

            rightPredecessor.Terminate(new LoweredBranchTerminator(mergeBlock.Name));
            this.current = mergeBlock;

            LoweredTemporaryValue destination = this.CreateTemporary(IRType.Bool);

            this.current.Instructions.Add(new LoweredBooleanPhiInstruction(destination,
            [
                new LoweredPhiIncoming(skipBlock.Name, new LoweredBooleanConstant(shortCircuitValue)),
                new LoweredPhiIncoming(rightPredecessor.Name, right)
            ]));

            return destination;
        }

        private LoweredValue LowerEager(IRBooleanBinaryExpression expression)
        {
            LoweredValue left = this.LowerExpression(expression.Left);
            LoweredValue right = this.LowerExpression(expression.Right);

            if (left is LoweredBooleanConstant leftConstant && right is LoweredBooleanConstant rightConstant)
            {
                bool value = expression.Operator switch
                {
                    IRBooleanBinaryOperator.EagerAnd => leftConstant.Value & rightConstant.Value,
                    IRBooleanBinaryOperator.EagerOr => leftConstant.Value | rightConstant.Value,
                    IRBooleanBinaryOperator.EagerXor => leftConstant.Value ^ rightConstant.Value,
                    _ => throw new InvalidOperationException("Unexpected eager Boolean operator.")
                };

                return new LoweredBooleanConstant(value);
            }

            LoweredTemporaryValue destination = this.CreateTemporary(IRType.Bool);
            this.current.Instructions.Add(new LoweredBooleanBinaryInstruction(destination, expression.Operator, left, right));

            return destination;
        }

        private LoweredValue LowerIntegerUnary(IRIntegerUnaryExpression expression)
        {
            if (expression.Operator is IRIntegerUnaryOperator.Plus)
            {
                return this.LowerExpression(expression.Operand);
            }

            if (expression.Operator is not (IRIntegerUnaryOperator.Negate or IRIntegerUnaryOperator.Complement))
            {
                throw new NotSupportedException($"Integer unary operator \"{expression.Operator}\" is not supported by lowering.");
            }

            LoweredValue operand = this.LowerExpression(expression.Operand);
            LoweredTemporaryValue destination = this.CreateTemporary(IRType.Int32);

            LoweredInstruction instruction = expression.Operator switch
            {
                IRIntegerUnaryOperator.Negate => new LoweredIntegerNegateInstruction(destination, operand),
                IRIntegerUnaryOperator.Complement => new LoweredIntegerComplementInstruction(destination, operand),
                _ => throw new InvalidOperationException("Unexpected integer unary operation.")
            };

            this.current.Instructions.Add(instruction);
            return destination;
        }

        private LoweredValue LowerIntegerBinary(IRIntegerBinaryExpression expression)
        {
            IRIntegerBinaryOperator operation = expression.Operator;

            if (!Enum.IsDefined(operation))
            {
                throw new NotSupportedException($"Integer binary operator \"{operation}\" is not supported by lowering.");
            }

            LoweredValue left = this.LowerExpression(expression.Left);
            LoweredValue right = this.LowerExpression(expression.Right);
            LoweredTemporaryValue destination = this.CreateTemporary(IRType.Int32);

            LoweredInstruction instruction = operation switch
            {
                IRIntegerBinaryOperator.Add or IRIntegerBinaryOperator.Subtract or IRIntegerBinaryOperator.Multiply or IRIntegerBinaryOperator.Divide or IRIntegerBinaryOperator.Remainder
                    => new LoweredIntegerArithmeticInstruction(destination, operation, left, right),

                IRIntegerBinaryOperator.BitwiseAnd or IRIntegerBinaryOperator.BitwiseOr or IRIntegerBinaryOperator.BitwiseXor
                    => new LoweredIntegerBitwiseInstruction(destination, operation, left, right),

                IRIntegerBinaryOperator.ShiftLeft or IRIntegerBinaryOperator.ShiftRight
                    => new LoweredIntegerShiftInstruction(destination, operation, left, right),

                _ => throw new NotSupportedException($"Integer binary operator \"{operation}\" is not supported by lowering.")
            };

            this.current.Instructions.Add(instruction);
            return destination;
        }

        private LoweredValue LowerIntegerComparison(IRIntegerComparisonExpression expression)
        {
            LoweredValue left = this.LowerExpression(expression.Left);
            LoweredValue right = this.LowerExpression(expression.Right);

            if (left is LoweredIntegerConstant leftConstant && right is LoweredIntegerConstant rightConstant)
            {
                return new LoweredBooleanConstant(EvaluateIntegerComparison(expression.Operator, leftConstant.Value, rightConstant.Value));
            }

            LoweredTemporaryValue destination = this.CreateTemporary(IRType.Bool);
            this.current.Instructions.Add(new LoweredIntegerComparisonInstruction(destination, expression.Operator, left, right));

            return destination;
        }

        private LoweredValue LowerIntegerRangeComparison(IRIntegerRangeComparisonExpression expression)
        {
            _ = this.LowerExpression(expression.Operand);
            return new LoweredBooleanConstant(expression.Result);
        }

        private static bool EvaluateIntegerComparison(IRIntegerComparisonOperator operation, int left, int right)
            => operation switch
            {
                IRIntegerComparisonOperator.Equal => left == right,
                IRIntegerComparisonOperator.NotEqual => left != right,
                IRIntegerComparisonOperator.LessThan => left < right,
                IRIntegerComparisonOperator.LessThanOrEqual => left <= right,
                IRIntegerComparisonOperator.GreaterThan => left > right,
                IRIntegerComparisonOperator.GreaterThanOrEqual => left >= right,
                _ => throw new NotSupportedException($"Integer comparison \"{operation}\" is not supported by lowering.")
            };

        private LoweredTemporaryValue CreateTemporary(IRType type)
            => new(this.nextTemporaryIndex++, type);

        private BlockBuilder CreateBlock(string prefix)
        {
            string name = this.blocks.Count == 0 ? "entry" : $"{prefix}.{this.nextBlockIndex++}";
            BlockBuilder block = new(name);

            this.blocks.Add(block);
            return block;
        }
    }

    private sealed class BlockBuilder(string name)
    {
        private LoweredTerminator? terminator;

        public string Name { get; } = name;

        public List<LoweredInstruction> Instructions { get; } = [];

        public bool IsTerminated => this.terminator is not null;

        public void Terminate(LoweredTerminator value)
        {
            ArgumentNullException.ThrowIfNull(value);

            if (this.terminator is not null)
            {
                throw new InvalidOperationException($"Basic block \"{this.Name}\" already has a terminator.");
            }

            this.terminator = value;
        }

        public LoweredBasicBlock Complete()
            => new(this.Name, this.Instructions, this.terminator ?? throw new InvalidOperationException($"Basic block \"{this.Name}\" has no terminator."));
    }
}
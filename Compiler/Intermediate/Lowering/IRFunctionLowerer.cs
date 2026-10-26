namespace Sushi.Intermediate.Lowering;

/// <summary>
/// Lowers structured Sushi IR into explicit control flow while preserving
/// conditional evaluation, checked arithmetic, and constant results.
/// </summary>
public sealed class IRFunctionLowerer
{
    /// <summary>
    /// Converts a structured IR function into its lowered control-flow representation.
    /// </summary>
    /// <param name="function">
    /// The structured function to lower.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels lowering.
    /// </param>
    /// <returns>
    /// A function containing ordered basic blocks, typed SSA values, and explicit control-flow edges.
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

            if (function.Body.Statements.Count != 1 || function.Body.Statements[0] is not IRReturnStatement returnStatement)
            {
                throw new NotSupportedException("The current lowering pass supports exactly one return statement in a function body.");
            }

            LoweredValue value = this.LowerExpression(returnStatement.Expression);
            this.current.Terminate(new LoweredReturnTerminator(value));

            LoweredBasicBlock[] loweredBlocks = [.. this.blocks.Select(block => block.Complete())];

            return new LoweredFunction(function.Accessibility, function.Name, function.ReturnType, function.Parameters, loweredBlocks);
        }

        private LoweredValue LowerExpression(IRExpression expression)
        {
            this.cancellationToken.ThrowIfCancellationRequested();

            return expression switch
            {
                IRIntegerConstant integer => new LoweredIntegerConstant(integer.Value),
                IRBooleanConstant boolean => new LoweredBooleanConstant(boolean.Value),
                IRParameterReferenceExpression parameter => new LoweredParameterValue(parameter.Index, parameter.Type),
                IRBooleanNotExpression negation => this.LowerBooleanNegation(negation),
                IRBooleanBinaryExpression binary => this.LowerBooleanBinary(binary),
                IRIntegerUnaryExpression unary => this.LowerIntegerUnary(unary),
                IRIntegerBinaryExpression binary => this.LowerIntegerBinary(binary),
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

            if (expression.Operator is not IRIntegerUnaryOperator.Negate)
            {
                throw new NotSupportedException($"Integer unary operator \"{expression.Operator}\" is not supported by lowering.");
            }

            LoweredValue operand = this.LowerExpression(expression.Operand);
            LoweredTemporaryValue destination = this.CreateTemporary(IRType.Int32);

            this.current.Instructions.Add(new LoweredIntegerNegateInstruction(destination, operand));

            return destination;
        }

        private LoweredValue LowerIntegerBinary(IRIntegerBinaryExpression expression)
        {
            if (expression.Operator is not (IRIntegerBinaryOperator.Add or IRIntegerBinaryOperator.Subtract or IRIntegerBinaryOperator.Multiply or IRIntegerBinaryOperator.Divide or IRIntegerBinaryOperator.Remainder))
            {
                throw new NotSupportedException($"Integer binary operator \"{expression.Operator}\" is not supported by lowering.");
            }

            LoweredValue left = this.LowerExpression(expression.Left);
            LoweredValue right = this.LowerExpression(expression.Right);
            LoweredTemporaryValue destination = this.CreateTemporary(IRType.Int32);

            this.current.Instructions.Add(new LoweredIntegerArithmeticInstruction(destination, expression.Operator, left, right));

            return destination;
        }

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
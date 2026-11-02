using System.Globalization;
using System.Text;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;

namespace Sushi.Inspection;

/// <summary>
/// Formats lowered IR and records semantic navigation metadata as text is written.
/// Basic blocks, storage slots, and SSA values retain separate function-scoped identities.
/// </summary>
public static class LoweredIRFormatter
{
    /// <summary>
    /// Formats an existing lowered function without modifying its representation.
    /// </summary>
    /// <param name="function">
    /// The function with its authoritative compiler-generated symbol.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels catalog construction and formatting before a partial document is returned.
    /// </param>
    /// <returns>
    /// The complete lowered representation and its UTF-16 semantic occurrences.
    /// </returns>
    public static InspectionDocument Format(LoweredFunction function, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(function);
        cancellationToken.ThrowIfCancellationRequested();

        Writer writer = new(function, cancellationToken);
        return writer.Format();
    }

    private sealed class Writer(LoweredFunction function, CancellationToken cancellationToken)
    {
        private readonly LoweredFunction function = function;
        private readonly CancellationToken cancellationToken = cancellationToken;
        private readonly StringBuilder text = new();
        private readonly List<InspectionSymbolOccurrence> occurrences = [];
        private readonly Dictionary<string, int> blocks = new(StringComparer.Ordinal);
        private readonly Dictionary<int, IRType> temporaries = [];
        private readonly SortedDictionary<int, IRType> locals = [];

        public InspectionDocument Format()
        {
            this.BuildCatalogs();
            this.text.Append(GetAccessibilityName(this.function.Accessibility)).Append(" function ")
                .Append(GetTypeName(this.function.ReturnType)).Append(' ');
            this.WriteSymbol(this.function.Name, InspectionSymbolKind.Function, this.function.Name, null, true);
            this.text.Append('(');

            for (int i = 0; i < this.function.Parameters.Count; i++)
            {
                this.cancellationToken.ThrowIfCancellationRequested();
                if (i > 0)
                {
                    this.text.Append(", ");
                }

                IRParameter parameter = this.function.Parameters[i];
                this.text.Append(GetTypeName(parameter.Type)).Append(' ');
                this.WriteSymbol(parameter.Name, InspectionSymbolKind.Parameter, this.function.Name, i, true);
            }

            this.text.Append(")\n{\n");
            foreach (KeyValuePair<int, IRType> local in this.locals)
            {
                this.cancellationToken.ThrowIfCancellationRequested();
                this.text.Append("    slot ").Append(GetTypeName(local.Value)).Append(' ');
                this.WriteLocal(local.Key, true);
                this.text.Append(";\n");
            }

            for (int i = 0; i < this.function.Blocks.Count; i++)
            {
                this.cancellationToken.ThrowIfCancellationRequested();
                LoweredBasicBlock block = this.function.Blocks[i];
                this.text.Append("    ");
                this.WriteSymbol(block.Name, InspectionSymbolKind.BasicBlock, this.function.Name, i, true);
                this.text.Append(":\n");
                foreach (LoweredInstruction instruction in block.Instructions)
                {
                    this.WriteInstruction(instruction);
                }

                this.WriteTerminator(block.Terminator);
            }

            this.text.Append("}\n");
            this.cancellationToken.ThrowIfCancellationRequested();
            return new InspectionDocument(InspectionStage.LoweredIR, this.function.Name, this.text.ToString(), this.occurrences);
        }

        private void BuildCatalogs()
        {
            for (int i = 0; i < this.function.Blocks.Count; i++)
            {
                this.cancellationToken.ThrowIfCancellationRequested();
                LoweredBasicBlock block = this.function.Blocks[i];
                if (!this.blocks.TryAdd(block.Name, i))
                {
                    throw new InvalidOperationException("Basic-block names must be unique within the inspected function.");
                }

                foreach (LoweredInstruction instruction in block.Instructions)
                {
                    this.cancellationToken.ThrowIfCancellationRequested();
                    LoweredTemporaryValue? destination = GetDestination(instruction);
                    if (destination is not null && !this.temporaries.TryAdd(destination.Index, destination.Type))
                    {
                        throw new InvalidOperationException($"Temporary index {destination.Index} has duplicate definitions.");
                    }

                    switch (instruction)
                    {
                        case LoweredLocalLoadInstruction load:
                            this.AddLocal(load.Index, load.Destination.Type);
                            break;
                        case LoweredLocalStoreInstruction store:
                            this.AddLocal(store.Index, store.Type);
                            break;
                    }
                }
            }
        }

        private void AddLocal(int index, IRType type)
        {
            if (this.locals.TryGetValue(index, out IRType existing) && existing != type)
            {
                throw new InvalidOperationException($"Local slot {index} has conflicting types.");
            }

            this.locals[index] = type;
        }

        private void WriteInstruction(LoweredInstruction instruction)
        {
            this.cancellationToken.ThrowIfCancellationRequested();
            this.text.Append("        ");
            LoweredTemporaryValue? destination = GetDestination(instruction);
            if (destination is not null)
            {
                this.text.Append(GetTypeName(destination.Type)).Append(' ');
                this.WriteTemporary(destination, true);
                this.text.Append(" = ");
            }

            switch (instruction)
            {
                case LoweredBooleanBinaryInstruction binary:
                    this.WriteBinary("eager " + GetBooleanOperator(binary.Operator), binary.Left, binary.Right);
                    break;
                case LoweredIntegerArithmeticInstruction binary:
                    this.WriteBinary("checked " + GetIntegerOperator(binary.Operator), binary.Left, binary.Right);
                    break;
                case LoweredIntegerBitwiseInstruction binary:
                    this.WriteBinary("bitwise " + GetIntegerOperator(binary.Operator), binary.Left, binary.Right);
                    break;
                case LoweredIntegerComparisonInstruction binary:
                    this.WriteBinary("compare " + GetComparisonOperator(binary.Operator), binary.Left, binary.Right);
                    break;
                case LoweredIntegerShiftInstruction binary:
                    this.WriteBinary("checked_shift " + GetIntegerOperator(binary.Operator), binary.Left, binary.Right);
                    break;
                case LoweredBooleanNotInstruction unary:
                    this.text.Append("not ");
                    this.WriteValue(unary.Operand);
                    break;
                case LoweredIntegerNegateInstruction unary:
                    this.text.Append("checked_negate ");
                    this.WriteValue(unary.Operand);
                    break;
                case LoweredIntegerComplementInstruction unary:
                    this.text.Append("complement ");
                    this.WriteValue(unary.Operand);
                    break;
                case LoweredBooleanPhiInstruction phi:
                    this.text.Append("phi ");
                    for (int i = 0; i < phi.Incoming.Count; i++)
                    {
                        this.cancellationToken.ThrowIfCancellationRequested();
                        if (i > 0)
                        {
                            this.text.Append(", ");
                        }

                        LoweredPhiIncoming incoming = phi.Incoming[i];
                        this.text.Append('[');
                        this.WriteValue(incoming.Value);
                        this.text.Append(" from ");
                        this.WriteBlockReference(incoming.Predecessor);
                        this.text.Append(']');
                    }

                    break;
                case LoweredLocalLoadInstruction load:
                    this.text.Append("load ");
                    this.WriteLocal(load.Index, false);
                    break;
                case LoweredLocalStoreInstruction store:
                    this.text.Append("store ").Append(GetTypeName(store.Type)).Append(' ');
                    this.WriteLocal(store.Index, false);
                    this.text.Append(" = ");
                    this.WriteValue(store.Value);
                    break;
                case LoweredValueCallInstruction call:
                    this.WriteCall(call.Symbol, call.Destination.Type, call.ParameterTypes, call.Arguments);
                    break;
                case LoweredVoidCallInstruction call:
                    this.WriteCall(call.Symbol, IRType.Void, call.ParameterTypes, call.Arguments);
                    break;
                default:
                    throw new NotSupportedException($"Lowered IR instruction \"{instruction.GetType().Name}\" cannot be formatted.");
            }

            this.text.Append(";\n");
        }

        private void WriteBinary(string operation, LoweredValue left, LoweredValue right)
        {
            this.text.Append(operation).Append(' ');
            this.WriteValue(left);
            this.text.Append(", ");
            this.WriteValue(right);
        }

        private void WriteTerminator(LoweredTerminator terminator)
        {
            this.cancellationToken.ThrowIfCancellationRequested();
            this.text.Append("        ");
            switch (terminator)
            {
                case LoweredBranchTerminator branch:
                    this.text.Append("branch ");
                    this.WriteBlockReference(branch.Target);
                    break;
                case LoweredConditionalBranchTerminator branch:
                    this.text.Append("branch ");
                    this.WriteValue(branch.Condition);
                    this.text.Append(" true: ");
                    this.WriteBlockReference(branch.TrueTarget);
                    this.text.Append(" false: ");
                    this.WriteBlockReference(branch.FalseTarget);
                    break;
                case LoweredReturnTerminator returning:
                    this.text.Append("return");
                    if (returning.Value is not null)
                    {
                        this.text.Append(' ');
                        this.WriteValue(returning.Value);
                    }

                    break;
                default:
                    throw new NotSupportedException($"Lowered IR terminator \"{terminator.GetType().Name}\" cannot be formatted.");
            }

            this.text.Append(";\n");
        }

        private void WriteValue(LoweredValue value)
        {
            this.cancellationToken.ThrowIfCancellationRequested();
            switch (value)
            {
                case LoweredIntegerConstant integer:
                    this.text.Append(integer.Value.ToString(CultureInfo.InvariantCulture));
                    break;
                case LoweredBooleanConstant boolean:
                    this.text.Append(boolean.Value ? "true" : "false");
                    break;
                case LoweredTemporaryValue temporary:
                    this.WriteTemporary(temporary, false);
                    break;
                case LoweredParameterValue parameter:
                    if (parameter.Index >= this.function.Parameters.Count
                        || this.function.Parameters[parameter.Index].Type != parameter.Type)
                    {
                        throw new InvalidOperationException($"Parameter index {parameter.Index} has no matching typed definition.");
                    }

                    this.WriteSymbol(this.function.Parameters[parameter.Index].Name, InspectionSymbolKind.Parameter,
                        this.function.Name, parameter.Index, false);
                    break;
                default:
                    throw new NotSupportedException($"Lowered IR value \"{value.GetType().Name}\" cannot be formatted.");
            }
        }

        private void WriteTemporary(LoweredTemporaryValue value, bool isDefinition)
        {
            if (!this.temporaries.TryGetValue(value.Index, out IRType type) || type != value.Type)
            {
                throw new InvalidOperationException($"Temporary index {value.Index} has no matching typed definition.");
            }

            this.WriteSymbol("%temp" + value.Index.ToString(CultureInfo.InvariantCulture), InspectionSymbolKind.Temporary,
                this.function.Name, value.Index, isDefinition);
        }

        private void WriteLocal(int index, bool isDefinition) => this.WriteSymbol(
            "%local" + index.ToString(CultureInfo.InvariantCulture), InspectionSymbolKind.Local, this.function.Name, index, isDefinition);

        private void WriteBlockReference(string name)
        {
            if (!this.blocks.TryGetValue(name, out int index))
            {
                throw new InvalidOperationException($"Basic-block target \"{name}\" is not in the inspected function.");
            }

            this.WriteSymbol(name, InspectionSymbolKind.BasicBlock, this.function.Name, index, false);
        }

        private void WriteCall(string symbol, IRType returnType, IReadOnlyList<IRType> parameterTypes, IReadOnlyList<LoweredCallArgument> arguments)
        {
            this.text.Append("call ").Append(GetTypeName(returnType)).Append(' ');
            this.WriteSymbol(symbol, InspectionSymbolKind.Function, symbol, null, false);
            this.text.Append('(');
            HashSet<int> supplied = [];
            for (int i = 0; i < arguments.Count; i++)
            {
                this.cancellationToken.ThrowIfCancellationRequested();
                LoweredCallArgument argument = arguments[i];
                if (argument is null || argument.ParameterIndex < 0 || argument.ParameterIndex >= parameterTypes.Count
                    || !supplied.Add(argument.ParameterIndex) || argument.Value is null
                    || argument.Value.Type != parameterTypes[argument.ParameterIndex])
                {
                    throw new InvalidOperationException("Lowered call arguments must uniquely match the selected signature.");
                }

                if (i > 0)
                {
                    this.text.Append(", ");
                }

                this.text.Append(GetTypeName(parameterTypes[argument.ParameterIndex])).Append(' ');
                this.WriteSymbol("arg" + argument.ParameterIndex.ToString(CultureInfo.InvariantCulture),
                    InspectionSymbolKind.Parameter, symbol, argument.ParameterIndex, false);
                this.text.Append(": ");
                this.WriteValue(argument.Value);
            }

            if (supplied.Count != parameterTypes.Count)
            {
                throw new InvalidOperationException("Lowered call arguments must supply every parameter.");
            }

            this.text.Append(')');
        }

        private void WriteSymbol(string name, InspectionSymbolKind kind, string symbol, int? index, bool isDefinition)
        {
            this.cancellationToken.ThrowIfCancellationRequested();
            int start = this.text.Length;
            this.text.Append(name);
            this.occurrences.Add(new InspectionSymbolOccurrence(new InspectionSymbolIdentity(kind, symbol, index),
                start, this.text.Length - start, isDefinition));
        }

        private static LoweredTemporaryValue? GetDestination(LoweredInstruction instruction) => instruction switch
        {
            LoweredBooleanBinaryInstruction value => value.Destination,
            LoweredBooleanNotInstruction value => value.Destination,
            LoweredBooleanPhiInstruction value => value.Destination,
            LoweredIntegerArithmeticInstruction value => value.Destination,
            LoweredIntegerBitwiseInstruction value => value.Destination,
            LoweredIntegerComparisonInstruction value => value.Destination,
            LoweredIntegerComplementInstruction value => value.Destination,
            LoweredIntegerNegateInstruction value => value.Destination,
            LoweredIntegerShiftInstruction value => value.Destination,
            LoweredLocalLoadInstruction value => value.Destination,
            LoweredValueCallInstruction value => value.Destination,
            LoweredLocalStoreInstruction or LoweredVoidCallInstruction => null,
            null => throw new InvalidOperationException("Lowered instructions must not be null."),
            _ => throw new NotSupportedException($"Lowered IR instruction \"{instruction.GetType().Name}\" cannot be formatted.")
        };

        private static string GetAccessibilityName(IRAccessibility accessibility) => accessibility switch
        {
            IRAccessibility.Public => "public",
            IRAccessibility.Internal => "internal",
            IRAccessibility.Package => "package",
            IRAccessibility.Protected => "protected",
            IRAccessibility.Private => "private",
            _ => throw new NotSupportedException($"IR accessibility \"{accessibility}\" cannot be formatted.")
        };

        private static string GetTypeName(IRType type) => type switch
        {
            IRType.Int32 => "int32",
            IRType.Bool => "bool",
            IRType.Void => "void",
            _ => throw new NotSupportedException($"IR type \"{type}\" cannot be formatted.")
        };

        private static string GetBooleanOperator(IRBooleanBinaryOperator operation) => operation switch
        {
            IRBooleanBinaryOperator.EagerAnd => "&",
            IRBooleanBinaryOperator.EagerOr => "|",
            IRBooleanBinaryOperator.EagerXor => "^",
            _ => throw new NotSupportedException($"Boolean IR operator \"{operation}\" cannot be formatted.")
        };

        private static string GetIntegerOperator(IRIntegerBinaryOperator operation) => operation switch
        {
            IRIntegerBinaryOperator.Add => "+",
            IRIntegerBinaryOperator.Subtract => "-",
            IRIntegerBinaryOperator.Multiply => "*",
            IRIntegerBinaryOperator.Divide => "/",
            IRIntegerBinaryOperator.Remainder => "%",
            IRIntegerBinaryOperator.BitwiseAnd => "&",
            IRIntegerBinaryOperator.BitwiseOr => "|",
            IRIntegerBinaryOperator.BitwiseXor => "^",
            IRIntegerBinaryOperator.ShiftLeft => "<<",
            IRIntegerBinaryOperator.ShiftRight => ">>",
            _ => throw new NotSupportedException($"Binary integer IR operator \"{operation}\" cannot be formatted.")
        };

        private static string GetComparisonOperator(IRIntegerComparisonOperator operation) => operation switch
        {
            IRIntegerComparisonOperator.Equal => "==",
            IRIntegerComparisonOperator.NotEqual => "!=",
            IRIntegerComparisonOperator.LessThan => "<",
            IRIntegerComparisonOperator.LessThanOrEqual => "<=",
            IRIntegerComparisonOperator.GreaterThan => ">",
            IRIntegerComparisonOperator.GreaterThanOrEqual => ">=",
            _ => throw new NotSupportedException($"Integer comparison IR operator \"{operation}\" cannot be formatted.")
        };
    }
}
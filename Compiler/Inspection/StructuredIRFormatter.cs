using System.Globalization;
using System.Text;
using Sushi.Intermediate;

namespace Sushi.Inspection;

/// <summary>
/// Formats structured Sushi IR into readable, deterministic text while
/// recording semantic definitions and references in the same writing pass.
/// The formatter does not modify IR or perform semantic name resolution.
/// </summary>
public static class StructuredIRFormatter
{
    /// <summary>
    /// Formats one structured IR function and its associated navigation metadata.
    /// </summary>
    /// <param name="function">
    /// The function whose existing IR is inspected. Its name is the function
    /// symbol used to identify the definition in the current compilation.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels formatting before a partial result is returned.
    /// </param>
    /// <returns>
    /// A complete inspection document containing text and semantic occurrences.
    /// </returns>
    public static InspectionDocument Format(IRFunction function, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(function);
        cancellationToken.ThrowIfCancellationRequested();

        Writer writer = new(function, cancellationToken);
        return writer.Format();
    }

    private sealed class Writer(IRFunction function, CancellationToken cancellationToken)
    {
        private readonly IRFunction function = function;
        private readonly CancellationToken cancellationToken = cancellationToken;
        private readonly StringBuilder text = new();
        private readonly List<InspectionSymbolOccurrence> occurrences = [];

        private int indentation;

        public InspectionDocument Format()
        {
            this.text.Append(GetAccessibilityName(this.function.Accessibility))
                .Append(" function ")
                .Append(GetTypeName(this.function.ReturnType))
                .Append(' ');

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
                this.WriteSymbol(parameter.Name, InspectionSymbolKind.Parameter, this.function.Name, parameter.Index, true);
            }

            this.text.Append(")\n");
            this.WriteBlock(this.function.Body);

            return new InspectionDocument(InspectionStage.StructuredIR, this.function.Name, this.text.ToString(), this.occurrences);
        }

        private void WriteBlock(IRBlock block)
        {
            this.WriteIndentation();
            this.text.Append("{\n");
            this.indentation++;

            foreach (IRStatement statement in block.Statements)
            {
                this.WriteStatement(statement);
            }

            this.indentation--;
            this.WriteIndentation();
            this.text.Append("}\n");
        }

        private void WriteStatement(IRStatement statement)
        {
            this.cancellationToken.ThrowIfCancellationRequested();
            this.WriteIndentation();

            switch (statement)
            {
                case IRReturnStatement returning:
                    this.text.Append("return");

                    if (returning.Expression is not null)
                    {
                        this.text.Append(' ');
                        this.WriteExpression(returning.Expression);
                    }

                    this.text.Append(";\n");
                    break;

                case IRLocalDeclarationStatement declaration:
                    this.text.Append("local ").Append(GetTypeName(declaration.Type)).Append(' ');
                    this.WriteLocal(declaration.Index, true);
                    this.text.Append(" = ");
                    this.WriteExpression(declaration.Initializer);
                    this.text.Append(";\n");
                    break;

                case IRLocalAssignmentStatement assignment:
                    this.text.Append("store ").Append(GetTypeName(assignment.Type)).Append(' ');
                    this.WriteLocal(assignment.Index, false);
                    this.text.Append(" = ");
                    this.WriteExpression(assignment.Value);
                    this.text.Append(";\n");
                    break;

                case IRIntegerUpdateStatement update:
                    this.text.Append("update ");
                    this.WriteExpression(update.Expression);
                    this.text.Append(";\n");
                    break;

                case IRVoidCallStatement call:
                    this.WriteCall(call.Symbol, IRType.Void, call.ParameterTypes, call.Arguments);
                    this.text.Append(";\n");
                    break;

                case IRIfStatement conditional:
                    this.text.Append("if (");
                    this.WriteExpression(conditional.Condition);
                    this.text.Append(")\n");
                    this.WriteBlock(conditional.Then);

                    if (conditional.Else is not null)
                    {
                        this.WriteIndentation();
                        this.text.Append("else\n");
                        this.WriteBlock(conditional.Else);
                    }

                    break;

                case IRWhileStatement loop:
                    this.text.Append("while (");
                    this.WriteExpression(loop.Condition);
                    this.text.Append(")\n");
                    this.WriteBlock(loop.Body);
                    break;

                case IRDoWhileStatement loop:
                    this.text.Append("do\n");
                    this.WriteBlock(loop.Body);
                    this.WriteIndentation();
                    this.text.Append("while (");
                    this.WriteExpression(loop.Condition);
                    this.text.Append(");\n");
                    break;

                case IRForStatement loop:
                    this.WriteFor(loop);
                    break;

                case IRLoopTransferStatement transfer:
                    this.text.Append(transfer.IsBreak ? "break depth " : "continue depth ")
                        .Append(transfer.Depth.ToString(CultureInfo.InvariantCulture))
                        .Append(";\n");
                    break;

                default:
                    throw new NotSupportedException($"Structured IR statement \"{statement.GetType().Name}\" cannot be formatted.");
            }
        }

        private void WriteFor(IRForStatement loop)
        {
            this.text.Append("for\n");
            this.WriteIndentation();
            this.text.Append("{\n");
            this.indentation++;

            this.WriteIndentation();
            this.text.Append("initialize\n");
            this.WriteBlock(loop.Initialization);

            this.WriteIndentation();
            this.text.Append("condition (");
            this.WriteExpression(loop.Condition);
            this.text.Append(");\n");

            this.WriteIndentation();
            this.text.Append("iterate\n");
            this.WriteBlock(loop.Iteration);

            this.WriteIndentation();
            this.text.Append("body\n");
            this.WriteBlock(loop.Body);

            this.indentation--;
            this.WriteIndentation();
            this.text.Append("}\n");
        }

        private void WriteExpression(IRExpression expression)
        {
            this.cancellationToken.ThrowIfCancellationRequested();

            switch (expression)
            {
                case IRIntegerConstant integer:
                    this.text.Append(integer.Value.ToString(CultureInfo.InvariantCulture));
                    break;

                case IRBooleanConstant boolean:
                    this.text.Append(boolean.Value ? "true" : "false");
                    break;

                case IRParameterReferenceExpression parameter:
                    this.WriteParameterReference(parameter);
                    break;

                case IRLocalReferenceExpression local:
                    this.WriteLocal(local.Index, false);
                    break;

                case IRIntegerUpdateExpression update:
                    this.WriteIntegerUpdate(update);
                    break;

                case IRCallExpression call:
                    this.WriteCall(call.Symbol, call.Type, call.ParameterTypes, call.Arguments);
                    break;

                case IRBooleanNotExpression negation:
                    this.text.Append("not (");
                    this.WriteExpression(negation.Operand);
                    this.text.Append(')');
                    break;

                case IRBooleanBinaryExpression binary:
                    this.WriteBinary(binary.Left, GetBooleanOperator(binary.Operator), binary.Right);
                    break;

                case IRIntegerUnaryExpression unary:
                    this.text.Append(GetUnaryOperator(unary.Operator)).Append('(');
                    this.WriteExpression(unary.Operand);
                    this.text.Append(')');
                    break;

                case IRIntegerBinaryExpression binary:
                    this.WriteBinary(binary.Left, GetIntegerOperator(binary.Operator), binary.Right);
                    break;

                case IRIntegerComparisonExpression comparison:
                    this.WriteBinary(comparison.Left, GetComparisonOperator(comparison.Operator), comparison.Right);
                    break;

                case IRIntegerRangeComparisonExpression comparison:
                    this.text.Append("evaluate_then(");
                    this.WriteExpression(comparison.Operand);
                    this.text.Append(", result: ").Append(comparison.Result ? "true" : "false").Append(')');
                    break;

                default:
                    throw new NotSupportedException($"Structured IR expression \"{expression.GetType().Name}\" cannot be formatted.");
            }
        }

        private void WriteBinary(IRExpression left, string operation, IRExpression right)
        {
            this.text.Append('(');
            this.WriteExpression(left);
            this.text.Append(' ').Append(operation).Append(' ');
            this.WriteExpression(right);
            this.text.Append(')');
        }

        private void WriteIntegerUpdate(IRIntegerUpdateExpression update)
        {
            string operation = update.IsIncrement ? "++" : "--";

            if (update.IsPrefix)
            {
                this.text.Append(operation);
            }

            this.WriteLocal(update.Index, false);

            if (!update.IsPrefix)
            {
                this.text.Append(operation);
            }
        }

        private void WriteParameterReference(IRParameterReferenceExpression reference)
        {
            if (reference.Index >= this.function.Parameters.Count)
            {
                throw new InvalidOperationException($"Parameter index {reference.Index} is outside the inspected function signature.");
            }

            IRParameter parameter = this.function.Parameters[reference.Index];

            if (parameter.Type != reference.Type)
            {
                throw new InvalidOperationException($"Parameter index {reference.Index} has an inconsistent IR type.");
            }

            this.WriteSymbol(parameter.Name, InspectionSymbolKind.Parameter, this.function.Name, reference.Index, false);
        }

        private void WriteLocal(int index, bool isDefinition)
        {
            string name = "%local" + index.ToString(CultureInfo.InvariantCulture);
            this.WriteSymbol(name, InspectionSymbolKind.Local, this.function.Name, index, isDefinition);
        }

        private void WriteCall(string symbol, IRType returnType, IReadOnlyList<IRType> parameterTypes, IReadOnlyList<IRCallArgument> arguments)
        {
            ArgumentException.ThrowIfNullOrEmpty(symbol);
            ArgumentNullException.ThrowIfNull(parameterTypes);
            ArgumentNullException.ThrowIfNull(arguments);

            this.text.Append("call ").Append(GetTypeName(returnType)).Append(' ');
            this.WriteSymbol(symbol, InspectionSymbolKind.Function, symbol, null, false);
            this.text.Append('(');

            for (int i = 0; i < arguments.Count; i++)
            {
                this.cancellationToken.ThrowIfCancellationRequested();

                IRCallArgument argument = arguments[i];

                if (argument is null || argument.ParameterIndex < 0 || argument.ParameterIndex >= parameterTypes.Count
                    || argument.Expression is null || argument.Expression.Type != parameterTypes[argument.ParameterIndex])
                {
                    throw new InvalidOperationException("Structured IR call arguments must refer to parameters of the selected signature.");
                }

                if (i > 0)
                {
                    this.text.Append(", ");
                }

                this.text.Append(GetTypeName(parameterTypes[argument.ParameterIndex])).Append(' ');
                string name = "arg" + argument.ParameterIndex.ToString(CultureInfo.InvariantCulture);

                this.WriteSymbol(name, InspectionSymbolKind.Parameter, symbol, argument.ParameterIndex, false);
                this.text.Append(": ");
                this.WriteExpression(argument.Expression);
            }

            this.text.Append(')');
        }

        private void WriteSymbol(string name, InspectionSymbolKind kind, string functionSymbol, int? index, bool isDefinition)
        {
            int start = this.text.Length;
            this.text.Append(name);

            InspectionSymbolIdentity identity = new(kind, functionSymbol, index);
            this.occurrences.Add(new InspectionSymbolOccurrence(identity, start, this.text.Length - start, isDefinition));
        }

        private void WriteIndentation() => this.text.Append(' ', this.indentation * 4);

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
            IRBooleanBinaryOperator.LogicalAnd => "and",
            IRBooleanBinaryOperator.LogicalOr => "or",
            IRBooleanBinaryOperator.EagerAnd => "&",
            IRBooleanBinaryOperator.EagerOr => "|",
            IRBooleanBinaryOperator.EagerXor => "^",
            _ => throw new NotSupportedException($"Boolean IR operator \"{operation}\" cannot be formatted.")
        };

        private static string GetUnaryOperator(IRIntegerUnaryOperator operation) => operation switch
        {
            IRIntegerUnaryOperator.Plus => "+",
            IRIntegerUnaryOperator.Negate => "-",
            IRIntegerUnaryOperator.Complement => "~",
            _ => throw new NotSupportedException($"Unary integer IR operator \"{operation}\" cannot be formatted.")
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
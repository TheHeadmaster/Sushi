using System.Globalization;
using FluentAssertions;
using NUnit.Framework;
using Sushi.Inspection;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;

namespace Sushi.Testing.Inspection;

/// <summary>
/// Verifies lowered representation fidelity and semantic navigation without reparsing output.
/// </summary>
[TestFixture]
public class LoweredIRFormatterTests
{
    [TestCase(TestName = "Lowered IR Formatting Emits Exact Text And Typed Semantic Ranges")]
    public void FormatShouldProduceTextAndNavigationMetadata()
    {
        LoweredFunction function = new(IRAccessibility.Public, "__sushi_😀", IRType.Int32,
            [new IRParameter(0, "input😀", IRType.Int32)],
            [new LoweredBasicBlock("entry😀", [
                new LoweredLocalStoreInstruction(7, IRType.Int32, new LoweredParameterValue(0, IRType.Int32)),
                new LoweredLocalStoreInstruction(7, IRType.Int32, new LoweredIntegerConstant(42)),
                new LoweredLocalLoadInstruction(new LoweredTemporaryValue(3, IRType.Int32), 7)
            ], new LoweredReturnTerminator(new LoweredTemporaryValue(3, IRType.Int32)))]);

        InspectionDocument document = LoweredIRFormatter.Format(function, CancellationToken.None);
        string expected = """
            public function int32 __sushi_😀(int32 input😀)
            {
                slot int32 %local7;
                entry😀:
                    store int32 %local7 = input😀;
                    store int32 %local7 = 42;
                    int32 %temp3 = load %local7;
                    return %temp3;
            }
            """.ReplaceLineEndings("\n") + "\n";

        document.Text
            .Should()
            .Be(expected);
        document.Stage
            .Should()
            .Be(InspectionStage.LoweredIR);
        document.FunctionSymbol
            .Should()
            .Be(function.Name);
        AssertSymbol(document, InspectionSymbolKind.Function, function.Name, null, true, 1);
        AssertSymbol(document, InspectionSymbolKind.Parameter, function.Name, 0, true, 1);
        AssertSymbol(document, InspectionSymbolKind.Parameter, function.Name, 0, false, 1);
        AssertSymbol(document, InspectionSymbolKind.BasicBlock, function.Name, 0, true, 1);
        AssertSymbol(document, InspectionSymbolKind.Local, function.Name, 7, true, 1);
        AssertSymbol(document, InspectionSymbolKind.Local, function.Name, 7, false, 3);
        AssertSymbol(document, InspectionSymbolKind.Temporary, function.Name, 3, true, 1);
        AssertSymbol(document, InspectionSymbolKind.Temporary, function.Name, 3, false, 1);
        document.SymbolMetadata[new InspectionSymbolIdentity(InspectionSymbolKind.Local, function.Name, 7)]
            .Should()
            .Be(new InspectionValueMetadata(IRType.Int32, "%local7"));
        document.SymbolMetadata[new InspectionSymbolIdentity(InspectionSymbolKind.Temporary, function.Name, 3)]
            .Should()
            .Be(new InspectionValueMetadata(IRType.Int32, "%temp3"));
        document.SymbolMetadata[new InspectionSymbolIdentity(InspectionSymbolKind.BasicBlock, function.Name, 0)]
            .Should()
            .Be(new InspectionBasicBlockMetadata("entry😀"));
        AssertRanges(document, function);
    }

    [TestCase(TestName = "Branches And Phi Inputs Resolve Block Positions And Forward SSA References")]
    public void PhiAndBranchesShouldRetainIndependentIdentities()
    {
        LoweredTemporaryValue condition = new(12, IRType.Bool);
        LoweredTemporaryValue result = new(6, IRType.Bool);
        LoweredFunction function = new(IRAccessibility.Private, "Flow", IRType.Bool, [
            new LoweredBasicBlock("start", [], new LoweredConditionalBranchTerminator(new LoweredBooleanConstant(true), "left", "right")),
            new LoweredBasicBlock("merge", [new LoweredBooleanPhiInstruction(result, [
                new LoweredPhiIncoming("left", condition),
                new LoweredPhiIncoming("right", new LoweredBooleanConstant(false))
            ])], new LoweredReturnTerminator(result)),
            new LoweredBasicBlock("left", [new LoweredBooleanNotInstruction(condition, new LoweredBooleanConstant(false))], new LoweredBranchTerminator("merge")),
            new LoweredBasicBlock("right", [], new LoweredBranchTerminator("merge"))
        ]);

        InspectionDocument document = LoweredIRFormatter.Format(function, CancellationToken.None);
        document.Text
            .Should()
            .Contain("branch true true: left false: right;")
            .And
            .Contain("bool %temp6 = phi [%temp12 from left], [false from right];");
        AssertSymbol(document, InspectionSymbolKind.BasicBlock, "Flow", 1, false, 2);
        AssertSymbol(document, InspectionSymbolKind.BasicBlock, "Flow", 2, false, 2);
        AssertSymbol(document, InspectionSymbolKind.BasicBlock, "Flow", 3, false, 2);
        AssertSymbol(document, InspectionSymbolKind.Temporary, "Flow", 12, true, 1);
        AssertSymbol(document, InspectionSymbolKind.Temporary, "Flow", 12, false, 1);
        AssertRanges(document, function);
    }

    [TestCase(TestName = "Calls Preserve Source Argument Order And Callee Parameter Identity")]
    public void CallsShouldRetainCalleeAndArgumentIdentities()
    {
        LoweredTemporaryValue result = new(0, IRType.Int32);
        LoweredFunction function = Function([
            new LoweredValueCallInstruction(result, "Target", [IRType.Int32, IRType.Bool], [
                new LoweredCallArgument(1, new LoweredBooleanConstant(true)),
                new LoweredCallArgument(0, new LoweredIntegerConstant(8))]),
            new LoweredVoidCallInstruction("Sink", [IRType.Int32], [new LoweredCallArgument(0, result)])]);
        InspectionDocument document = LoweredIRFormatter.Format(function, CancellationToken.None);
        document.Text
            .Should()
            .Contain("call int32 Target(bool arg1: true, int32 arg0: 8)")
            .And
            .Contain("call void Sink(int32 arg0: %temp0)")
            .And
            .Contain("return;");
        AssertSymbol(document, InspectionSymbolKind.Function, "Target", null, false, 1);
        AssertSymbol(document, InspectionSymbolKind.Function, "Sink", null, false, 1);
        AssertSymbol(document, InspectionSymbolKind.Parameter, "Target", 1, false, 1);
        AssertSymbol(document, InspectionSymbolKind.Parameter, "Target", 0, false, 1);
        AssertSymbol(document, InspectionSymbolKind.Parameter, "Sink", 0, false, 1);
        AssertRanges(document, function);
    }

    [TestCase(IRIntegerBinaryOperator.Add, "checked +", TestName = "Lowered Add IntegerArithmetic Retains Its Operation")]
    [TestCase(IRIntegerBinaryOperator.Subtract, "checked -", TestName = "Lowered Subtract IntegerArithmetic Retains Its Operation")]
    [TestCase(IRIntegerBinaryOperator.Multiply, "checked *", TestName = "Lowered Multiply IntegerArithmetic Retains Its Operation")]
    [TestCase(IRIntegerBinaryOperator.Divide, "checked /", TestName = "Lowered Divide IntegerArithmetic Retains Its Operation")]
    [TestCase(IRIntegerBinaryOperator.Remainder, "checked %", TestName = "Lowered Remainder IntegerArithmetic Retains Its Operation")]
    public void IntegerArithmeticShouldRemainExplicit(IRIntegerBinaryOperator operation, string expected)
    {
        LoweredFunction function = Function([new LoweredIntegerArithmeticInstruction(
            new LoweredTemporaryValue(0, IRType.Int32), operation, new LoweredIntegerConstant(-9), new LoweredIntegerConstant(2))]);
        InspectionDocument document = LoweredIRFormatter.Format(function, CancellationToken.None);
        document.Text
            .Should()
            .Contain("%temp0 = " + expected);
        AssertSymbol(document, InspectionSymbolKind.Temporary, "Test", 0, true, 1);
    }

    [TestCase(IRIntegerBinaryOperator.BitwiseAnd, "bitwise &", TestName = "Lowered BitwiseAnd IntegerBitwise Retains Its Operation")]
    [TestCase(IRIntegerBinaryOperator.BitwiseOr, "bitwise |", TestName = "Lowered BitwiseOr IntegerBitwise Retains Its Operation")]
    [TestCase(IRIntegerBinaryOperator.BitwiseXor, "bitwise ^", TestName = "Lowered BitwiseXor IntegerBitwise Retains Its Operation")]
    public void IntegerBitwiseShouldRemainExplicit(IRIntegerBinaryOperator operation, string expected)
    {
        LoweredFunction function = Function([new LoweredIntegerBitwiseInstruction(
            new LoweredTemporaryValue(0, IRType.Int32), operation, new LoweredIntegerConstant(-9), new LoweredIntegerConstant(2))]);
        InspectionDocument document = LoweredIRFormatter.Format(function, CancellationToken.None);
        document.Text
            .Should()
            .Contain("%temp0 = " + expected);
        AssertSymbol(document, InspectionSymbolKind.Temporary, "Test", 0, true, 1);
    }

    [TestCase(IRIntegerBinaryOperator.ShiftLeft, "checked_shift <<", TestName = "Lowered ShiftLeft IntegerShift Retains Its Operation")]
    [TestCase(IRIntegerBinaryOperator.ShiftRight, "count_checked_shift >>", TestName = "Lowered ShiftRight IntegerShift Retains Its Operation")]
    public void IntegerShiftShouldRemainExplicit(IRIntegerBinaryOperator operation, string expected)
    {
        LoweredFunction function = Function([new LoweredIntegerShiftInstruction(
            new LoweredTemporaryValue(0, IRType.Int32), operation, new LoweredIntegerConstant(-9), new LoweredIntegerConstant(2))]);
        InspectionDocument document = LoweredIRFormatter.Format(function, CancellationToken.None);
        document.Text
            .Should()
            .Contain("%temp0 = " + expected + " -9, 2;");
        AssertSymbol(document, InspectionSymbolKind.Temporary, "Test", 0, true, 1);
    }

    [TestCase(IRIntegerComparisonOperator.Equal, "compare ==", TestName = "Lowered Equal IntegerComparison Retains Its Operation")]
    [TestCase(IRIntegerComparisonOperator.NotEqual, "compare !=", TestName = "Lowered NotEqual IntegerComparison Retains Its Operation")]
    [TestCase(IRIntegerComparisonOperator.LessThan, "compare <", TestName = "Lowered LessThan IntegerComparison Retains Its Operation")]
    [TestCase(IRIntegerComparisonOperator.LessThanOrEqual, "compare <=", TestName = "Lowered LessThanOrEqual IntegerComparison Retains Its Operation")]
    [TestCase(IRIntegerComparisonOperator.GreaterThan, "compare >", TestName = "Lowered GreaterThan IntegerComparison Retains Its Operation")]
    [TestCase(IRIntegerComparisonOperator.GreaterThanOrEqual, "compare >=", TestName = "Lowered GreaterThanOrEqual IntegerComparison Retains Its Operation")]
    public void IntegerComparisonShouldRemainExplicit(IRIntegerComparisonOperator operation, string expected)
    {
        LoweredFunction function = Function([new LoweredIntegerComparisonInstruction(
            new LoweredTemporaryValue(0, IRType.Bool), operation, new LoweredIntegerConstant(-9), new LoweredIntegerConstant(2))]);
        InspectionDocument document = LoweredIRFormatter.Format(function, CancellationToken.None);
        document.Text
            .Should()
            .Contain("%temp0 = " + expected);
        AssertSymbol(document, InspectionSymbolKind.Temporary, "Test", 0, true, 1);
    }

    [TestCase(IRBooleanBinaryOperator.EagerAnd, "eager &", TestName = "Lowered EagerAnd BooleanBinary Retains Its Operation")]
    [TestCase(IRBooleanBinaryOperator.EagerOr, "eager |", TestName = "Lowered EagerOr BooleanBinary Retains Its Operation")]
    [TestCase(IRBooleanBinaryOperator.EagerXor, "eager ^", TestName = "Lowered EagerXor BooleanBinary Retains Its Operation")]
    public void BooleanBinaryShouldRemainExplicit(IRBooleanBinaryOperator operation, string expected)
    {
        LoweredFunction function = Function([new LoweredBooleanBinaryInstruction(
            new LoweredTemporaryValue(0, IRType.Bool), operation, new LoweredBooleanConstant(true), new LoweredBooleanConstant(false))]);
        InspectionDocument document = LoweredIRFormatter.Format(function, CancellationToken.None);
        document.Text
            .Should()
            .Contain("%temp0 = " + expected);
        AssertSymbol(document, InspectionSymbolKind.Temporary, "Test", 0, true, 1);
    }

    [TestCase(TestName = "Unary Instructions And Sparse Local Inventories Retain Types And Order")]
    public void UnaryInstructionsAndLocalInventoryShouldRemainExplicit()
    {
        LoweredFunction function = Function([
            new LoweredLocalStoreInstruction(9, IRType.Bool, new LoweredBooleanConstant(false)),
            new LoweredBooleanNotInstruction(new LoweredTemporaryValue(0, IRType.Bool), new LoweredBooleanConstant(true)),
            new LoweredIntegerComplementInstruction(new LoweredTemporaryValue(1, IRType.Int32), new LoweredIntegerConstant(1)),
            new LoweredIntegerNegateInstruction(new LoweredTemporaryValue(2, IRType.Int32), new LoweredIntegerConstant(-2)),
            new LoweredLocalLoadInstruction(new LoweredTemporaryValue(3, IRType.Int32), 2)]);
        InspectionDocument document = LoweredIRFormatter.Format(function, CancellationToken.None);
        document.Text
            .Should()
            .Contain("slot int32 %local2;\n    slot bool %local9;")
            .And
            .Contain("bool %temp0 = not true;")
            .And
            .Contain("int32 %temp1 = complement 1;")
            .And
            .Contain("int32 %temp2 = checked_negate -2;");
        AssertRanges(document, function);
    }

    [TestCase(TestName = "Formatting Is Deterministic Read Only And Function Scoped")]
    public void FormattingShouldBeDeterministicReadOnlyAndFunctionScoped()
    {
        LoweredTemporaryValue temporary = new(0, IRType.Int32);
        LoweredInstruction instruction = new LoweredIntegerNegateInstruction(temporary, new LoweredIntegerConstant(3));
        LoweredFunction function = Function([instruction]);
        LoweredBasicBlock block = function.EntryBlock;
        LoweredInstruction[] instructions = [.. block.Instructions];
        LoweredBasicBlock[] blocks = [.. function.Blocks];
        LoweredFunction snapshot = Function([new LoweredIntegerNegateInstruction(new LoweredTemporaryValue(0, IRType.Int32), new LoweredIntegerConstant(3))]);
        InspectionDocument first = LoweredIRFormatter.Format(function, CancellationToken.None);
        InspectionDocument second = LoweredIRFormatter.Format(function, CancellationToken.None);
        LoweredFunction other = new(IRAccessibility.Public, "Other", IRType.Void, function.Blocks);
        InspectionDocument isolated = LoweredIRFormatter.Format(other, CancellationToken.None);

        first.Text
            .Should()
            .Be(second.Text);
        first.Occurrences
            .Should()
            .Equal(second.Occurrences);
        function
            .Should()
            .BeEquivalentTo(snapshot);
        function.Blocks
            .Should()
            .Equal(blocks);
        block.Instructions
            .Should()
            .Equal(instructions);
        block.Instructions[0]
            .Should()
            .BeSameAs(instruction);
        first.Occurrences.Select(item => item.Identity).Intersect(isolated.Occurrences.Select(item => item.Identity))
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Real Lowered IR Uses The Existing Inspection Foundation")]
    public void LoweredStructuredFunctionShouldFormatWithoutChangingStructuredOutput()
    {
        IRFunction function = new(IRAccessibility.Public, "Real", IRType.Int32,
            [new IRParameter(0, "input", IRType.Int32)], new IRBlock([
                new IRLocalDeclarationStatement(0, IRType.Int32, new IRParameterReferenceExpression(0, IRType.Int32)),
                new IRReturnStatement(new IRIntegerBinaryExpression(IRIntegerBinaryOperator.Add,
                    new IRLocalReferenceExpression(0, IRType.Int32), new IRIntegerConstant(1)))
            ]));
        InspectionDocument before = StructuredIRFormatter.Format(function, CancellationToken.None);
        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);
        InspectionDocument document = LoweredIRFormatter.Format(lowered, CancellationToken.None);
        document.Text
            .Should()
            .Contain("checked +")
            .And
            .Contain("load %local0");
        StructuredIRFormatter.Format(function, CancellationToken.None).Text
            .Should()
            .Be(before.Text);
        AssertRanges(document, lowered);
    }

    [TestCase(TestName = "Numeric Formatting Is Independent Of Current Culture")]
    public void ConstantsShouldUseInvariantCulture()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo custom = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            custom.NumberFormat.NegativeSign = "negative";
            CultureInfo.CurrentCulture = custom;
            InspectionDocument document = LoweredIRFormatter.Format(Function([
                new LoweredLocalStoreInstruction(0, IRType.Int32, new LoweredIntegerConstant(-123))]), CancellationToken.None);
            document.Text
                .Should()
                .Contain("= -123;");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [TestCase(TestName = "Null Function And Cancellation Fail Without A Document")]
    public void NullAndCancelledFormattingShouldFail()
    {
        Action nullFunction = () => LoweredIRFormatter.Format(null!, CancellationToken.None);
        nullFunction
            .Should()
            .Throw<ArgumentNullException>();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        Action cancelled = () => LoweredIRFormatter.Format(Function([]), cancellation.Token);
        cancelled
            .Should()
            .Throw<OperationCanceledException>();
    }

    [TestCase(TestName = "Unsupported Instruction Value And Terminator Types Fail Explicitly")]
    public void UnsupportedNodesShouldFail()
    {
        Action instruction = () => LoweredIRFormatter.Format(Function([new UnsupportedInstruction()]), CancellationToken.None);
        Action value = () => LoweredIRFormatter.Format(Function([
            new LoweredLocalStoreInstruction(0, IRType.Int32, new UnsupportedValue())]), CancellationToken.None);
        Action terminator = () => LoweredIRFormatter.Format(new LoweredFunction(IRAccessibility.Public, "Test", IRType.Void,
            [new LoweredBasicBlock("entry", [], new UnsupportedTerminator())]), CancellationToken.None);
        instruction
            .Should()
            .Throw<NotSupportedException>()
            .WithMessage("*UnsupportedInstruction*");
        value
            .Should()
            .Throw<NotSupportedException>()
            .WithMessage("*UnsupportedValue*");
        terminator
            .Should()
            .Throw<NotSupportedException>()
            .WithMessage("*UnsupportedTerminator*");
    }

    [TestCase(0, TestName = "Unknown Unconditional Branch Target Fails")]
    [TestCase(1, TestName = "Unknown True Branch Target Fails")]
    [TestCase(2, TestName = "Unknown False Branch Target Fails")]
    [TestCase(3, TestName = "Unknown Phi Predecessor Fails")]
    public void UnknownBlockReferencesShouldFail(int scenario)
    {
        LoweredTerminator terminator = scenario switch
        {
            0 => new LoweredBranchTerminator("missing"),
            1 => new LoweredConditionalBranchTerminator(new LoweredBooleanConstant(true), "missing", "entry"),
            2 => new LoweredConditionalBranchTerminator(new LoweredBooleanConstant(true), "entry", "missing"),
            _ => new LoweredReturnTerminator()
        };
        LoweredInstruction[] instructions = scenario == 3
            ? [new LoweredBooleanPhiInstruction(new LoweredTemporaryValue(0, IRType.Bool), [
                new LoweredPhiIncoming("entry", new LoweredBooleanConstant(true)),
                new LoweredPhiIncoming("missing", new LoweredBooleanConstant(false))])]
            : [];
        LoweredFunction function = new(IRAccessibility.Public, "Test", IRType.Void,
            [new LoweredBasicBlock("entry", instructions, terminator)]);
        Action format = () => LoweredIRFormatter.Format(function, CancellationToken.None);
        format
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*Basic-block target*missing*");
    }

    [TestCase(false, TestName = "Duplicate SSA Definitions With Matching Types Fail")]
    [TestCase(true, TestName = "Duplicate SSA Definitions With Conflicting Types Fail")]
    public void DuplicateTemporariesShouldFail(bool conflictingType)
    {
        LoweredInstruction second = conflictingType
            ? new LoweredBooleanNotInstruction(new LoweredTemporaryValue(0, IRType.Bool), new LoweredBooleanConstant(true))
            : new LoweredIntegerNegateInstruction(new LoweredTemporaryValue(0, IRType.Int32), new LoweredIntegerConstant(1));
        LoweredFunction function = Function([
            new LoweredIntegerNegateInstruction(new LoweredTemporaryValue(0, IRType.Int32), new LoweredIntegerConstant(1)), second]);
        Action format = () => LoweredIRFormatter.Format(function, CancellationToken.None);
        format
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*Temporary index 0*duplicate*");
    }

    [TestCase(false, TestName = "Conflicting Local Store Types Fail")]
    [TestCase(true, TestName = "Conflicting Local Load Types Fail")]
    public void ConflictingLocalTypesShouldFail(bool load)
    {
        LoweredInstruction second = load
            ? new LoweredLocalLoadInstruction(new LoweredTemporaryValue(0, IRType.Bool), 0)
            : new LoweredLocalStoreInstruction(0, IRType.Bool, new LoweredBooleanConstant(true));
        Action format = () => LoweredIRFormatter.Format(Function([
            new LoweredLocalStoreInstruction(0, IRType.Int32, new LoweredIntegerConstant(1)), second]), CancellationToken.None);
        format
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*Local slot 0*conflicting*");
    }

    [TestCase(0, TestName = "Undefined Temporary Use Fails")]
    [TestCase(1, TestName = "Inconsistent Temporary Use Type Fails")]
    [TestCase(2, TestName = "Unknown Parameter Use Fails")]
    [TestCase(3, TestName = "Inconsistent Parameter Use Type Fails")]
    public void InvalidValueIdentitiesShouldFail(int scenario)
    {
        LoweredValue value = scenario switch
        {
            0 => new LoweredTemporaryValue(1, IRType.Bool),
            1 => new LoweredTemporaryValue(0, IRType.Bool),
            2 => new LoweredParameterValue(1, IRType.Bool),
            _ => new LoweredParameterValue(0, IRType.Bool)
        };
        LoweredFunction function = new(IRAccessibility.Public, "Test", IRType.Bool,
            [new IRParameter(0, "input", IRType.Int32)], [new LoweredBasicBlock("entry", [
                new LoweredIntegerNegateInstruction(new LoweredTemporaryValue(0, IRType.Int32), new LoweredIntegerConstant(1))
            ], new LoweredReturnTerminator(value))]);
        Action format = () => LoweredIRFormatter.Format(function, CancellationToken.None);
        format
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*matching typed definition*");
    }

    private static LoweredFunction Function(IReadOnlyList<LoweredInstruction> instructions) =>
        new(IRAccessibility.Public, "Test", IRType.Void, [new LoweredBasicBlock("entry", instructions, new LoweredReturnTerminator())]);

    private static void AssertSymbol(InspectionDocument document, InspectionSymbolKind kind, string owner, int? index, bool definition, int count)
    {
        InspectionSymbolIdentity identity = new(kind, owner, index);
        document.Occurrences.Count(item => item.Identity == identity && item.IsDefinition == definition)
            .Should()
            .Be(count);
    }

    private static void AssertRanges(InspectionDocument document, LoweredFunction function)
    {
        int previousEnd = 0;
        foreach (InspectionSymbolOccurrence occurrence in document.Occurrences)
        {
            string expected = occurrence.Identity.Kind switch
            {
                InspectionSymbolKind.Function => occurrence.Identity.FunctionSymbol,
                InspectionSymbolKind.BasicBlock => function.Blocks[occurrence.Identity.Index!.Value].Name,
                InspectionSymbolKind.Local => "%local" + occurrence.Identity.Index!.Value.ToString(CultureInfo.InvariantCulture),
                InspectionSymbolKind.Temporary => "%temp" + occurrence.Identity.Index!.Value.ToString(CultureInfo.InvariantCulture),
                InspectionSymbolKind.Parameter when occurrence.Identity.FunctionSymbol == function.Name => function.Parameters[occurrence.Identity.Index!.Value].Name,
                InspectionSymbolKind.Parameter => "arg" + occurrence.Identity.Index!.Value.ToString(CultureInfo.InvariantCulture),
                _ => throw new InvalidOperationException("Unexpected symbol category.")
            };
            occurrence.Start
                .Should()
                .BeGreaterThanOrEqualTo(previousEnd);
            occurrence.End
                .Should()
                .BeLessThanOrEqualTo(document.Text.Length);
            document.Text[occurrence.Start..occurrence.End]
                .Should()
                .Be(expected);
            previousEnd = occurrence.End;
        }
    }

    private sealed record UnsupportedInstruction : LoweredInstruction;
    private sealed record UnsupportedTerminator : LoweredTerminator;
    private sealed record UnsupportedValue() : LoweredValue(IRType.Int32);
}

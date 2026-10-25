using FluentAssertions;
using NUnit.Framework;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;

namespace Sushi.Testing.Intermediate.Lowering;

[TestFixture]
public class IRFunctionLowererTests
{
    [TestCase(TestName = "IR Function Lowerer Should Lower Minimum Function To Entry Block Return")]
    public void LowerShould_0()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "main",
            IRType.Int32,
            new IRBlock(
            [
                new IRReturnStatement(new IRIntegerConstant(42))
            ]));

        LoweredFunction loweredFunction = IRFunctionLowerer.Lower(function, CancellationToken.None);

        loweredFunction.Accessibility
            .Should()
            .Be(IRAccessibility.Public);

        loweredFunction.Name
            .Should()
            .Be("main");

        loweredFunction.ReturnType
            .Should()
            .Be(IRType.Int32);

        LoweredBasicBlock entryBlock = loweredFunction.Blocks
            .Should()
            .ContainSingle()
            .Which;

        loweredFunction.EntryBlock
            .Should()
            .BeSameAs(entryBlock);

        entryBlock.Name
            .Should()
            .Be("entry");

        entryBlock.Instructions
            .Should()
            .BeEmpty();

        LoweredReturnTerminator terminator = entryBlock.Terminator
            .Should()
            .BeOfType<LoweredReturnTerminator>()
            .Subject;

        LoweredIntegerConstant value = terminator.Value
            .Should()
            .BeOfType<LoweredIntegerConstant>()
            .Subject;

        value.Type
            .Should()
            .Be(IRType.Int32);

        value.Value
            .Should()
            .Be(42);
    }

    [TestCase(IRBooleanBinaryOperator.LogicalAnd, false, false, TestName = "Lowerer Should Skip Right Operand Of False Logical And")]
    [TestCase(IRBooleanBinaryOperator.LogicalOr, true, true, TestName = "Lowerer Should Skip Right Operand Of True Logical Or")]
    public void LowerShould_1(IRBooleanBinaryOperator operation, bool leftValue, bool expectedValue)
    {
        IRFunction function = CreateBooleanFunction(
            new IRBooleanBinaryExpression(operation, new IRBooleanConstant(leftValue), new UnsupportedBooleanExpression()));
    
        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);
    
        LoweredReturnTerminator terminator = lowered.EntryBlock.Terminator
            .Should()
            .BeOfType<LoweredReturnTerminator>()
            .Subject;
    
        LoweredBooleanConstant value = terminator.Value
            .Should()
            .BeOfType<LoweredBooleanConstant>()
            .Subject;
    
        value.Value
            .Should()
            .Be(expectedValue);
    }
    
    [TestCase(IRBooleanBinaryOperator.EagerAnd, false, TestName = "Lowerer Should Visit Right Operand Of Eager And")]
    [TestCase(IRBooleanBinaryOperator.EagerOr, true, TestName = "Lowerer Should Visit Right Operand Of Eager Or")]
    [TestCase(IRBooleanBinaryOperator.EagerXor, false, TestName = "Lowerer Should Visit Right Operand Of Eager Xor")]
    [TestCase(IRBooleanBinaryOperator.LogicalAnd, true, TestName = "Lowerer Should Visit Right Operand Of True Logical And")]
    [TestCase(IRBooleanBinaryOperator.LogicalOr, false, TestName = "Lowerer Should Visit Right Operand Of False Logical Or")]
    public void LowerShould_2(IRBooleanBinaryOperator operation, bool leftValue)
    {
        IRFunction function = CreateBooleanFunction(
            new IRBooleanBinaryExpression(operation, new IRBooleanConstant(leftValue), new UnsupportedBooleanExpression()));
    
        Action action = () => IRFunctionLowerer.Lower(function, CancellationToken.None);
    
        action
            .Should()
            .Throw<NotSupportedException>();
    }
    
    private static IRFunction CreateBooleanFunction(IRExpression expression)
        => new(
            IRAccessibility.Public,
            "main",
            IRType.Bool,
            new IRBlock([new IRReturnStatement(expression)]));
    
    private sealed record UnsupportedBooleanExpression() : IRExpression(IRType.Bool);
}
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

        LoweredFunction loweredFunction = new IRFunctionLowerer().Lower(function, CancellationToken.None);

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
}
using FluentAssertions;
using LLVMSharp.Interop;
using NUnit.Framework;
using Sushi.Backends;
using Sushi.Backends.LLVM;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;

namespace Sushi.Testing.Intermediate.Lowering;

/// <summary>
/// Verifies while-loop control-flow lowering, continuation and exit targets,
/// nested loop transfers, and LLVM verifier compatibility.
/// </summary>
[TestFixture]
public class WhileLoopLoweringTests
{
    [TestCase(TestName = "While Condition Should Be Revisited From Normal Body Completion")]
    public void LowerShouldCreateConditionBackEdge()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "Count",
            IRType.Void,
            [new IRParameter(0, "condition", IRType.Bool)],
            new IRBlock(
            [
                new IRWhileStatement(
                    new IRParameterReferenceExpression(0, IRType.Bool),
                    new IRBlock([]))
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);
        LoweredBasicBlock condition = lowered.Blocks.Single(block => block.Name.StartsWith("while.condition", StringComparison.Ordinal));
        LoweredBasicBlock body = lowered.Blocks.Single(block => block.Name.StartsWith("while.body", StringComparison.Ordinal));
        LoweredBasicBlock exit = lowered.Blocks.Single(block => block.Name.StartsWith("while.exit", StringComparison.Ordinal));

        lowered.EntryBlock.Terminator
            .Should()
            .BeOfType<LoweredBranchTerminator>()
            .Which.Target
            .Should()
            .Be(condition.Name);

        condition.Terminator
            .Should()
            .BeOfType<LoweredConditionalBranchTerminator>()
            .Which.TrueTarget
            .Should()
            .Be(body.Name);

        condition.Terminator
            .Should()
            .BeOfType<LoweredConditionalBranchTerminator>()
            .Which.FalseTarget
            .Should()
            .Be(exit.Name);

        body.Terminator
            .Should()
            .BeOfType<LoweredBranchTerminator>()
            .Which.Target
            .Should()
            .Be(condition.Name);

        exit.Terminator
            .Should()
            .BeOfType<LoweredReturnTerminator>();

        VerifyModule(lowered);
    }

    [TestCase(TestName = "Break Should Branch Directly To Loop Exit")]
    public void LowerShouldSendBreakToExit()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "BreakLoop",
            IRType.Void,
            new IRBlock(
            [
                new IRWhileStatement(
                    new IRBooleanConstant(true),
                    new IRBlock([new IRLoopTransferStatement(true, 1)]))
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);
        LoweredBasicBlock body = lowered.Blocks.Single(block => block.Name.StartsWith("while.body", StringComparison.Ordinal));
        LoweredBasicBlock exit = lowered.Blocks.Single(block => block.Name.StartsWith("while.exit", StringComparison.Ordinal));

        body.Terminator
            .Should()
            .BeOfType<LoweredBranchTerminator>()
            .Which.Target
            .Should()
            .Be(exit.Name);

        VerifyModule(lowered);
    }

    [TestCase(TestName = "Continue Should Branch Directly To Loop Condition")]
    public void LowerShouldSendContinueToCondition()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "ContinueLoop",
            IRType.Void,
            [new IRParameter(0, "condition", IRType.Bool)],
            new IRBlock(
            [
                new IRWhileStatement(
                    new IRParameterReferenceExpression(0, IRType.Bool),
                    new IRBlock([new IRLoopTransferStatement(false, 1)]))
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);
        LoweredBasicBlock body = lowered.Blocks.Single(block => block.Name.StartsWith("while.body", StringComparison.Ordinal));
        LoweredBasicBlock condition = lowered.Blocks.Single(block => block.Name.StartsWith("while.condition", StringComparison.Ordinal));

        body.Terminator
            .Should()
            .BeOfType<LoweredBranchTerminator>()
            .Which.Target
            .Should()
            .Be(condition.Name);

        VerifyModule(lowered);
    }

    [TestCase(true, TestName = "Repeated Break Should Target Outer Loop Exit")]
    [TestCase(false, TestName = "Repeated Continue Should Target Outer Loop Condition")]
    public void LowerShouldTargetEnclosingLoop(bool isBreak)
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "Nested",
            IRType.Void,
            [new IRParameter(0, "condition", IRType.Bool)],
            new IRBlock(
            [
                new IRWhileStatement(
                    new IRParameterReferenceExpression(0, IRType.Bool),
                    new IRBlock(
                    [
                        new IRWhileStatement(
                            new IRParameterReferenceExpression(0, IRType.Bool),
                            new IRBlock([new IRLoopTransferStatement(isBreak, 2)]))
                    ]))
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);

        LoweredBasicBlock[] conditions = [.. lowered.Blocks.Where(block => block.Name.StartsWith("while.condition", StringComparison.Ordinal))];
        LoweredBasicBlock[] bodies = [.. lowered.Blocks.Where(block => block.Name.StartsWith("while.body", StringComparison.Ordinal))];
        LoweredBasicBlock[] exits = [.. lowered.Blocks.Where(block => block.Name.StartsWith("while.exit", StringComparison.Ordinal))];

        conditions
            .Should()
            .HaveCount(2);

        bodies
            .Should()
            .HaveCount(2);

        exits
            .Should()
            .HaveCount(2);

        string expectedTarget = isBreak ? exits[0].Name : conditions[0].Name;

        bodies[1].Terminator
            .Should()
            .BeOfType<LoweredBranchTerminator>()
            .Which.Target
            .Should()
            .Be(expectedTarget);

        VerifyModule(lowered);
    }

    [TestCase(TestName = "Short Circuit While Condition Should Produce Valid LLVM Phi Edges")]
    public void LowerShouldVerifyShortCircuitCondition()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "ShortCircuit",
            IRType.Void,
            [
                new IRParameter(0, "first", IRType.Bool),
                new IRParameter(1, "second", IRType.Bool)
            ],
            new IRBlock(
            [
                new IRWhileStatement(
                    new IRBooleanBinaryExpression(
                        IRBooleanBinaryOperator.LogicalAnd,
                        new IRParameterReferenceExpression(0, IRType.Bool),
                        new IRParameterReferenceExpression(1, IRType.Bool)),
                    new IRBlock([new IRLoopTransferStatement(true, 1)]))
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);

        lowered.Blocks
            .SelectMany(block => block.Instructions)
            .OfType<LoweredBooleanPhiInstruction>()
            .Should()
            .ContainSingle();

        VerifyModule(lowered);
    }

    [TestCase(TestName = "Return Within While Should Keep Function Exit And Verifiable Blocks")]
    public void LowerShouldReturnFromInsideLoop()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "Find",
            IRType.Int32,
            [new IRParameter(0, "condition", IRType.Bool)],
            new IRBlock(
            [
                new IRWhileStatement(
                    new IRParameterReferenceExpression(0, IRType.Bool),
                    new IRBlock([new IRReturnStatement(new IRIntegerConstant(10))])),
                new IRReturnStatement(new IRIntegerConstant(20))
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);

        lowered.Blocks
            .Select(block => block.Terminator)
            .OfType<LoweredReturnTerminator>()
            .Should()
            .HaveCount(2);

        VerifyModule(lowered);
    }

    [TestCase(TestName = "Loop Transfer Outside Enclosing Loop Should Be Rejected")]
    public void LowerShouldRejectTransferOutsideLoop()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "Invalid",
            IRType.Void,
            new IRBlock([new IRLoopTransferStatement(true, 1)]));

        Action action = () => IRFunctionLowerer.Lower(function, CancellationToken.None);

        action
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*exceeds the available enclosing loops*");
    }

    [TestCase(TestName = "Excessive Nested Transfer Depth Should Be Rejected")]
    public void LowerShouldRejectExcessiveTransferDepth()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "Invalid",
            IRType.Void,
            new IRBlock(
            [
                new IRWhileStatement(
                    new IRBooleanConstant(true),
                    new IRBlock([new IRLoopTransferStatement(false, 2)]))
            ]));

        Action action = () => IRFunctionLowerer.Lower(function, CancellationToken.None);

        action
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*exceeds the available enclosing loops*");
    }

    [TestCase(TestName = "Non Boolean While Condition Should Be Rejected By Lowerer")]
    public void LowerShouldRejectNonBooleanCondition()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "Invalid",
            IRType.Void,
            new IRBlock(
            [
                new IRWhileStatement(new IRIntegerConstant(1), new IRBlock([]))
            ]));

        Action action = () => IRFunctionLowerer.Lower(function, CancellationToken.None);

        action
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*condition must have type bool*");
    }

    private static void VerifyModule(LoweredFunction function)
    {
        using IModuleArtifact artifact = new LLVMModuleEmitter().Emit("WhileLoop", function, CancellationToken.None);
        LLVMModuleArtifact module = (LLVMModuleArtifact)artifact;

        module.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message)
            .Should()
            .BeTrue(message);
    }
}
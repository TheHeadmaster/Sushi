using FluentAssertions;
using LLVMSharp.Interop;
using NUnit.Framework;
using Sushi.Backends;
using Sushi.Backends.LLVM;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;

namespace Sushi.Testing.Intermediate.Lowering;

/// <summary>
/// Verifies explicit for-loop phases, iteration and exit edges,
/// nested loop transfer destinations, and LLVM module validity.
/// </summary>
[TestFixture]
public class ForLoopLoweringTests
{
    [TestCase(TestName = "For Must Initialize Before The First Condition Test")]
    public void LowerShouldExecuteInitializationBeforeCondition()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "Count",
            IRType.Int32,
            new IRBlock(
            [
                new IRForStatement(
                    new IRBlock([new IRLocalDeclarationStatement(0, IRType.Int32, new IRIntegerConstant(0))]),
                    new IRIntegerComparisonExpression(
                        IRIntegerComparisonOperator.LessThan,
                        new IRLocalReferenceExpression(0, IRType.Int32),
                        new IRIntegerConstant(3)),
                    new IRBlock(
                    [
                        new IRLocalAssignmentStatement(0, IRType.Int32,
                            new IRIntegerBinaryExpression(
                                IRIntegerBinaryOperator.Add,
                                new IRLocalReferenceExpression(0, IRType.Int32),
                                new IRIntegerConstant(1)))
                    ]),
                    new IRBlock([])),
                new IRReturnStatement(new IRLocalReferenceExpression(0, IRType.Int32))
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);

        lowered.EntryBlock.Instructions
            .OfType<LoweredLocalStoreInstruction>()
            .Should()
            .ContainSingle();

        LoweredBasicBlock condition = FindBlock(lowered, "for.condition");
        LoweredBasicBlock body = FindBlock(lowered, "for.body");
        LoweredBasicBlock iteration = FindBlock(lowered, "for.iteration");
        LoweredBasicBlock exit = FindBlock(lowered, "for.exit");

        AssertBranchTarget(lowered.EntryBlock, condition.Name);
        AssertConditionalTargets(condition, body.Name, exit.Name);
        AssertBranchTarget(body, iteration.Name);
        AssertBranchTarget(iteration, condition.Name);

        iteration.Instructions
            .OfType<LoweredLocalStoreInstruction>()
            .Should()
            .ContainSingle();

        exit.Terminator
            .Should()
            .BeOfType<LoweredReturnTerminator>();

        VerifyModule(lowered);
    }

    [TestCase(TestName = "Continue Must Target For Iteration Rather Than Condition")]
    public void LowerShouldSendContinueToIteration()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "ContinueLoop",
            IRType.Void,
            new IRBlock(
            [
                new IRForStatement(
                    new IRBlock([]),
                    new IRBooleanConstant(true),
                    new IRBlock([]),
                    new IRBlock([new IRLoopTransferStatement(false, 1)]))
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);
        LoweredBasicBlock iteration = FindBlock(lowered, "for.iteration");

        AssertBranchTarget(FindBlock(lowered, "for.body"), iteration.Name);
        AssertBranchTarget(iteration, FindBlock(lowered, "for.condition").Name);

        VerifyModule(lowered);
    }

    [TestCase(TestName = "Break Must Bypass Both Iteration And Condition")]
    public void LowerShouldSendBreakDirectlyToExit()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "BreakLoop",
            IRType.Void,
            new IRBlock(
            [
                new IRForStatement(
                    new IRBlock([]),
                    new IRBooleanConstant(true),
                    new IRBlock([]),
                    new IRBlock([new IRLoopTransferStatement(true, 1)]))
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);

        AssertBranchTarget(FindBlock(lowered, "for.body"), FindBlock(lowered, "for.exit").Name);
        VerifyModule(lowered);
    }

    [TestCase(true, TestName = "Repeated Break From Inner While Must Target Outer For Exit")]
    [TestCase(false, TestName = "Repeated Continue From Inner While Must Target Outer For Iteration")]
    public void LowerShouldSelectOuterForTarget(bool isBreak)
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "Nested",
            IRType.Void,
            new IRBlock(
            [
                new IRForStatement(
                    new IRBlock([]),
                    new IRBooleanConstant(true),
                    new IRBlock([]),
                    new IRBlock(
                    [
                        new IRWhileStatement(
                            new IRBooleanConstant(true),
                            new IRBlock([new IRLoopTransferStatement(isBreak, 2)]))
                    ]))
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);

        string target = isBreak
            ? FindBlock(lowered, "for.exit").Name
            : FindBlock(lowered, "for.iteration").Name;

        AssertBranchTarget(FindBlock(lowered, "while.body"), target);
        VerifyModule(lowered);
    }

    [TestCase(TestName = "Repeated Continue From Inner Do While Must Target Outer For Iteration")]
    public void LowerShouldPreserveMixedLoopContinuation()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "Mixed",
            IRType.Void,
            new IRBlock(
            [
                new IRForStatement(
                    new IRBlock([]),
                    new IRBooleanConstant(true),
                    new IRBlock([]),
                    new IRBlock(
                    [
                        new IRDoWhileStatement(
                            new IRBlock([new IRLoopTransferStatement(false, 2)]),
                            new IRBooleanConstant(false))
                    ]))
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);

        AssertBranchTarget(FindBlock(lowered, "do.body"), FindBlock(lowered, "for.iteration").Name);
        VerifyModule(lowered);
    }

    [TestCase(TestName = "Ordered Iteration Stores Must Remain In One Iteration Block")]
    public void LowerShouldPreserveIterationOperationOrder()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "OrderedUpdates",
            IRType.Void,
            new IRBlock(
            [
                new IRForStatement(
                    new IRBlock(
                    [
                        new IRLocalDeclarationStatement(0, IRType.Int32, new IRIntegerConstant(1)),
                        new IRLocalDeclarationStatement(1, IRType.Int32, new IRIntegerConstant(0))
                    ]),
                    new IRBooleanConstant(false),
                    new IRBlock(
                    [
                        new IRLocalAssignmentStatement(1, IRType.Int32, new IRLocalReferenceExpression(0, IRType.Int32)),
                        new IRLocalAssignmentStatement(0, IRType.Int32, new IRIntegerConstant(2))
                    ]),
                    new IRBlock([]))
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);
        LoweredBasicBlock iteration = FindBlock(lowered, "for.iteration");

        iteration.Instructions
            .OfType<LoweredLocalStoreInstruction>()
            .Select(instruction => instruction.Index)
            .Should()
            .Equal(1, 0);

        VerifyModule(lowered);
    }

    [TestCase(TestName = "Short Circuit For Condition Must Keep Correct Phi Edges")]
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
                new IRForStatement(
                    new IRBlock([]),
                    new IRBooleanBinaryExpression(
                        IRBooleanBinaryOperator.LogicalAnd,
                        new IRParameterReferenceExpression(0, IRType.Bool),
                        new IRParameterReferenceExpression(1, IRType.Bool)),
                    new IRBlock([]),
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

    [TestCase(TestName = "Return Inside For Must Exit The Function")]
    public void LowerShouldPreserveReturnInsideFor()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "ReturnEarly",
            IRType.Int32,
            new IRBlock(
            [
                new IRForStatement(
                    new IRBlock([]),
                    new IRBooleanConstant(true),
                    new IRBlock([]),
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

    [TestCase(TestName = "Non Boolean For Condition Must Be Rejected")]
    public void LowerShouldRejectNonBooleanCondition()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "Invalid",
            IRType.Void,
            new IRBlock(
            [
                new IRForStatement(
                    new IRBlock([]),
                    new IRIntegerConstant(1),
                    new IRBlock([]),
                    new IRBlock([]))
            ]));

        Action action = () => IRFunctionLowerer.Lower(function, CancellationToken.None);

        action
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*for condition must have type bool*");
    }

    [TestCase(TestName = "Loop Transfer Beyond Available Depth Must Be Rejected")]
    public void LowerShouldRejectExcessiveTransferDepth()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "Invalid",
            IRType.Void,
            new IRBlock(
            [
                new IRForStatement(
                    new IRBlock([]),
                    new IRBooleanConstant(true),
                    new IRBlock([]),
                    new IRBlock([new IRLoopTransferStatement(false, 2)]))
            ]));

        Action action = () => IRFunctionLowerer.Lower(function, CancellationToken.None);

        action
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*exceeds the available enclosing loops*");
    }

    private static LoweredBasicBlock FindBlock(LoweredFunction function, string prefix)
        => function.Blocks.Single(block => block.Name.StartsWith(prefix, StringComparison.Ordinal));

    private static void AssertBranchTarget(LoweredBasicBlock source, string destination)
    {
        source.Terminator
            .Should()
            .BeOfType<LoweredBranchTerminator>()
            .Which.Target
            .Should()
            .Be(destination);
    }

    private static void AssertConditionalTargets(LoweredBasicBlock source, string trueTarget, string falseTarget)
    {
        LoweredConditionalBranchTerminator branch = source.Terminator
            .Should()
            .BeOfType<LoweredConditionalBranchTerminator>()
            .Subject;

        branch.TrueTarget
            .Should()
            .Be(trueTarget);

        branch.FalseTarget
            .Should()
            .Be(falseTarget);
    }

    private static void VerifyModule(LoweredFunction function)
    {
        using IModuleArtifact artifact = new LLVMModuleEmitter().Emit("ForLoop", function, CancellationToken.None);
        LLVMModuleArtifact module = (LLVMModuleArtifact)artifact;

        module.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message)
            .Should()
            .BeTrue(message);
    }
}
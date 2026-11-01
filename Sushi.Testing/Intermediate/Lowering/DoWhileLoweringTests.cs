using FluentAssertions;
using LLVMSharp.Interop;
using NUnit.Framework;
using Sushi.Backends;
using Sushi.Backends.LLVM;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;

namespace Sushi.Testing.Intermediate.Lowering;

/// <summary>
/// Verifies the post-test loop control-flow graph, trailing-condition
/// evaluation, nested transfer targets, and LLVM module validity.
/// </summary>
[TestFixture]
public class DoWhileLoweringTests
{
    [TestCase(TestName = "Do While Must Enter Body Before Evaluating Condition")]
    public void LowerShouldEnterBodyFirst()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "ExecuteOnce",
            IRType.Void,
            new IRBlock(
            [
                new IRDoWhileStatement(new IRBlock([]), new IRBooleanConstant(false))
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);
        LoweredBasicBlock body = FindBlock(lowered, "do.body");
        LoweredBasicBlock condition = FindBlock(lowered, "do.condition");
        LoweredBasicBlock exit = FindBlock(lowered, "do.exit");

        AssertBranchTarget(lowered.EntryBlock, body.Name);
        AssertBranchTarget(body, condition.Name);
        AssertConditionalTargets(condition, body.Name, exit.Name);

        exit.Terminator
            .Should()
            .BeOfType<LoweredReturnTerminator>();

        VerifyModule(lowered);
    }

    [TestCase(TestName = "Continue Must Transfer To Trailing Condition")]
    public void LowerShouldSendContinueToCondition()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "ContinueLoop",
            IRType.Void,
            [new IRParameter(0, "flag", IRType.Bool)],
            new IRBlock(
            [
                new IRDoWhileStatement(
                    new IRBlock([new IRLoopTransferStatement(false, 1)]),
                    new IRParameterReferenceExpression(0, IRType.Bool))
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);

        AssertBranchTarget(FindBlock(lowered, "do.body"), FindBlock(lowered, "do.condition").Name);
        VerifyModule(lowered);
    }

    [TestCase(TestName = "Break Must Bypass Trailing Condition")]
    public void LowerShouldSendBreakToExit()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "BreakLoop",
            IRType.Void,
            new IRBlock(
            [
                new IRDoWhileStatement(
                    new IRBlock([new IRLoopTransferStatement(true, 1)]),
                    new IRBooleanConstant(true))
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);

        AssertBranchTarget(FindBlock(lowered, "do.body"), FindBlock(lowered, "do.exit").Name);
        VerifyModule(lowered);
    }

    [TestCase(TestName = "Post Test Condition Must Read Value Assigned In Body")]
    public void LowerShouldObserveCurrentLocalStorage()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "Count",
            IRType.Int32,
            new IRBlock(
            [
                new IRLocalDeclarationStatement(0, IRType.Int32, new IRIntegerConstant(0)),
                new IRDoWhileStatement(
                    new IRBlock(
                    [
                        new IRLocalAssignmentStatement(0, IRType.Int32,
                            new IRIntegerBinaryExpression(
                                IRIntegerBinaryOperator.Add,
                                new IRLocalReferenceExpression(0, IRType.Int32),
                                new IRIntegerConstant(1)))
                    ]),
                    new IRIntegerComparisonExpression(
                        IRIntegerComparisonOperator.LessThan,
                        new IRLocalReferenceExpression(0, IRType.Int32),
                        new IRIntegerConstant(3))),
                new IRReturnStatement(new IRLocalReferenceExpression(0, IRType.Int32))
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);
        LoweredBasicBlock condition = FindBlock(lowered, "do.condition");

        condition.Instructions
            .OfType<LoweredLocalLoadInstruction>()
            .Should()
            .ContainSingle();

        condition.Instructions
            .OfType<LoweredIntegerComparisonInstruction>()
            .Should()
            .ContainSingle();

        lowered.Blocks
            .SelectMany(block => block.Instructions)
            .OfType<LoweredLocalStoreInstruction>()
            .Should()
            .HaveCount(2);

        VerifyModule(lowered);
    }

    [TestCase(true, TestName = "Repeated Break In Nested While Must Target Outer Do Exit")]
    [TestCase(false, TestName = "Repeated Continue In Nested While Must Target Outer Do Condition")]
    public void LowerShouldTargetOuterDoWhile(bool isBreak)
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "Nested",
            IRType.Void,
            new IRBlock(
            [
                new IRDoWhileStatement(
                    new IRBlock(
                    [
                        new IRWhileStatement(
                            new IRBooleanConstant(true),
                            new IRBlock([new IRLoopTransferStatement(isBreak, 2)]))
                    ]),
                    new IRBooleanConstant(false))
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);
        LoweredBasicBlock innerBody = FindBlock(lowered, "while.body");

        string expectedTarget = isBreak
            ? FindBlock(lowered, "do.exit").Name
            : FindBlock(lowered, "do.condition").Name;

        AssertBranchTarget(innerBody, expectedTarget);
        VerifyModule(lowered);
    }

    [TestCase(TestName = "Repeated Continue In Inner Do Must Target Outer While Condition")]
    public void LowerShouldPreserveMixedLoopContinuation()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "Nested",
            IRType.Void,
            new IRBlock(
            [
                new IRWhileStatement(
                    new IRBooleanConstant(false),
                    new IRBlock(
                    [
                        new IRDoWhileStatement(
                            new IRBlock([new IRLoopTransferStatement(false, 2)]),
                            new IRBooleanConstant(false))
                    ]))
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);

        AssertBranchTarget(FindBlock(lowered, "do.body"), FindBlock(lowered, "while.condition").Name);
        VerifyModule(lowered);
    }

    [TestCase(TestName = "Short Circuit Post Test Condition Must Keep Correct Phi Predecessors")]
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
                new IRDoWhileStatement(
                    new IRBlock([new IRLoopTransferStatement(true, 1)]),
                    new IRBooleanBinaryExpression(
                        IRBooleanBinaryOperator.LogicalAnd,
                        new IRParameterReferenceExpression(0, IRType.Bool),
                        new IRParameterReferenceExpression(1, IRType.Bool)))
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);

        lowered.Blocks
            .SelectMany(block => block.Instructions)
            .OfType<LoweredBooleanPhiInstruction>()
            .Should()
            .ContainSingle();

        VerifyModule(lowered);
    }

    [TestCase(TestName = "Return Inside Post Test Loop Must Exit Enclosing Function")]
    public void LowerShouldPreserveReturnInsideBody()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "ReturnEarly",
            IRType.Int32,
            new IRBlock(
            [
                new IRDoWhileStatement(
                    new IRBlock([new IRReturnStatement(new IRIntegerConstant(1))]),
                    new IRBooleanConstant(false)),
                new IRReturnStatement(new IRIntegerConstant(2))
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);
        LoweredBasicBlock body = FindBlock(lowered, "do.body");

        body.Terminator
            .Should()
            .BeOfType<LoweredReturnTerminator>();

        lowered.Blocks
            .Select(block => block.Terminator)
            .OfType<LoweredReturnTerminator>()
            .Should()
            .HaveCount(2);

        VerifyModule(lowered);
    }

    [TestCase(TestName = "Non Boolean Post Test Condition Must Be Rejected")]
    public void LowerShouldRejectInvalidConditionType()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "Invalid",
            IRType.Void,
            new IRBlock(
            [
                new IRDoWhileStatement(new IRBlock([]), new IRIntegerConstant(1))
            ]));

        Action action = () => IRFunctionLowerer.Lower(function, CancellationToken.None);

        action
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*condition must have type bool*");
    }

    [TestCase(TestName = "Loop Transfer Beyond Available Depth Must Be Rejected")]
    public void LowerShouldRejectInvalidTransferDepth()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "Invalid",
            IRType.Void,
            new IRBlock(
            [
                new IRDoWhileStatement(
                    new IRBlock([new IRLoopTransferStatement(true, 2)]),
                    new IRBooleanConstant(false))
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
        using IModuleArtifact artifact = new LLVMModuleEmitter().Emit("DoWhileLoop", function, CancellationToken.None);
        LLVMModuleArtifact module = (LLVMModuleArtifact)artifact;

        module.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message)
            .Should()
            .BeTrue(message);
    }
}
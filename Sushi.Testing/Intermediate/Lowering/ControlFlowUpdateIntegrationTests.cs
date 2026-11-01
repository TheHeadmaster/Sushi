using FluentAssertions;
using LLVMSharp.Interop;
using NUnit.Framework;
using Sushi.Backends;
using Sushi.Backends.LLVM;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;

namespace Sushi.Testing.Intermediate.Lowering;

/// <summary>
/// Verifies ordering and control-flow invariants when integer updates,
/// mutable assignments, nested logical expressions, and loop transfers
/// appear in the same lowered function.
/// </summary>
[TestFixture]
public class ControlFlowUpdateIntegrationTests
{
    [TestCase(TestName = "Compound Assignment Must Read Its Target Before RHS Updates And Store Last")]
    public void CompoundAssignmentShouldPreserveReadModifyWriteOrdering()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "CompoundUpdates",
            IRType.Int32,
            new IRBlock(
            [
                new IRLocalDeclarationStatement(0, IRType.Int32, new IRIntegerConstant(5)),
                new IRLocalAssignmentStatement(
                    0,
                    IRType.Int32,
                    new IRIntegerBinaryExpression(
                        IRIntegerBinaryOperator.Add,
                        new IRLocalReferenceExpression(0, IRType.Int32),
                        new IRIntegerBinaryExpression(
                            IRIntegerBinaryOperator.Add,
                            new IRIntegerUpdateExpression(0, true, false),
                            new IRIntegerUpdateExpression(0, true, true)))),
                new IRReturnStatement(new IRLocalReferenceExpression(0, IRType.Int32))
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);
        LoweredInstruction[] instructions = [.. lowered.EntryBlock.Instructions];

        LoweredLocalLoadInstruction[] loads = [.. instructions.OfType<LoweredLocalLoadInstruction>()];
        LoweredLocalStoreInstruction[] stores = [.. instructions.OfType<LoweredLocalStoreInstruction>()];

        loads
            .Should()
            .HaveCount(4);

        stores
            .Should()
            .HaveCount(4);

        int initialRead = Array.IndexOf(instructions, loads[0]);
        int firstUpdateRead = Array.IndexOf(instructions, loads[1]);
        int firstUpdateStore = Array.IndexOf(instructions, stores[1]);
        int secondUpdateRead = Array.IndexOf(instructions, loads[2]);
        int secondUpdateStore = Array.IndexOf(instructions, stores[2]);
        int assignmentStore = Array.IndexOf(instructions, stores[3]);
        int resultRead = Array.IndexOf(instructions, loads[3]);

        initialRead.Should().BeLessThan(firstUpdateRead);
        firstUpdateRead.Should().BeLessThan(firstUpdateStore);
        firstUpdateStore.Should().BeLessThan(secondUpdateRead);
        secondUpdateRead.Should().BeLessThan(secondUpdateStore);
        secondUpdateStore.Should().BeLessThan(assignmentStore);
        assignmentStore.Should().BeLessThan(resultRead);

        LoweredIntegerArithmeticInstruction[] arithmetic = [.. instructions.OfType<LoweredIntegerArithmeticInstruction>()];

        arithmetic
            .Should()
            .HaveCount(4);

        arithmetic
            .Select(instruction => instruction.Operator)
            .Should()
            .OnlyContain(operation => operation == IRIntegerBinaryOperator.Add);

        stores[3].Value
            .Should()
            .BeSameAs(arithmetic[3].Destination);

        VerifyModule(lowered);
    }

    [TestCase(TestName = "Nested Logical Expressions Must Preserve Separate Phi Nodes And Update Blocks")]
    public void NestedShortCircuitExpressionsShouldProduceValidPhiPredecessors()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "NestedLogical",
            IRType.Bool,
            [new IRParameter(0, "guard", IRType.Bool)],
            new IRBlock(
            [
                new IRLocalDeclarationStatement(0, IRType.Int32, new IRIntegerConstant(0)),
                new IRReturnStatement(
                    new IRBooleanBinaryExpression(
                        IRBooleanBinaryOperator.LogicalOr,
                        new IRBooleanBinaryExpression(
                            IRBooleanBinaryOperator.LogicalAnd,
                            new IRParameterReferenceExpression(0, IRType.Bool),
                            new IRIntegerComparisonExpression(
                                IRIntegerComparisonOperator.GreaterThan,
                                new IRIntegerUpdateExpression(0, true, false),
                                new IRIntegerConstant(0))),
                        new IRIntegerComparisonExpression(
                            IRIntegerComparisonOperator.GreaterThan,
                            new IRIntegerUpdateExpression(0, true, true),
                            new IRIntegerConstant(0))))
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);
        LoweredBasicBlock[] blocks = [.. lowered.Blocks];
        LoweredBooleanPhiInstruction[] phi = [.. blocks.SelectMany(block => block.Instructions).OfType<LoweredBooleanPhiInstruction>()];

        phi
            .Should()
            .HaveCount(2);

        HashSet<string> blockNames = [.. blocks.Select(block => block.Name)];

        foreach (LoweredBooleanPhiInstruction merge in phi)
        {
            merge.Incoming
                .Should()
                .HaveCount(2);

            merge.Incoming
                .Select(incoming => incoming.Predecessor)
                .Should()
                .OnlyContain(name => blockNames.Contains(name));
        }

        blocks
            .SelectMany(block => block.Instructions)
            .OfType<LoweredIntegerArithmeticInstruction>()
            .Should()
            .HaveCount(2);

        blocks
            .SelectMany(block => block.Instructions)
            .OfType<LoweredLocalStoreInstruction>()
            .Should()
            .HaveCount(3);

        VerifyModule(lowered);
    }

    [TestCase(TestName = "Outer For Continue Must Target Iteration Despite Side Effects In Condition")]
    public void NestedContinueShouldTargetOuterIterationWithSideEffectingCondition()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "ContinueAcrossLoops",
            IRType.Void,
            new IRBlock(
            [
                new IRForStatement(
                    new IRBlock([new IRLocalDeclarationStatement(0, IRType.Int32, new IRIntegerConstant(0))]),
                    new IRIntegerComparisonExpression(
                        IRIntegerComparisonOperator.LessThan,
                        new IRIntegerUpdateExpression(0, true, false),
                        new IRIntegerConstant(3)),
                    new IRBlock(
                    [
                        new IRIntegerUpdateStatement(new IRIntegerUpdateExpression(0, true, true))
                    ]),
                    new IRBlock(
                    [
                        new IRDoWhileStatement(
                            new IRBlock([new IRLoopTransferStatement(false, 2)]),
                            new IRBooleanConstant(false))
                    ]))
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);

        LoweredBasicBlock condition = FindBlock(lowered, "for.condition");
        LoweredBasicBlock iteration = FindBlock(lowered, "for.iteration");
        LoweredBasicBlock innerBody = FindBlock(lowered, "do.body");
        LoweredBasicBlock exit = FindBlock(lowered, "for.exit");

        AssertBranchTarget(innerBody, iteration.Name);
        AssertBranchTarget(iteration, condition.Name);

        condition.Instructions
            .OfType<LoweredLocalLoadInstruction>()
            .Should()
            .ContainSingle();

        condition.Instructions
            .OfType<LoweredLocalStoreInstruction>()
            .Should()
            .ContainSingle();

        iteration.Instructions
            .OfType<LoweredLocalLoadInstruction>()
            .Should()
            .ContainSingle();

        iteration.Instructions
            .OfType<LoweredLocalStoreInstruction>()
            .Should()
            .ContainSingle();

        exit.Terminator
            .Should()
            .BeOfType<LoweredReturnTerminator>();

        VerifyModule(lowered);
    }

    [TestCase(TestName = "Short Circuit RHS Update Must Remain In Conditional Successor")]
    public void ShortCircuitShouldNotHoistUpdateIntoEntryBlock()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "GuardedUpdate",
            IRType.Bool,
            [new IRParameter(0, "guard", IRType.Bool)],
            new IRBlock(
            [
                new IRLocalDeclarationStatement(0, IRType.Int32, new IRIntegerConstant(1)),
                new IRReturnStatement(
                    new IRBooleanBinaryExpression(
                        IRBooleanBinaryOperator.LogicalAnd,
                        new IRParameterReferenceExpression(0, IRType.Bool),
                        new IRIntegerComparisonExpression(
                            IRIntegerComparisonOperator.GreaterThan,
                            new IRIntegerUpdateExpression(0, true, false),
                            new IRIntegerConstant(0))))
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);

        lowered.EntryBlock.Instructions
            .OfType<LoweredLocalStoreInstruction>()
            .Should()
            .ContainSingle();

        lowered.EntryBlock.Instructions
            .OfType<LoweredIntegerArithmeticInstruction>()
            .Should()
            .BeEmpty();

        LoweredBasicBlock right = FindBlock(lowered, "logical.right");

        right.Instructions
            .OfType<LoweredIntegerArithmeticInstruction>()
            .Should()
            .ContainSingle();

        right.Instructions
            .OfType<LoweredLocalStoreInstruction>()
            .Should()
            .ContainSingle();

        lowered.Blocks
            .SelectMany(block => block.Instructions)
            .OfType<LoweredBooleanPhiInstruction>()
            .Should()
            .ContainSingle();

        VerifyModule(lowered);
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

    private static void VerifyModule(LoweredFunction function)
    {
        using IModuleArtifact artifact = new LLVMModuleEmitter().Emit("ControlFlowUpdates", function, CancellationToken.None);
        LLVMModuleArtifact module = (LLVMModuleArtifact)artifact;

        module.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message)
            .Should()
            .BeTrue(message);
    }
}
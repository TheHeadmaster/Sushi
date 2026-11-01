using FluentAssertions;
using LLVMSharp.Interop;
using NUnit.Framework;
using Sushi.Backends;
using Sushi.Backends.LLVM;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;

namespace Sushi.Testing.Intermediate.Lowering;

/// <summary>
/// Verifies integer update storage ordering, prefix/postfix result identity,
/// checked arithmetic, and LLVM verifier compatibility.
/// </summary>
[TestFixture]
public class IntegerUpdateLoweringTests
{
    [TestCase(true, false, IRIntegerBinaryOperator.Add, TestName = "Postfix Increment Returns Previous Loaded Value")]
    [TestCase(true, true, IRIntegerBinaryOperator.Add, TestName = "Prefix Increment Returns Replacement Value")]
    [TestCase(false, false, IRIntegerBinaryOperator.Subtract, TestName = "Postfix Decrement Returns Previous Loaded Value")]
    [TestCase(false, true, IRIntegerBinaryOperator.Subtract, TestName = "Prefix Decrement Returns Replacement Value")]
    public void LowerShouldReturnCorrectUpdateValue(bool isIncrement, bool isPrefix, IRIntegerBinaryOperator expectedOperation)
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "Update",
            IRType.Int32,
            new IRBlock(
            [
                new IRLocalDeclarationStatement(0, IRType.Int32, new IRIntegerConstant(10)),
                new IRReturnStatement(new IRIntegerUpdateExpression(0, isIncrement, isPrefix))
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);
        LoweredInstruction[] instructions = [.. lowered.EntryBlock.Instructions];

        instructions
            .Should()
            .HaveCount(4);

        instructions[0]
            .Should()
            .BeOfType<LoweredLocalStoreInstruction>();

        LoweredLocalLoadInstruction load = instructions[1]
            .Should()
            .BeOfType<LoweredLocalLoadInstruction>()
            .Subject;

        LoweredIntegerArithmeticInstruction arithmetic = instructions[2]
            .Should()
            .BeOfType<LoweredIntegerArithmeticInstruction>()
            .Subject;

        LoweredLocalStoreInstruction replacementStore = instructions[3]
            .Should()
            .BeOfType<LoweredLocalStoreInstruction>()
            .Subject;

        arithmetic.Operator
            .Should()
            .Be(expectedOperation);

        arithmetic.Left
            .Should()
            .BeSameAs(load.Destination);

        replacementStore.Index
            .Should()
            .Be(0);

        replacementStore.Value
            .Should()
            .BeSameAs(arithmetic.Destination);

        LoweredReturnTerminator result = lowered.EntryBlock.Terminator
            .Should()
            .BeOfType<LoweredReturnTerminator>()
            .Subject;

        result.Value
            .Should()
            .BeSameAs(isPrefix ? arithmetic.Destination : load.Destination);

        VerifyModule(lowered);
    }

    [TestCase(TestName = "Standalone Update Must Execute Read Arithmetic And Store")]
    public void StandaloneUpdateShouldPreserveMutation()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "UpdateStandalone",
            IRType.Void,
            new IRBlock(
            [
                new IRLocalDeclarationStatement(0, IRType.Int32, new IRIntegerConstant(10)),
                new IRIntegerUpdateStatement(new IRIntegerUpdateExpression(0, true, false)),
                new IRIntegerUpdateStatement(new IRIntegerUpdateExpression(0, false, true)),
                new IRReturnStatement(null)
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);

        lowered.EntryBlock.Instructions
            .OfType<LoweredIntegerArithmeticInstruction>()
            .Select(instruction => instruction.Operator)
            .Should()
            .Equal(IRIntegerBinaryOperator.Add, IRIntegerBinaryOperator.Subtract);

        lowered.EntryBlock.Instructions
            .OfType<LoweredLocalStoreInstruction>()
            .Should()
            .HaveCount(3);

        lowered.EntryBlock.Instructions
            .OfType<LoweredLocalLoadInstruction>()
            .Should()
            .HaveCount(2);

        VerifyModule(lowered);
    }

    [TestCase(TestName = "Two Updates In Binary Expression Must Execute In Source Order")]
    public void BinaryExpressionShouldPreserveUpdateOrder()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "UpdateTwice",
            IRType.Int32,
            new IRBlock(
            [
                new IRLocalDeclarationStatement(0, IRType.Int32, new IRIntegerConstant(1)),
                new IRReturnStatement(
                    new IRIntegerBinaryExpression(
                        IRIntegerBinaryOperator.Add,
                        new IRIntegerUpdateExpression(0, true, false),
                        new IRIntegerUpdateExpression(0, true, true)))
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);
        LoweredInstruction[] instructions = [.. lowered.EntryBlock.Instructions];

        LoweredLocalLoadInstruction[] loads = [.. instructions.OfType<LoweredLocalLoadInstruction>()];
        LoweredIntegerArithmeticInstruction[] arithmetic = [.. instructions.OfType<LoweredIntegerArithmeticInstruction>()];

        loads
            .Should()
            .HaveCount(2);

        arithmetic
            .Should()
            .HaveCount(3);

        int firstStore = Array.FindIndex(instructions, instruction => instruction is LoweredLocalStoreInstruction store && ReferenceEquals(store.Value, arithmetic[0].Destination));
        int secondLoad = Array.FindIndex(instructions, instruction => ReferenceEquals(instruction, loads[1]));

        firstStore
            .Should()
            .BeLessThan(secondLoad);

        VerifyModule(lowered);
    }

    [TestCase(TestName = "Update After For Initialization Must Use Existing Header Local")]
    public void ForHeaderUpdateShouldUseSameStorage()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "ForUpdate",
            IRType.Void,
            new IRBlock(
            [
                new IRForStatement(
                    new IRBlock([new IRLocalDeclarationStatement(0, IRType.Int32, new IRIntegerConstant(0))]),
                    new IRIntegerComparisonExpression(
                        IRIntegerComparisonOperator.LessThan,
                        new IRLocalReferenceExpression(0, IRType.Int32),
                        new IRIntegerConstant(3)),
                    new IRBlock([new IRIntegerUpdateStatement(new IRIntegerUpdateExpression(0, true, false))]),
                    new IRBlock([]))
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);
        LoweredBasicBlock iteration = lowered.Blocks.Single(block => block.Name.StartsWith("for.iteration", StringComparison.Ordinal));

        iteration.Instructions
            .OfType<LoweredLocalLoadInstruction>()
            .Should()
            .ContainSingle();

        iteration.Instructions
            .OfType<LoweredIntegerArithmeticInstruction>()
            .Should()
            .ContainSingle();

        iteration.Instructions
            .OfType<LoweredLocalStoreInstruction>()
            .Should()
            .ContainSingle();

        VerifyModule(lowered);
    }

    [TestCase(TestName = "Update Before Local Initialization Must Be Rejected By Lowerer")]
    public void UpdateBeforeDeclarationShouldBeRejected()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "InvalidUpdate",
            IRType.Int32,
            new IRBlock([new IRReturnStatement(new IRIntegerUpdateExpression(0, true, false))]));

        Action action = () => IRFunctionLowerer.Lower(function, CancellationToken.None);

        action
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*has not been initialized*");
    }

    private static void VerifyModule(LoweredFunction function)
    {
        using IModuleArtifact artifact = new LLVMModuleEmitter().Emit("IntegerUpdates", function, CancellationToken.None);
        LLVMModuleArtifact module = (LLVMModuleArtifact)artifact;

        module.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message)
            .Should()
            .BeTrue(message);
    }
}
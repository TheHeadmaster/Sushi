using FluentAssertions;
using LLVMSharp.Interop;
using NUnit.Framework;
using Sushi.Backends;
using Sushi.Backends.LLVM;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;

namespace Sushi.Testing.Intermediate.Lowering;

/// <summary>
/// Verifies typed local storage writes, SSA loads, assignment ordering,
/// invalid IR rejection, and LLVM control-flow correctness.
/// </summary>
[TestFixture]
public class LocalAssignmentLoweringTests
{
    [TestCase(TestName = "Assignment Should Load The Previous Value Before Storing Its Replacement")]
    public void LowerShouldPreserveReadBeforeWrite()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "Increment",
            IRType.Int32,
            new IRBlock(
            [
                new IRLocalDeclarationStatement(0, IRType.Int32, new IRIntegerConstant(10)),
                new IRLocalAssignmentStatement(0, IRType.Int32,
                    new IRIntegerBinaryExpression(
                        IRIntegerBinaryOperator.Add,
                        new IRLocalReferenceExpression(0, IRType.Int32),
                        new IRIntegerConstant(1))),
                new IRReturnStatement(new IRLocalReferenceExpression(0, IRType.Int32))
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);
        LoweredInstruction[] instructions = [.. lowered.EntryBlock.Instructions];

        instructions
            .OfType<LoweredLocalStoreInstruction>()
            .Should()
            .HaveCount(2);

        instructions
            .OfType<LoweredLocalLoadInstruction>()
            .Should()
            .HaveCount(2);

        instructions[0]
            .Should()
            .BeOfType<LoweredLocalStoreInstruction>();

        instructions[1]
            .Should()
            .BeOfType<LoweredLocalLoadInstruction>();

        instructions[2]
            .Should()
            .BeOfType<LoweredIntegerArithmeticInstruction>();

        instructions[3]
            .Should()
            .BeOfType<LoweredLocalStoreInstruction>();

        instructions[4]
            .Should()
            .BeOfType<LoweredLocalLoadInstruction>();

        VerifyModule(lowered);
    }

    [TestCase(TestName = "Conditional Assignments Should Use One Typed Slot Across Both Branches")]
    public void LowerShouldPreserveStorageAcrossBranches()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "Choose",
            IRType.Int32,
            [new IRParameter(0, "flag", IRType.Bool)],
            new IRBlock(
            [
                new IRLocalDeclarationStatement(0, IRType.Int32, new IRIntegerConstant(10)),
                new IRIfStatement(
                    new IRParameterReferenceExpression(0, IRType.Bool),
                    new IRBlock([new IRLocalAssignmentStatement(0, IRType.Int32, new IRIntegerConstant(20))]),
                    new IRBlock([new IRLocalAssignmentStatement(0, IRType.Int32, new IRIntegerConstant(30))])),
                new IRReturnStatement(new IRLocalReferenceExpression(0, IRType.Int32))
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);

        lowered.Blocks
            .SelectMany(block => block.Instructions)
            .OfType<LoweredLocalStoreInstruction>()
            .Should()
            .HaveCount(3);

        LoweredLocalLoadInstruction load = lowered.Blocks
            .SelectMany(block => block.Instructions)
            .OfType<LoweredLocalLoadInstruction>()
            .Should()
            .ContainSingle()
            .Which;

        load.Index
            .Should()
            .Be(0);

        lowered.EntryBlock.Terminator
            .Should()
            .BeOfType<LoweredConditionalBranchTerminator>();

        VerifyModule(lowered);
    }

    [TestCase(TestName = "Assignment Initializer Call Should Be Evaluated Once")]
    public void LowerShouldNotDuplicateAssignmentCall()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "Calculate",
            IRType.Int32,
            new IRBlock(
            [
                new IRLocalDeclarationStatement(0, IRType.Int32, new IRIntegerConstant(10)),
                new IRLocalAssignmentStatement(0, IRType.Int32,
                    new IRCallExpression("GetValue", IRType.Int32, [], [])),
                new IRReturnStatement(new IRLocalReferenceExpression(0, IRType.Int32))
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);

        lowered.Blocks
            .SelectMany(block => block.Instructions)
            .OfType<LoweredValueCallInstruction>()
            .Should()
            .ContainSingle();

        lowered.Blocks
            .SelectMany(block => block.Instructions)
            .OfType<LoweredLocalStoreInstruction>()
            .Should()
            .HaveCount(2);
    }

    [TestCase(TestName = "Assignment Before Initialization Should Be Rejected")]
    public void LowerShouldRejectAssignmentWithoutDeclaration()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "Invalid",
            IRType.Int32,
            new IRBlock(
            [
                new IRLocalAssignmentStatement(0, IRType.Int32, new IRIntegerConstant(10)),
                new IRReturnStatement(new IRIntegerConstant(10))
            ]));

        Action action = () => IRFunctionLowerer.Lower(function, CancellationToken.None);

        action
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*has not been initialized*");
    }

    [TestCase(TestName = "Assignment With Inconsistent Local Type Should Be Rejected")]
    public void LowerShouldRejectAssignmentTypeMismatch()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "Invalid",
            IRType.Int32,
            new IRBlock(
            [
                new IRLocalDeclarationStatement(0, IRType.Int32, new IRIntegerConstant(10)),
                new IRLocalAssignmentStatement(0, IRType.Bool, new IRBooleanConstant(true)),
                new IRReturnStatement(new IRLocalReferenceExpression(0, IRType.Int32))
            ]));

        Action action = () => IRFunctionLowerer.Lower(function, CancellationToken.None);

        action
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*does not match its initialized value*");
    }

    [TestCase(TestName = "Boolean Local Reassignment Should Produce Verifiable LLVM IR")]
    public void LowerShouldVerifyBooleanStorage()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "Toggle",
            IRType.Bool,
            [new IRParameter(0, "flag", IRType.Bool)],
            new IRBlock(
            [
                new IRLocalDeclarationStatement(0, IRType.Bool, new IRParameterReferenceExpression(0, IRType.Bool)),
                new IRLocalAssignmentStatement(0, IRType.Bool,
                    new IRBooleanNotExpression(new IRLocalReferenceExpression(0, IRType.Bool))),
                new IRReturnStatement(new IRLocalReferenceExpression(0, IRType.Bool))
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);
        VerifyModule(lowered);
    }

    private static void VerifyModule(LoweredFunction function)
    {
        using IModuleArtifact artifact = new LLVMModuleEmitter().Emit(
            "LocalAssignment", function, CancellationToken.None);

        LLVMModuleArtifact module = (LLVMModuleArtifact)artifact;

        module.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message)
            .Should()
            .BeTrue(message);
    }
}
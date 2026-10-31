
using FluentAssertions;
using LLVMSharp.Interop;
using NUnit.Framework;
using Sushi.Backends;
using Sushi.Backends.LLVM;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;

namespace Sushi.Testing.Intermediate.Lowering;

/// <summary>
/// Verifies early return, conditional successor selection, and LLVM validity for void callables.
/// </summary>
[TestFixture]
public class VoidConditionalLoweringTests
{
    [TestCase(TestName = "Void Conditional Should Preserve Runtime Boolean Branching")]
    public void LowerShould_0()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "conditional",
            IRType.Void,
            [new IRParameter(0, "flag", IRType.Bool)],
            new IRBlock(
            [
                new IRIfStatement(
                    new IRParameterReferenceExpression(0, IRType.Bool),
                    new IRBlock([new IRReturnStatement(null)]),
                    new IRBlock([])),
                new IRReturnStatement(null)
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);

        LoweredConditionalBranchTerminator conditional = lowered.EntryBlock.Terminator
            .Should()
            .BeOfType<LoweredConditionalBranchTerminator>()
            .Subject;

        LoweredParameterValue condition = conditional.Condition
            .Should()
            .BeOfType<LoweredParameterValue>()
            .Subject;

        condition.Index
            .Should()
            .Be(0);

        LoweredBasicBlock trueBlock = lowered.Blocks.Single(block => block.Name == conditional.TrueTarget);
        LoweredBasicBlock falseBlock = lowered.Blocks.Single(block => block.Name == conditional.FalseTarget);

        trueBlock.Terminator
            .Should()
            .BeOfType<LoweredReturnTerminator>();

        LoweredBranchTerminator falseTerminator = falseBlock.Terminator
            .Should()
            .BeOfType<LoweredBranchTerminator>()
            .Subject;

        LoweredBasicBlock mergeBlock = lowered.Blocks.Single(block => block.Name == falseTerminator.Target);

        mergeBlock.Terminator
            .Should()
            .BeOfType<LoweredReturnTerminator>();

        using IModuleArtifact artifact = new LLVMModuleEmitter().Emit("VoidConditional", lowered, CancellationToken.None);
        LLVMModuleArtifact module = (LLVMModuleArtifact)artifact;

        module.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message)
            .Should()
            .BeTrue(message);
    }

    [TestCase(TestName = "Void Conditional Should Lower Nested Returns And Fallthrough")]
    public void LowerShould_1()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "nested",
            IRType.Void,
            new IRBlock(
            [
                new IRIfStatement(
                    new IRBooleanConstant(true),
                    new IRBlock(
                    [
                        new IRIfStatement(
                            new IRBooleanConstant(false),
                            new IRBlock([new IRReturnStatement(null)]),
                            new IRBlock([new IRReturnStatement(null)]))
                    ]),
                    new IRBlock([])),
                new IRReturnStatement(null)
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);

        lowered.Blocks
            .Should()
            .HaveCountGreaterThan(3);

        lowered.Blocks
            .Should()
            .OnlyContain(block => block.Terminator.GetType() == typeof(LoweredReturnTerminator)
                || block.Terminator.GetType() == typeof(LoweredBranchTerminator)
                || block.Terminator.GetType() ==typeof(LoweredConditionalBranchTerminator));

        using IModuleArtifact artifact = new LLVMModuleEmitter().Emit("NestedVoidConditional", lowered, CancellationToken.None);
        LLVMModuleArtifact module = (LLVMModuleArtifact)artifact;

        module.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message)
            .Should()
            .BeTrue(message);
    }

    [TestCase(TestName = "Void Lowering Should Stop Emitting Statements After Return")]
    public void LowerShould_2()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "earlyReturn",
            IRType.Void,
            new IRBlock(
            [
                new IRReturnStatement(null),
                new IRIfStatement(
                    new IRBooleanConstant(true),
                    new IRBlock([new IRReturnStatement(null)]),
                    null)
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);

        lowered.Blocks
            .Should()
            .ContainSingle();

        lowered.EntryBlock.Terminator
            .Should()
            .BeOfType<LoweredReturnTerminator>();
    }
}
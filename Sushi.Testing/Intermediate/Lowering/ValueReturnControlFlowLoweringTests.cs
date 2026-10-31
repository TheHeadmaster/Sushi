using FluentAssertions;
using LLVMSharp.Interop;
using NUnit.Framework;
using Sushi.Backends;
using Sushi.Backends.LLVM;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;

namespace Sushi.Testing.Intermediate.Lowering;

/// <summary>
/// Verifies value-returning control flow, including branch termination,
/// fallthrough continuation, and rejection of incomplete IR return paths.
/// </summary>
[TestFixture]
public class ValueReturnControlFlowLoweringTests
{
    [TestCase(TestName = "Both Returning Branches Should Not Produce A Merge Block")]
    public void LowerShould_0()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "Choose",
            IRType.Int32,
            [new IRParameter(0, "flag", IRType.Bool)],
            new IRBlock(
            [
                new IRIfStatement(
                    new IRParameterReferenceExpression(0, IRType.Bool),
                    new IRBlock([new IRReturnStatement(new IRIntegerConstant(10))]),
                    new IRBlock([new IRReturnStatement(new IRIntegerConstant(20))])),
                new IRReturnStatement(new IRIntegerConstant(999))
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);

        lowered.Blocks
            .Should()
            .HaveCount(3);

        LoweredConditionalBranchTerminator branch = lowered.EntryBlock.Terminator
            .Should()
            .BeOfType<LoweredConditionalBranchTerminator>()
            .Subject;

        LoweredBasicBlock trueBlock = lowered.Blocks.Single(block => block.Name == branch.TrueTarget);
        LoweredBasicBlock falseBlock = lowered.Blocks.Single(block => block.Name == branch.FalseTarget);

        trueBlock.Terminator
            .Should()
            .BeOfType<LoweredReturnTerminator>();

        falseBlock.Terminator
            .Should()
            .BeOfType<LoweredReturnTerminator>();

        lowered.Blocks
            .Should()
            .NotContain(block => block.Name.StartsWith("if.merge", StringComparison.Ordinal));

        VerifyModule(lowered);
    }

    [TestCase(TestName = "Conditional Early Return Should Continue Into Later Return")]
    public void LowerShould_1()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "Fallback",
            IRType.Int32,
            [new IRParameter(0, "flag", IRType.Bool)],
            new IRBlock(
            [
                new IRIfStatement(
                    new IRParameterReferenceExpression(0, IRType.Bool),
                    new IRBlock([new IRReturnStatement(new IRIntegerConstant(10))]),
                    null),
                new IRReturnStatement(new IRIntegerConstant(20))
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);

        LoweredConditionalBranchTerminator branch = lowered.EntryBlock.Terminator
            .Should()
            .BeOfType<LoweredConditionalBranchTerminator>()
            .Subject;

        LoweredBasicBlock trueBlock = lowered.Blocks.Single(block => block.Name == branch.TrueTarget);
        LoweredBasicBlock falseBlock = lowered.Blocks.Single(block => block.Name == branch.FalseTarget);

        trueBlock.Terminator
            .Should()
            .BeOfType<LoweredReturnTerminator>();

        LoweredReturnTerminator fallback = falseBlock.Terminator
            .Should()
            .BeOfType<LoweredReturnTerminator>()
            .Subject;

        LoweredIntegerConstant value = fallback.Value
            .Should()
            .BeOfType<LoweredIntegerConstant>()
            .Subject;

        value.Value
            .Should()
            .Be(20);

        VerifyModule(lowered);
    }

    [TestCase(TestName = "Constant True Condition Should Return Without A Synthetic False Path")]
    public void LowerShould_2()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "Constant",
            IRType.Int32,
            new IRBlock(
            [
                new IRIfStatement(
                    new IRBooleanConstant(true),
                    new IRBlock([new IRReturnStatement(new IRIntegerConstant(42))]),
                    null)
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);

        LoweredBasicBlock entry = lowered.Blocks
            .Should()
            .ContainSingle()
            .Which;

        LoweredReturnTerminator result = entry.Terminator
            .Should()
            .BeOfType<LoweredReturnTerminator>()
            .Subject;

        LoweredIntegerConstant value = result.Value
            .Should()
            .BeOfType<LoweredIntegerConstant>()
            .Subject;

        value.Value
            .Should()
            .Be(42);

        VerifyModule(lowered);
    }

    [TestCase(TestName = "Incomplete Value Returning IR Should Remain Rejected By Lowerer")]
    public void LowerShould_3()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "Incomplete",
            IRType.Int32,
            [new IRParameter(0, "flag", IRType.Bool)],
            new IRBlock(
            [
                new IRIfStatement(
                    new IRParameterReferenceExpression(0, IRType.Bool),
                    new IRBlock([new IRReturnStatement(new IRIntegerConstant(42))]),
                    null)
            ]));

        Action action = () => IRFunctionLowerer.Lower(function, CancellationToken.None);

        action
            .Should()
            .Throw<NotSupportedException>()
            .WithMessage("*reaches its end without returning a value*");
    }

    private static void VerifyModule(LoweredFunction function)
    {
        using IModuleArtifact artifact = new LLVMModuleEmitter().Emit(
            "ValueReturnControlFlow", function, CancellationToken.None);

        LLVMModuleArtifact module = (LLVMModuleArtifact)artifact;

        module.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message)
            .Should()
            .BeTrue(message);
    }
}
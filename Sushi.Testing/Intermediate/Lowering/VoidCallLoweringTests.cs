using FluentAssertions;
using LLVMSharp.Interop;
using NUnit.Framework;
using Sushi.Backends;
using Sushi.Backends.LLVM;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;

namespace Sushi.Testing.Intermediate.Lowering;

/// <summary>
/// Verifies direct void-call lowering, argument-position mapping, and LLVM validity.
/// </summary>
[TestFixture]
public class VoidCallLoweringTests
{
    [TestCase(TestName = "Void Call Should Preserve Source Order And Parameter Mapping")]
    public void LowerShould_0()
    {
        IRFunction caller = new(
            IRAccessibility.Public,
            "Caller",
            IRType.Void,
            new IRBlock(
            [
                new IRVoidCallStatement(
                    "Target",
                    [IRType.Int32, IRType.Bool],
                    [
                        new IRCallArgument(1, new IRBooleanConstant(true)),
                        new IRCallArgument(0, new IRIntegerConstant(42))
                    ])
            ]));

        IRFunction callee = new(
            IRAccessibility.Public,
            "Target",
            IRType.Void,
            [
                new IRParameter(0, "number", IRType.Int32),
                new IRParameter(1, "enabled", IRType.Bool)
            ],
            new IRBlock([]));

        LoweredFunction loweredCaller = IRFunctionLowerer.Lower(caller, CancellationToken.None);
        LoweredFunction loweredCallee = IRFunctionLowerer.Lower(callee, CancellationToken.None);

        LoweredVoidCallInstruction call = loweredCaller.EntryBlock.Instructions
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<LoweredVoidCallInstruction>()
            .Subject;

        call.Arguments
            .Should()
            .HaveCount(2);

        call.Arguments[0].ParameterIndex
            .Should()
            .Be(1);

        call.Arguments[1].ParameterIndex
            .Should()
            .Be(0);

        call.Arguments[0].Value
            .Should()
            .BeOfType<LoweredBooleanConstant>();

        call.Arguments[1].Value
            .Should()
            .BeOfType<LoweredIntegerConstant>();

        using IModuleArtifact artifact = new LLVMModuleEmitter().Emit(
            "VoidCalls", [loweredCaller, loweredCallee], CancellationToken.None);

        LLVMModuleArtifact module = (LLVMModuleArtifact)artifact;

        module.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message)
            .Should()
            .BeTrue(message);
    }

    [TestCase(TestName = "Void Call Should Reject Duplicate Destination Positions")]
    public void LowerShould_1()
    {
        Action action = () => _ = new LoweredVoidCallInstruction(
            "Target",
            [IRType.Int32, IRType.Int32],
            [
                new LoweredCallArgument(0, new LoweredIntegerConstant(1)),
                new LoweredCallArgument(0, new LoweredIntegerConstant(2))
            ]);

        action
            .Should()
            .Throw<ArgumentException>();
    }

    [TestCase(TestName = "Void Call Should Reject Missing Required Arguments")]
    public void LowerShould_2()
    {
        Action action = () => _ = new LoweredVoidCallInstruction(
            "Target",
            [IRType.Int32, IRType.Bool],
            [new LoweredCallArgument(0, new LoweredIntegerConstant(1))]);

        action
            .Should()
            .Throw<ArgumentException>();
    }
}
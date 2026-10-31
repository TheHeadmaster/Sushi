using FluentAssertions;
using LLVMSharp.Interop;
using NUnit.Framework;
using Sushi.Backends;
using Sushi.Backends.LLVM;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;

namespace Sushi.Testing.Intermediate.Lowering;

/// <summary>
/// Verifies typed value-call results, source-order argument evaluation,
/// and native LLVM validity for nested calls.
/// </summary>
[TestFixture]
public class ValueCallLoweringTests
{
    [TestCase(TestName = "Nested Value Calls Should Lower In Source Order Before Outer Invocation")]
    public void LowerShould_0()
    {
        IRFunction caller = new(
            IRAccessibility.Public,
            "Caller",
            IRType.Void,
            new IRBlock(
            [
                new IRVoidCallStatement(
                    "Use",
                    [IRType.Int32, IRType.Bool],
                    [
                        new IRCallArgument(1, new IRCallExpression("Truth", IRType.Bool, [], [])),
                        new IRCallArgument(0, new IRCallExpression("Compute", IRType.Int32, [], []))
                    ])
            ]));

        IRFunction truth = new(
            IRAccessibility.Public,
            "Truth",
            IRType.Bool,
            new IRBlock([new IRReturnStatement(new IRBooleanConstant(true))]));

        IRFunction compute = new(
            IRAccessibility.Public,
            "Compute",
            IRType.Int32,
            new IRBlock([new IRReturnStatement(new IRIntegerConstant(42))]));

        IRFunction use = new(
            IRAccessibility.Public,
            "Use",
            IRType.Void,
            [
                new IRParameter(0, "number", IRType.Int32),
                new IRParameter(1, "enabled", IRType.Bool)
            ],
            new IRBlock([]));

        LoweredFunction loweredCaller = IRFunctionLowerer.Lower(caller, CancellationToken.None);

        loweredCaller.EntryBlock.Instructions
            .Should()
            .HaveCount(3);

        LoweredValueCallInstruction first = loweredCaller.EntryBlock.Instructions[0]
            .Should()
            .BeOfType<LoweredValueCallInstruction>()
            .Subject;

        LoweredValueCallInstruction second = loweredCaller.EntryBlock.Instructions[1]
            .Should()
            .BeOfType<LoweredValueCallInstruction>()
            .Subject;

        LoweredVoidCallInstruction outer = loweredCaller.EntryBlock.Instructions[2]
            .Should()
            .BeOfType<LoweredVoidCallInstruction>()
            .Subject;

        first.Symbol
            .Should()
            .Be("Truth");

        first.Destination.Type
            .Should()
            .Be(IRType.Bool);

        second.Symbol
            .Should()
            .Be("Compute");

        second.Destination.Type
            .Should()
            .Be(IRType.Int32);

        outer.Arguments[0].ParameterIndex
            .Should()
            .Be(1);

        outer.Arguments[1].ParameterIndex
            .Should()
            .Be(0);

        using IModuleArtifact artifact = new LLVMModuleEmitter().Emit(
            "NestedValueCalls",
            [
                loweredCaller,
                IRFunctionLowerer.Lower(use, CancellationToken.None),
                IRFunctionLowerer.Lower(truth, CancellationToken.None),
                IRFunctionLowerer.Lower(compute, CancellationToken.None)
            ],
            CancellationToken.None);

        LLVMModuleArtifact module = (LLVMModuleArtifact)artifact;

        module.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string message)
            .Should()
            .BeTrue(message);
    }

    [TestCase(TestName = "Value Call Should Reject Missing Parameter Binding")]
    public void LowerShould_1()
    {
        Action action = () => _ = new LoweredValueCallInstruction(
            new LoweredTemporaryValue(0, IRType.Int32),
            "Calculate",
            [IRType.Int32],
            []);

        action
            .Should()
            .Throw<ArgumentException>();
    }

    [TestCase(TestName = "Value Call Should Reject Void Destination")]
    public void LowerShould_2()
    {
        Action action = () => _ = new LoweredValueCallInstruction(
            new LoweredTemporaryValue(0, IRType.Void),
            "Calculate",
            [],
            []);

        action
            .Should()
            .Throw<ArgumentException>();
    }

    [TestCase(TestName = "Value Call Should Reject Incorrect Argument Type")]
    public void LowerShould_3()
    {
        Action action = () => _ = new LoweredValueCallInstruction(
            new LoweredTemporaryValue(0, IRType.Int32),
            "Calculate",
            [IRType.Bool],
            [new LoweredCallArgument(0, new LoweredIntegerConstant(42))]);

        action
            .Should()
            .Throw<ArgumentException>();
    }
}
using FluentAssertions;
using NUnit.Framework;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;

namespace Sushi.Testing.Intermediate.Lowering;

/// <summary>
/// Verifies that initialized local bindings reuse their established values
/// without duplicating initializer evaluation or function parameters.
/// </summary>
[TestFixture]
public class LocalInitializationLoweringTests
{
    [TestCase(TestName = "Multiple Local Reads Should Not Reevaluate Initializer Call")]
    public void LowerShouldReuseInitializedValue()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "Calculate",
            IRType.Int32,
            new IRBlock(
            [
                new IRLocalDeclarationStatement(
                    0,
                    IRType.Int32,
                    new IRCallExpression("GetValue", IRType.Int32, [], [])),
                new IRReturnStatement(
                    new IRIntegerBinaryExpression(
                        IRIntegerBinaryOperator.Add,
                        new IRLocalReferenceExpression(0, IRType.Int32),
                        new IRLocalReferenceExpression(0, IRType.Int32)))
            ]));

        LoweredFunction lowered = IRFunctionLowerer.Lower(function, CancellationToken.None);

        lowered.Blocks
            .SelectMany(block => block.Instructions)
            .OfType<LoweredValueCallInstruction>()
            .Should()
            .ContainSingle();

        lowered.EntryBlock.Terminator
            .Should()
            .BeOfType<LoweredReturnTerminator>();

        lowered.Parameters
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Uninitialized Local IR Reference Should Be Rejected")]
    public void LowerShouldRejectMissingLocalDefinition()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "Invalid",
            IRType.Int32,
            new IRBlock(
            [
                new IRReturnStatement(new IRLocalReferenceExpression(0, IRType.Int32))
            ]));

        Action action = () => IRFunctionLowerer.Lower(function, CancellationToken.None);

        action
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*has not been initialized*");
    }

    [TestCase(TestName = "Duplicate Local Declaration Identity Should Be Rejected")]
    public void LowerShouldRejectDuplicateLocalIdentity()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "Invalid",
            IRType.Int32,
            new IRBlock(
            [
                new IRLocalDeclarationStatement(0, IRType.Int32, new IRIntegerConstant(10)),
                new IRLocalDeclarationStatement(0, IRType.Int32, new IRIntegerConstant(20)),
                new IRReturnStatement(new IRLocalReferenceExpression(0, IRType.Int32))
            ]));

        Action action = () => IRFunctionLowerer.Lower(function, CancellationToken.None);

        action
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*already been initialized*");
    }

    [TestCase(TestName = "Local Reference Must Match Its Initialized Value Type")]
    public void LowerShouldRejectLocalTypeMismatch()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "Invalid",
            IRType.Bool,
            new IRBlock(
            [
                new IRLocalDeclarationStatement(0, IRType.Int32, new IRIntegerConstant(10)),
                new IRReturnStatement(new IRLocalReferenceExpression(0, IRType.Bool))
            ]));

        Action action = () => IRFunctionLowerer.Lower(function, CancellationToken.None);

        action
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*does not match its initialized value*");
    }
}
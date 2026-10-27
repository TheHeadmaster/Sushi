using FluentAssertions;
using LLVMSharp.Interop;
using NUnit.Framework;
using Sushi.Analysis;
using Sushi.Backends;
using Sushi.Backends.LLVM;
using Sushi.Diagnostics;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.Testing.Intermediate;

[TestFixture]
public class BooleanOperationStructureTests
{
    private static readonly Uri testUri = new("file:///TestProject/BooleanOperations.sus");

    [TestCase("true and false", BooleanBinaryOperator.LogicalAnd, IRBooleanBinaryOperator.LogicalAnd, false)]
    [TestCase("true or false", BooleanBinaryOperator.LogicalOr, IRBooleanBinaryOperator.LogicalOr, true)]
    [TestCase("true & false", BooleanBinaryOperator.EagerAnd, IRBooleanBinaryOperator.EagerAnd, false)]
    [TestCase("true | false", BooleanBinaryOperator.EagerOr, IRBooleanBinaryOperator.EagerOr, true)]
    [TestCase("true ^ false", BooleanBinaryOperator.EagerXor, IRBooleanBinaryOperator.EagerXor, true)]
    public async Task ConvertShould_0(string expression, BooleanBinaryOperator boundOperator, IRBooleanBinaryOperator irOperator, bool expectedValue)
    {
        (BoundFunction function, IReadOnlyList<SushiDiagnostic> diagnostics) = await BindExpression(expression);

        BoundReturnStatement statement = function.Body.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<BoundReturnStatement>()
            .Subject;

        BoundBooleanBinaryExpression boundBinary = statement.Expression
            .Should()
            .BeOfType<BoundBooleanBinaryExpression>()
            .Subject;

        boundBinary.Operator
            .Should()
            .Be(boundOperator);

        boundBinary.Left.Type
            .Should()
            .Be(BoundType.Bool);

        boundBinary.Right.Type
            .Should()
            .Be(BoundType.Bool);

        IRFunction structured = BoundFunctionIRConverter.Convert(function, CancellationToken.None);

        IRReturnStatement irReturn = structured.Body.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<IRReturnStatement>()
            .Subject;

        IRBooleanBinaryExpression irBinary = irReturn.Expression
            .Should()
            .BeOfType<IRBooleanBinaryExpression>()
            .Subject;

        irBinary.Operator
            .Should()
            .Be(irOperator);

        irBinary.Left
            .Should()
            .BeOfType<IRBooleanConstant>();

        irBinary.Right
            .Should()
            .BeOfType<IRBooleanConstant>();

        LoweredFunction lowered = IRFunctionLowerer.Lower(structured, CancellationToken.None);

        lowered.EntryBlock.Instructions
            .Should()
            .BeEmpty();

        LoweredReturnTerminator terminator = lowered.EntryBlock.Terminator
            .Should()
            .BeOfType<LoweredReturnTerminator>()
            .Subject;

        LoweredBooleanConstant constant = terminator.Value
            .Should()
            .BeOfType<LoweredBooleanConstant>()
            .Subject;

        constant.Value
            .Should()
            .Be(expectedValue);

        IModuleEmitter moduleEmitter = new LLVMModuleEmitter();

        using IModuleArtifact moduleArtifact = moduleEmitter.Emit("BooleanOperations", lowered, CancellationToken.None);

        LLVMModuleArtifact llvmModuleArtifact = moduleArtifact
            .Should()
            .BeOfType<LLVMModuleArtifact>()
            .Subject;

        llvmModuleArtifact.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string verificationMessage)
            .Should()
            .BeTrue(verificationMessage);

        llvmModuleArtifact.Module.PrintToString()
            .Should()
            .Contain(expectedValue ? "ret i1 true" : "ret i1 false");

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Negation Should Remain An Operation Until Lowering")]
    public async Task ConvertShould_1()
    {
        (BoundFunction function, IReadOnlyList<SushiDiagnostic> diagnostics) = await BindExpression("not (true and false)");

        BoundReturnStatement boundReturn = function.Body.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<BoundReturnStatement>()
            .Subject;

        BoundBooleanNotExpression negation = boundReturn.Expression
            .Should()
            .BeOfType<BoundBooleanNotExpression>()
            .Subject;

        BoundBooleanBinaryExpression conjunction = negation.Operand
            .Should()
            .BeOfType<BoundBooleanBinaryExpression>()
            .Subject;

        conjunction.Operator
            .Should()
            .Be(BooleanBinaryOperator.LogicalAnd);

        IRFunction structured = BoundFunctionIRConverter.Convert(function, CancellationToken.None);

        IRReturnStatement irReturn = structured.Body.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<IRReturnStatement>()
            .Subject;

        IRBooleanNotExpression irNegation = irReturn.Expression
            .Should()
            .BeOfType<IRBooleanNotExpression>()
            .Subject;

        IRBooleanBinaryExpression irConjunction = irNegation.Operand
            .Should()
            .BeOfType<IRBooleanBinaryExpression>()
            .Subject;

        irConjunction.Operator
            .Should()
            .Be(IRBooleanBinaryOperator.LogicalAnd);

        LoweredFunction lowered = IRFunctionLowerer.Lower(structured, CancellationToken.None);

        LoweredReturnTerminator terminator = lowered.EntryBlock.Terminator
            .Should()
            .BeOfType<LoweredReturnTerminator>()
            .Subject;

        LoweredBooleanConstant result = terminator.Value
            .Should()
            .BeOfType<LoweredBooleanConstant>()
            .Subject;

        result.Value
            .Should()
            .BeTrue();

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Bound Boolean Binary Nodes Should Reject Integer Operands")]
    public void ConvertShould_2()
    {
        Action action = () => _ = new BoundBooleanBinaryExpression(
            BooleanBinaryOperator.EagerAnd,
            new BoundBooleanConstantExpression(true),
            new BoundIntegerLiteralExpression(1));

        action
            .Should()
            .Throw<ArgumentException>();
    }

    [TestCase(TestName = "IR Boolean Binary Nodes Should Reject Integer Operands")]
    public void ConvertShould_3()
    {
        Action action = () => _ = new IRBooleanBinaryExpression(
            IRBooleanBinaryOperator.EagerAnd,
            new IRBooleanConstant(true),
            new IRIntegerConstant(1));

        action
            .Should()
            .Throw<ArgumentException>();
    }

    private static async Task<(BoundFunction Function, IReadOnlyList<SushiDiagnostic> Diagnostics)> BindExpression(string expression)
    {
        string source = $$"""
            package Example;
            namespace Example;

            public bool launch() {
                return {{expression}};
            }
            """;

        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);
        IDiagnosticReporter reporter = new DiagnosticReporter();

        SourceAnalysisResult result = (SourceAnalysisResult)await new SourceAnalyzer().Analyze(snapshot, reporter, CancellationToken.None);

        BoundFunction function = result.Functions
            .Should()
            .ContainSingle()
            .Which;

        return (function, reporter.ReportDiagnostics());
    }
}
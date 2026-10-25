using FluentAssertions;
using LLVMSharp.Interop;
using NUnit.Framework;
using Sushi.Analysis;
using Sushi.Backends.LLVM;
using Sushi.Diagnostics;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.Testing.Semantics;

[TestFixture]
public class BooleanConstantExpressionBinderTests
{
    private static readonly Uri testUri = new("file:///TestProject/BooleanConstants.sus");

    [TestCase("true", true, TestName = "Binder Should Bind True Literal")]
    [TestCase("false", false, TestName = "Binder Should Bind False Literal")]
    [TestCase("not true", false, TestName = "Binder Should Negate True")]
    [TestCase("not false", true, TestName = "Binder Should Negate False")]
    [TestCase("not not true", true, TestName = "Binder Should Evaluate Nested Negation")]
    [TestCase("not not false", false, TestName = "Binder Should Preserve False After Double Negation")]
    [TestCase("not not not false", true, TestName = "Binder Should Evaluate Triple Negation")]
    [TestCase("(true)", true, TestName = "Binder Should Bind Parenthesized Boolean Literal")]
    [TestCase("((false))", false, TestName = "Binder Should Bind Nested Parentheses")]
    [TestCase("not (not false)", false, TestName = "Binder Should Evaluate Parenthesized Negation")]
    public async Task BindShould_0(string expression, bool expectedValue)
    {
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalyzeExpression("bool", expression);

        BoundFunction function = result.Functions
            .Should()
            .ContainSingle()
            .Which;

        function.ReturnType
            .Should()
            .Be(BoundType.Bool);

        BoundReturnStatement statement = function.Body.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<BoundReturnStatement>()
            .Subject;

        BoundBooleanConstantExpression boundExpression = statement.Expression
            .Should()
            .BeOfType<BoundBooleanConstantExpression>()
            .Subject;

        boundExpression.Type
            .Should()
            .Be(BoundType.Bool);

        boundExpression.Value
            .Should()
            .Be(expectedValue);

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase("1", TestName = "Binder Should Reject Integer Literal As Boolean Result")]
    [TestCase("not 1", TestName = "Binder Should Reject Integer Operand Of Logical Negation")]
    public async Task BindShould_1(string expression)
    {
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalyzeExpression("bool", expression);

        result.Functions
            .Should()
            .BeEmpty();

        diagnostics
            .Should()
            .ContainSingle(item => item.IsType(ErrorType.UnsupportedMVPExpression));
    }

    [TestCase(TestName = "Binder Should Not Convert Boolean To Int32")]
    public async Task BindShould_2()
    {
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalyzeExpression("int32", "true");

        result.Functions
            .Should()
            .BeEmpty();

        diagnostics
            .Should()
            .ContainSingle(item => item.IsType(ErrorType.UnsupportedMVPExpression));
    }

    [TestCase(TestName = "Binder Should Preserve Missing Boolean Operand Diagnostic Without Duplication")]
    public async Task BindShould_3()
    {
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalyzeExpression("bool", "not");

        result.Functions
            .Should()
            .BeEmpty();

        diagnostics
            .Should()
            .ContainSingle(item => item.IsType(ErrorType.ExpectedSyntax));
    }

    [TestCase(TestName = "Boolean Constant Should Pass Through Structured IR And LLVM")]
    public async Task BindShould_4()
    {
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalyzeExpression("bool", "not false");

        BoundFunction bound = result.Functions
            .Should()
            .ContainSingle()
            .Which;

        IRFunction structured = BoundFunctionIRConverter.Convert(bound, CancellationToken.None);

        structured.ReturnType
            .Should()
            .Be(IRType.Bool);

        IRReturnStatement structuredReturn = structured.Body.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<IRReturnStatement>()
            .Subject;

        IRBooleanConstant constant = structuredReturn.Expression
            .Should()
            .BeOfType<IRBooleanConstant>()
            .Subject;

        constant.Type
            .Should()
            .Be(IRType.Bool);

        constant.Value
            .Should()
            .BeTrue();

        LoweredFunction lowered = IRFunctionLowerer.Lower(structured, CancellationToken.None);

        lowered.ReturnType
            .Should()
            .Be(IRType.Bool);

        LoweredReturnTerminator terminator = lowered.EntryBlock.Terminator
            .Should()
            .BeOfType<LoweredReturnTerminator>()
            .Subject;

        LoweredBooleanConstant loweredValue = terminator.Value
            .Should()
            .BeOfType<LoweredBooleanConstant>()
            .Subject;

        loweredValue.Type
            .Should()
            .Be(IRType.Bool);

        loweredValue.Value
            .Should()
            .BeTrue();

        using LLVMModuleArtifact artifact = new LLVMModuleEmitter().Emit("BooleanConstants", lowered, CancellationToken.None);

        artifact.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string verificationMessage)
            .Should()
            .BeTrue(verificationMessage);

        string llvm = artifact.Module.PrintToString();

        llvm
            .Should()
            .Contain("define i1 @launch()");

        llvm
            .Should()
            .Contain("ret i1 true");

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase(TestName = "Temporary Native Entry Shim Should Widen Boolean Return To Int32")]
    public void BindShould_5()
    {
        LoweredFunction function = new(
            IRAccessibility.Public,
            "launch",
            IRType.Bool,
            [
                new LoweredBasicBlock(
                    "entry",
                    [],
                    new LoweredReturnTerminator(new LoweredBooleanConstant(true)))
            ]);

        using LLVMModuleArtifact artifact = new LLVMModuleEmitter().Emit("BooleanEntryShim", function, CancellationToken.None);

        TemporaryMVPEntryShim.Add(artifact, function.Name, function.ReturnType);

        artifact.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string verificationMessage)
            .Should()
            .BeTrue(verificationMessage);

        string llvm = artifact.Module.PrintToString();

        llvm
            .Should()
            .Contain("@__sushi_mvp_entry_target()");

        llvm
            .Should()
            .Contain("zext i1");

        llvm
            .Should()
            .Contain("to i32");

        llvm
            .Should()
            .Contain("define i32 @main(");
    }

    private static async Task<(SourceAnalysisResult Result, IReadOnlyList<SushiDiagnostic> Diagnostics)> AnalyzeExpression(string returnType, string expression)
    {
        string source = $$"""
            package Example;
            namespace Example;

            public {{returnType}} launch() {
                return {{expression}};
            }
            """;

        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, source);
        IDiagnosticReporter reporter = new DiagnosticReporter();

        SourceAnalysisResult result = (SourceAnalysisResult)await new SourceAnalyzer().Analyze(snapshot, reporter, CancellationToken.None);

        return (result, reporter.ReportDiagnostics());
    }
}
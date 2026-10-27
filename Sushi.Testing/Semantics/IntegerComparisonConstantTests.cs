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

namespace Sushi.Testing.Semantics;

[TestFixture]
public class IntegerComparisonConstantTests
{
    private static readonly Uri testUri = new("file:///TestProject/IntegerComparisons.sus");

    [TestCase("1 == 1", true, TestName = "Binder Should Evaluate Equal Integers")]
    [TestCase("1 == 2", false, TestName = "Binder Should Evaluate Unequal Integers")]
    [TestCase("1 != 2", true, TestName = "Binder Should Evaluate Integer Inequality")]
    [TestCase("1 != 1", false, TestName = "Binder Should Evaluate Equal Values With Inequality")]
    [TestCase("1 < 2", true, TestName = "Binder Should Evaluate Less Than")]
    [TestCase("2 < 1", false, TestName = "Binder Should Evaluate False Less Than")]
    [TestCase("2 <= 2", true, TestName = "Binder Should Evaluate Less Than Or Equal")]
    [TestCase("3 <= 2", false, TestName = "Binder Should Evaluate False Less Than Or Equal")]
    [TestCase("3 > 2", true, TestName = "Binder Should Evaluate Greater Than")]
    [TestCase("2 > 3", false, TestName = "Binder Should Evaluate False Greater Than")]
    [TestCase("3 >= 3", true, TestName = "Binder Should Evaluate Greater Than Or Equal")]
    [TestCase("2 >= 3", false, TestName = "Binder Should Evaluate False Greater Than Or Equal")]
    [TestCase("-7 < -3", true, TestName = "Binder Should Compare Negative Integers")]
    [TestCase("-7 > 1", false, TestName = "Binder Should Compare Signed Integers")]
    [TestCase("1 + 2 < 3 * 4", true, TestName = "Binder Should Respect Arithmetic Precedence")]
    [TestCase("(5 << 2) > (3 * 6)", true, TestName = "Binder Should Compare Shift And Arithmetic Results")]
    [TestCase("1 | 2 ^ 3 & 1 == 3", true, TestName = "Binder Should Respect Bitwise And Equality Precedence")]
    [TestCase("2147483648 > 2147483647", true, TestName = "Binder Should Compare Constants Larger Than Int32")]
    [TestCase("-2147483649 < -2147483648", true, TestName = "Binder Should Compare Constants Smaller Than Int32")]
    [TestCase("2147483647 + 1 == 2147483648", true, TestName = "Binder Should Compare Exact Arithmetic Without Narrowing")]
    [TestCase("(1 << 32) == 4294967296", true, TestName = "Binder Should Permit Exact Untyped Shift Beyond Int32 Width")]
    [TestCase("(1 << 100) > 0", true, TestName = "Binder Should Compare Large Exact Shift Result")]
    [TestCase("2147483648 & 1 == 0", true, TestName = "Binder Should Use Untyped Bitwise Semantics In Comparison")]
    [TestCase("~2147483648 == -2147483649", true, TestName = "Binder Should Use Untyped Complement Semantics")]
    [TestCase("-7 >> 1 == -4", true, TestName = "Binder Should Preserve Arithmetic Right Shift In Comparison")]
    [TestCase("not (1 < 2)", false, TestName = "Binder Should Negate Integer Comparison")]
    [TestCase("not (1 == 2)", true, TestName = "Binder Should Negate Integer Equality")]
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

        statement.Expression.Type
            .Should()
            .Be(BoundType.Bool);
        
        IRFunction structured = BoundFunctionIRConverter.Convert(function, CancellationToken.None);
        LoweredFunction lowered = IRFunctionLowerer.Lower(structured, CancellationToken.None);
        
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

        diagnostics
            .Should()
            .BeEmpty();
    }

    [TestCase("true == false", TestName = "Binder Should Retain Unsupported Boolean Equality Boundary")]
    [TestCase("1 < true", TestName = "Binder Should Reject Boolean As Integer Comparison Operand")]
    [TestCase("1 == (2 < 3)", TestName = "Binder Should Reject Boolean Operand In Integer Equality")]
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

    [TestCase("1 < 2 < 3", TestName = "Binder Should Preserve Non Associative Relational Diagnostic")]
    [TestCase("1 == 2 != 3", TestName = "Binder Should Preserve Non Associative Equality Diagnostic")]
    [TestCase("1 < 2 < 3 == 4", TestName = "Binder Should Avoid Secondary Error For Nested Invalid Chain")]
    [TestCase("not (1 < 2 < 3)", TestName = "Binder Should Avoid Secondary Error For Negated Invalid Chain")]
    public async Task BindShould_2(string expression)
    {
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalyzeExpression("bool", expression);

        result.Functions
            .Should()
            .BeEmpty();

        diagnostics
            .Should()
            .ContainSingle(item => item.IsType(ErrorType.NonAssociativeComparison));
    }

    [TestCase("1 <", TestName = "Binder Should Preserve Missing Relational Operand Diagnostic")]
    [TestCase("1 ==", TestName = "Binder Should Preserve Missing Equality Operand Diagnostic")]
    public async Task BindShould_3(string expression)
    {
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalyzeExpression("bool", expression);

        result.Functions
            .Should()
            .BeEmpty();

        diagnostics
            .Should()
            .ContainSingle(item => item.IsType(ErrorType.ExpectedSyntax));
    }

    [TestCase("1 / 0 > 0", ErrorType.IntegerDivisionByZero, TestName = "Binder Should Diagnose Division By Zero Inside Comparison")]
    [TestCase("1 << -1 > 0", ErrorType.InvalidIntegerShiftCount, TestName = "Binder Should Diagnose Negative Untyped Shift Count")]
    public async Task BindShould_4(string expression, ErrorType expectedError)
    {
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalyzeExpression("bool", expression);

        result.Functions
            .Should()
            .BeEmpty();

        diagnostics
            .Should()
            .ContainSingle(item => item.IsType(expectedError));
    }

    [TestCase(TestName = "Integer Comparison Should Produce Boolean Structured IR And LLVM")]
    public async Task BindShould_5()
    {
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await AnalyzeExpression("bool", "(5 << 2) > (3 * 6)");

        BoundFunction boundFunction = result.Functions
            .Should()
            .ContainSingle()
            .Which;

        IRFunction structured = BoundFunctionIRConverter.Convert(boundFunction, CancellationToken.None);

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

        constant.Value
            .Should()
            .BeTrue();

        LoweredFunction lowered = IRFunctionLowerer.Lower(structured, CancellationToken.None);

        LoweredReturnTerminator terminator = lowered.EntryBlock.Terminator
            .Should()
            .BeOfType<LoweredReturnTerminator>()
            .Subject;

        LoweredBooleanConstant loweredConstant = terminator.Value
            .Should()
            .BeOfType<LoweredBooleanConstant>()
            .Subject;

        loweredConstant.Value
            .Should()
            .BeTrue();

        using IModuleArtifact artifact = new LLVMModuleEmitter().Emit("IntegerComparison", lowered, CancellationToken.None);

        LLVMModuleArtifact llvmArtifact = artifact
            .Should()
            .BeOfType<LLVMModuleArtifact>()
            .Subject;

        llvmArtifact.Module.TryVerify(LLVMVerifierFailureAction.LLVMReturnStatusAction, out string verificationMessage)
            .Should()
            .BeTrue(verificationMessage);

        llvmArtifact.Module.PrintToString()
            .Should()
            .Contain("ret i1 true");

        diagnostics
            .Should()
            .BeEmpty();
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
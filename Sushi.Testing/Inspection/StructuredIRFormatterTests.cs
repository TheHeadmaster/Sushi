using FluentAssertions;
using NUnit.Framework;
using Sushi.Inspection;
using Sushi.Intermediate;

namespace Sushi.Testing.Inspection;

/// <summary>
/// Verifies that structured IR formatting preserves representation details
/// and emits consistent semantic navigation metadata alongside generated text.
/// </summary>
[TestFixture]
public class StructuredIRFormatterTests
{
    [TestCase(TestName = "Structured IR Formatter Produces Readable Text And Source Ordered Symbol Occurrences")]
    public void FormatShouldProduceTextAndNavigationMetadata()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "__sushi_package_function_0",
            IRType.Int32,
            [
                new IRParameter(0, "left", IRType.Int32),
                new IRParameter(1, "right", IRType.Int32)
            ],
            new IRBlock([
                new IRLocalDeclarationStatement(0, IRType.Int32,
                    new IRIntegerBinaryExpression(IRIntegerBinaryOperator.Add,
                        new IRParameterReferenceExpression(0, IRType.Int32),
                        new IRParameterReferenceExpression(1, IRType.Int32))),
                new IRReturnStatement(new IRLocalReferenceExpression(0, IRType.Int32))
            ]));

        InspectionDocument document = StructuredIRFormatter.Format(function, CancellationToken.None);

        string expected = """
            public function int32 __sushi_package_function_0(int32 left, int32 right)
            {
                local int32 %local0 = (left + right);
                return %local0;
            }
            """.ReplaceLineEndings("\n") + "\n";

        document.Stage
            .Should()
            .Be(InspectionStage.StructuredIR);

        document.FunctionSymbol
            .Should()
            .Be("__sushi_package_function_0");

        document.Text
            .Should()
            .Be(expected);

        document.Occurrences
            .Should()
            .HaveCount(7);
        document.SymbolMetadata[new InspectionSymbolIdentity(InspectionSymbolKind.Local, function.Name, 0)]
            .Should()
            .Be(new InspectionValueMetadata(IRType.Int32, "%local0"));

        foreach (InspectionSymbolOccurrence occurrence in document.Occurrences)
        {
            occurrence.Start
                .Should()
                .BeGreaterThanOrEqualTo(0);

            occurrence.End
                .Should()
                .BeLessThanOrEqualTo(document.Text.Length);

            GetOccurrenceText(document, occurrence)
                .Should()
                .NotBeNullOrEmpty();
        }

        for (int i = 1; i < document.Occurrences.Count; i++)
        {
            document.Occurrences[i].Start
                .Should()
                .BeGreaterThanOrEqualTo(document.Occurrences[i - 1].End);
        }

        AssertSymbol(document, InspectionSymbolKind.Function, "__sushi_package_function_0", null, true, 1);
        AssertSymbol(document, InspectionSymbolKind.Parameter, "__sushi_package_function_0", 0, true, 1);
        AssertSymbol(document, InspectionSymbolKind.Parameter, "__sushi_package_function_0", 0, false, 1);
        AssertSymbol(document, InspectionSymbolKind.Parameter, "__sushi_package_function_0", 1, true, 1);
        AssertSymbol(document, InspectionSymbolKind.Parameter, "__sushi_package_function_0", 1, false, 1);
        AssertSymbol(document, InspectionSymbolKind.Local, "__sushi_package_function_0", 0, true, 1);
        AssertSymbol(document, InspectionSymbolKind.Local, "__sushi_package_function_0", 0, false, 1);
    }

    [TestCase(TestName = "Function Calls And Named Argument Positions Retain Callee Semantic Identity")]
    public void CallsShouldReferenceCalleeDefinitionsAndParameters()
    {
        IRFunction target = new(
            IRAccessibility.Public,
            "__sushi_package_function_1",
            IRType.Void,
            [
                new IRParameter(0, "amount", IRType.Int32),
                new IRParameter(1, "enabled", IRType.Bool)
            ],
            new IRBlock([new IRReturnStatement(null)]));

        IRFunction caller = new(
            IRAccessibility.Public,
            "__sushi_package_function_0",
            IRType.Void,
            new IRBlock([
                new IRVoidCallStatement(
                    "__sushi_package_function_1",
                    [IRType.Int32, IRType.Bool],
                    [
                        new IRCallArgument(1, new IRBooleanConstant(true)),
                        new IRCallArgument(0, new IRIntegerConstant(42))
                    ]),
                new IRReturnStatement(null)
            ]));

        InspectionDocument targetDocument = StructuredIRFormatter.Format(target, CancellationToken.None);
        InspectionDocument callerDocument = StructuredIRFormatter.Format(caller, CancellationToken.None);

        callerDocument.Text
            .Should()
            .Contain("call void __sushi_package_function_1(bool arg1: true, int32 arg0: 42);");

        InspectionSymbolOccurrence definition = targetDocument.Occurrences
            .Single(occurrence => occurrence.Identity.Kind is InspectionSymbolKind.Function && occurrence.IsDefinition);

        InspectionSymbolOccurrence reference = callerDocument.Occurrences
            .Single(occurrence => occurrence.Identity.Kind is InspectionSymbolKind.Function && !occurrence.IsDefinition);

        reference.Identity
            .Should()
            .Be(definition.Identity);

        InspectionSymbolIdentity parameterZero = new(InspectionSymbolKind.Parameter, target.Name, 0);
        InspectionSymbolIdentity parameterOne = new(InspectionSymbolKind.Parameter, target.Name, 1);

        targetDocument.Occurrences
            .Should()
            .ContainSingle(occurrence => occurrence.Identity == parameterZero && occurrence.IsDefinition);

        callerDocument.Occurrences
            .Should()
            .ContainSingle(occurrence => occurrence.Identity == parameterZero && !occurrence.IsDefinition);

        targetDocument.Occurrences
            .Should()
            .ContainSingle(occurrence => occurrence.Identity == parameterOne && occurrence.IsDefinition);

        callerDocument.Occurrences
            .Should()
            .ContainSingle(occurrence => occurrence.Identity == parameterOne && !occurrence.IsDefinition);

        InspectionSymbolOccurrence argumentOne = callerDocument.Occurrences
            .Single(occurrence => occurrence.Identity == parameterOne);

        InspectionSymbolOccurrence argumentZero = callerDocument.Occurrences
            .Single(occurrence => occurrence.Identity == parameterZero);

        argumentOne.Start
            .Should()
            .BeLessThan(argumentZero.Start);
    }

    [TestCase(TestName = "Identical Local Indices In Different Functions Have Distinct Symbol Identities")]
    public void LocalIdentitiesShouldBeScopedToTheirContainingFunction()
    {
        IRFunction first = CreateLocalFunction("first");
        IRFunction second = CreateLocalFunction("second");

        InspectionDocument firstDocument = StructuredIRFormatter.Format(first, CancellationToken.None);
        InspectionDocument secondDocument = StructuredIRFormatter.Format(second, CancellationToken.None);

        InspectionSymbolOccurrence firstDefinition = firstDocument.Occurrences
            .Single(occurrence => occurrence.Identity.Kind is InspectionSymbolKind.Local && occurrence.IsDefinition);

        InspectionSymbolOccurrence secondDefinition = secondDocument.Occurrences
            .Single(occurrence => occurrence.Identity.Kind is InspectionSymbolKind.Local && occurrence.IsDefinition);

        GetOccurrenceText(firstDocument, firstDefinition)
            .Should()
            .Be("%local0");

        GetOccurrenceText(secondDocument, secondDefinition)
            .Should()
            .Be("%local0");

        firstDefinition.Identity
            .Should()
            .NotBe(secondDefinition.Identity);

        InspectionSymbolOccurrence firstReference = firstDocument.Occurrences
            .Single(occurrence => occurrence.Identity.Kind is InspectionSymbolKind.Local && !occurrence.IsDefinition);

        firstReference.Identity
            .Should()
            .Be(firstDefinition.Identity);
    }

    [TestCase(TestName = "Structured Loops And Conditionals Retain Distinct Execution Phases")]
    public void ControlFlowShouldRetainStructuredPhases()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "Control",
            IRType.Void,
            new IRBlock([
                new IRLocalDeclarationStatement(0, IRType.Int32, new IRIntegerConstant(0)),
                new IRWhileStatement(
                    new IRIntegerComparisonExpression(IRIntegerComparisonOperator.LessThan,
                        new IRLocalReferenceExpression(0, IRType.Int32),
                        new IRIntegerConstant(10)),
                    new IRBlock([
                        new IRIntegerUpdateStatement(new IRIntegerUpdateExpression(0, true, false)),
                        new IRLoopTransferStatement(false, 1)
                    ])),
                new IRDoWhileStatement(
                    new IRBlock([
                        new IRIntegerUpdateStatement(new IRIntegerUpdateExpression(0, false, true))
                    ]),
                    new IRBooleanConstant(false)),
                new IRForStatement(
                    new IRBlock([
                        new IRLocalAssignmentStatement(0, IRType.Int32, new IRIntegerConstant(1))
                    ]),
                    new IRBooleanConstant(true),
                    new IRBlock([
                        new IRIntegerUpdateStatement(new IRIntegerUpdateExpression(0, true, true))
                    ]),
                    new IRBlock([
                        new IRIfStatement(
                            new IRBooleanConstant(true),
                            new IRBlock([new IRLoopTransferStatement(true, 1)]),
                            new IRBlock([new IRLoopTransferStatement(false, 1)]))
                    ])),
                new IRReturnStatement(null)
            ]));

        InspectionDocument document = StructuredIRFormatter.Format(function, CancellationToken.None);

        document.Text
            .Should()
            .Contain("while ((%local0 < 10))");

        document.Text
            .Should()
            .Contain("update %local0++;");

        document.Text
            .Should()
            .Contain("update --%local0;");

        document.Text
            .Should()
            .Contain("initialize\n");

        document.Text
            .Should()
            .Contain("condition (true);");

        document.Text
            .Should()
            .Contain("iterate\n");

        document.Text
            .Should()
            .Contain("body\n");

        document.Text
            .Should()
            .Contain("break depth 1;");

        document.Text
            .Should()
            .Contain("continue depth 1;");

        AssertSymbol(document, InspectionSymbolKind.Local, "Control", 0, true, 1);
        AssertSymbol(document, InspectionSymbolKind.Local, "Control", 0, false, 5);
    }

    [TestCase(TestName = "Range Resolved Comparisons Retain Mandatory Operand Evaluation")]
    public void RangeComparisonShouldPreserveEvaluatedOperand()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "Evaluate",
            IRType.Bool,
            new IRBlock([
                new IRLocalDeclarationStatement(0, IRType.Int32, new IRIntegerConstant(0)),
                new IRReturnStatement(
                    new IRIntegerRangeComparisonExpression(
                        new IRIntegerUpdateExpression(0, true, true), false))
            ]));

        InspectionDocument document = StructuredIRFormatter.Format(function, CancellationToken.None);

        document.Text
            .Should()
            .Contain("return evaluate_then(++%local0, result: false);");

        AssertSymbol(document, InspectionSymbolKind.Local, "Evaluate", 0, true, 1);
        AssertSymbol(document, InspectionSymbolKind.Local, "Evaluate", 0, false, 1);
    }

    [TestCase(TestName = "Repeated Formatting Is Deterministic And Does Not Modify The IR")]
    public void FormattingShouldBeDeterministic()
    {
        IRFunction function = CreateLocalFunction("Deterministic");

        InspectionDocument first = StructuredIRFormatter.Format(function, CancellationToken.None);
        InspectionDocument second = StructuredIRFormatter.Format(function, CancellationToken.None);

        first.Text
            .Should()
            .Be(second.Text);

        first.Occurrences
            .Should()
            .Equal(second.Occurrences);

        function.Body.Statements
            .Should()
            .HaveCount(2);
    }

    [TestCase(TestName = "Unsupported Structured IR Nodes Are Explicitly Rejected")]
    public void UnsupportedNodesShouldThrow()
    {
        IRFunction function = new(
            IRAccessibility.Public,
            "Unsupported",
            IRType.Int32,
            new IRBlock([
                new IRReturnStatement(new UnsupportedExpression())
            ]));

        Action format = () => StructuredIRFormatter.Format(function, CancellationToken.None);

        format
            .Should()
            .Throw<NotSupportedException>()
            .WithMessage("*UnsupportedExpression*");
    }

    [TestCase(TestName = "Cancelled Formatting Does Not Return A Partial Inspection Document")]
    public void CancelledFormattingShouldThrow()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        IRFunction function = CreateLocalFunction("Cancelled");
        Action format = () => StructuredIRFormatter.Format(function, cancellation.Token);

        format
            .Should()
            .Throw<OperationCanceledException>();
    }

    private static IRFunction CreateLocalFunction(string name)
        => new(
            IRAccessibility.Public,
            name,
            IRType.Int32,
            new IRBlock([
                new IRLocalDeclarationStatement(0, IRType.Int32, new IRIntegerConstant(42)),
                new IRReturnStatement(new IRLocalReferenceExpression(0, IRType.Int32))
            ]));

    private static string GetOccurrenceText(InspectionDocument document, InspectionSymbolOccurrence occurrence)
        => document.Text.Substring(occurrence.Start, occurrence.Length);

    private static void AssertSymbol(InspectionDocument document, InspectionSymbolKind kind, string owner, int? index, bool isDefinition, int expected)
    {
        InspectionSymbolIdentity identity = new(kind, owner, index);

        document.Occurrences
            .Count(occurrence => occurrence.Identity == identity && occurrence.IsDefinition == isDefinition)
            .Should()
            .Be(expected);
    }

    private sealed record UnsupportedExpression() : IRExpression(IRType.Int32);
}

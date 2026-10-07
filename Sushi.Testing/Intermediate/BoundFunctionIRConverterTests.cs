using FluentAssertions;
using NUnit.Framework;
using Sushi.Intermediate;
using Sushi.Semantics;

namespace Sushi.Testing.Intermediate;

[TestFixture]
public class BoundFunctionIRConverterTests
{
    [TestCase(TestName = "IR Converter Should Convert Minimum Bound Function")]
    public void ConvertShould_0()
    {
        BoundFunction boundFunction = new(Accessibility.Public, "main", BoundIntegerType.Int32, new BoundBlock(
        [ new BoundReturnStatement(new BoundIntegerLiteralExpression(42)) ]));

        IRFunction function = new BoundFunctionIRConverter().Convert(boundFunction, CancellationToken.None);

        function.Accessibility
            .Should()
            .Be(IRAccessibility.Public);

        function.Name
            .Should()
            .Be("main");

        function.ReturnType
            .Should()
            .Be(IRType.Int32);

        IRReturnStatement returnStatement = function.Body.Statements
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .BeOfType<IRReturnStatement>()
            .Subject;

        IRIntegerConstant expression = returnStatement.Expression
            .Should()
            .BeOfType<IRIntegerConstant>()
            .Subject;

        expression.Type
            .Should()
            .Be(IRType.Int32);

        expression.Value
            .Should()
            .Be(42);
    }
}
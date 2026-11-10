using FluentAssertions;
using NUnit.Framework;
using Sushi.Inspection;
using Sushi.Intermediate;
using Sushi.Intermediate.Lowering;

namespace Sushi.Testing.Inspection;

/// <summary>
/// Confirms that program termination remains visible in both current IR inspection views.
/// </summary>
[TestFixture]
public class ProgramTerminationIRFormatterTests
{
    [TestCase(TestName = "IR Formatters Should Render Program Exit Without Treating It As Return")]
    public void FormatShould_RenderProgramTermination()
    {
        IRFunction structured = new(
            IRAccessibility.Public,
            "terminate",
            IRType.Void,
            new IRBlock([new IRExitStatement(new IRIntegerConstant(42))]));

        InspectionDocument structuredDocument = StructuredIRFormatter.Format(structured, CancellationToken.None);
        LoweredFunction lowered = IRFunctionLowerer.Lower(structured, CancellationToken.None);
        InspectionDocument loweredDocument = LoweredIRFormatter.Format(lowered, CancellationToken.None);

        structuredDocument.Text.Should().Contain("exit 42;");
        structuredDocument.Text.Should().NotContain("return");
        loweredDocument.Text.Should().Contain("exit 42;");
        loweredDocument.Text.Should().NotContain("return");
    }
}

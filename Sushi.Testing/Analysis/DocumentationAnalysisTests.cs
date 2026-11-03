using System.Text;
using FluentAssertions;
using NUnit.Framework;
using Sushi.Analysis;
using Sushi.Diagnostics;
using Sushi.Documentation;
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.Testing.Analysis;

[TestFixture]
public sealed class DocumentationAnalysisTests
{
    private const string Header = "package Example;\nnamespace Example;\n";
    private static readonly Uri testUri = new("file:///TestProject/Documentation.sus");

    [TestCase("\n")]
    [TestCase("\r\n")]
    [TestCase("\r")]
    public async Task ConsecutiveIndentedDocumentationLinesShouldFormOneSourceBackedBlock(string lineEnding)
    {
        string source = Header + "  /// first line" + lineEnding + "\t/// second line" + lineEnding + "public int32 Calculate() { return 1; }";
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);

        DocumentationBlock block = result.DocumentationBlocks.Should().ContainSingle().Subject;
        int firstStart = ByteOffset(source, source.IndexOf("/// first", StringComparison.Ordinal));
        int secondStart = ByteOffset(source, source.IndexOf("/// second", StringComparison.Ordinal));

        block.LineSpans
            .Should()
            .HaveCount(2);

        block.Span.Start
            .Should()
            .Be(firstStart);

        block.Span.End
            .Should()
            .Be(secondStart + Encoding.UTF8.GetByteCount("/// second line"));

        block.Declaration
            .Should()
            .BeOfType<FunctionDeclarationSyntax>();

        block.Snapshot
            .Should()
            .BeSameAs(result.Snapshot);

        diagnostics
            .Where(IsDetachedDocumentation)
            .Should()
            .BeEmpty();
    }

    [Test]
    public async Task EmptyDocumentationLineShouldRemainInTheBlock()
    {
        string source = Header + "/// first paragraph\n///\n/// second paragraph\npublic int32 Calculate() { return 1; }";
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);

        DocumentationBlock block = result.DocumentationBlocks.Should().ContainSingle().Subject;

        block.LineSpans
            .Should()
            .HaveCount(3);

        source.Substring(
                block.LineSpans[1].Start,
                block.LineSpans[1].Length)
            .Should()
            .Be("///");

        diagnostics
            .Where(IsDetachedDocumentation)
            .Should()
            .BeEmpty();
    }

    [TestCase("\n\n")]
    [TestCase("\n \t\n")]
    public async Task PhysicalBlankLineShouldSeparateDocumentationBlocks(string separator)
    {
        string source = Header + "/// detached" + separator + "/// attached\npublic int32 Calculate() { return 1; }";
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);

        result.DocumentationBlocks
            .Should()
            .HaveCount(2);

        result.DocumentationBlocks[0].IsAttached
            .Should()
            .BeFalse();

        result.DocumentationBlocks[1].Declaration
            .Should()
            .BeOfType<FunctionDeclarationSyntax>();

        diagnostics
            .Where(IsDetachedDocumentation)
            .Should()
            .ContainSingle();
    }

    [TestCase("// interruption\n")]
    [TestCase("/* interruption */\n")]
    public async Task OrdinaryCommentShouldInterruptDocumentationGrouping(string interruption)
    {
        string source = Header + "/// detached\n" + interruption + "/// attached\npublic int32 Calculate() { return 1; }";
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);

        result.DocumentationBlocks
            .Should()
            .HaveCount(2);

        result.DocumentationBlocks[0].IsAttached
            .Should()
            .BeFalse();

        result.DocumentationBlocks[1].IsAttached
            .Should()
            .BeTrue();

        diagnostics
            .Where(IsDetachedDocumentation)
            .Should()
            .ContainSingle();
    }

    [Test]
    public async Task DocumentationAtEndOfFileShouldBeRetainedAndWarnedOnce()
    {
        string source = Header + "/// at end of file\n/// continued at end of file";
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);

        DocumentationBlock block = result.DocumentationBlocks.Should().ContainSingle().Subject;
        SushiDiagnostic warning = diagnostics.Where(IsDetachedDocumentation).Should().ContainSingle().Subject;

        block.Declaration
            .Should()
            .BeNull();

        block.LineSpans
            .Should()
            .HaveCount(2);

        warning.DisplayName
            .Should()
            .Be("SUSW0003");

        warning.Severity
            .Should()
            .Be(DiagnosticSeverity.Warning);

        warning.Span
            .Should()
            .Be(block.Span);
    }

    [Test]
    public async Task DocumentationShouldAttachToPublicAndNonPublicPackageFunctionsInOrder()
    {
        string source = Header + "/// public function\npublic int32 First() { return 1; }\n/// private function\nprivate int32 Second() { return 2; }";
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);

        result.DocumentationBlocks[0].Declaration
            .Should()
            .BeSameAs(AnalysisHelper.GetSourceFile(result).FunctionDeclarations[0]);

        result.DocumentationBlocks[1].Declaration
            .Should()
            .BeSameAs(AnalysisHelper.GetSourceFile(result).FunctionDeclarations[1]);

        diagnostics
            .Where(IsDetachedDocumentation)
            .Should()
            .BeEmpty();
    }

    [Test]
    public async Task ActivityAndLifecycleMethodDocumentationShouldAttachIndependently()
    {
        string source = Header + """
            /// activity documentation
            public root activity Application : task
            {
                /// entry documentation
                public entry void Start() { return; }
                /// stop documentation
                public stop void Stop() { return; }
            }
            """;

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);
        ActivityDeclarationSyntax activity = AnalysisHelper.GetSourceFile(result).ActivityDeclarations.Should().ContainSingle().Subject;
        ActivityMethodDeclarationSyntax[] methods = activity.Members.OfType<ActivityMethodDeclarationSyntax>().ToArray();

        result.DocumentationBlocks
            .Should()
            .HaveCount(3);

        result.DocumentationBlocks[0].Declaration
            .Should()
            .BeSameAs(activity);

        result.DocumentationBlocks[1].Declaration
            .Should()
            .BeSameAs(methods[0]);

        result.DocumentationBlocks[2].Declaration
            .Should()
            .BeSameAs(methods[1]);

        diagnostics
            .Where(IsDetachedDocumentation)
            .Should()
            .BeEmpty();
    }

    [Test]
    public async Task DocumentationBeforePackageAndNamespaceDeclarationsShouldRemainDetached()
    {
        string source = "/// package\npackage Example;\n/// namespace\nnamespace Example;\n/// function\npublic int32 Calculate() { return 1; }";
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);

        result.DocumentationBlocks
            .Select(block => block.IsAttached)
            .Should()
            .Equal(false, false, true);

        diagnostics
            .Where(IsDetachedDocumentation)
            .Should()
            .HaveCount(2);
    }

    [Test]
    public async Task DocumentationBeforeUnsupportedSourceShouldNotAttachToLaterFunction()
    {
        string source = Header + "/// before unsupported syntax\nusing Example.Other;\npublic int32 Later() { return 1; }";
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);

        result.DocumentationBlocks
            .Should()
            .ContainSingle(block => !block.IsAttached);

        AnalysisHelper.GetSourceFile(result).FunctionDeclarations
            .Should()
            .BeEmpty();

        diagnostics
            .Where(IsDetachedDocumentation)
            .Should()
            .ContainSingle();
    }

    [Test]
    public async Task DocumentationInsideFunctionBodyShouldNotAttachToFollowingTopLevelFunction()
    {
        string source = Header + "public int32 First() {\n/// local declaration\nint32 value = 1;\nreturn value;\n}\npublic int32 Next() { return 2; }";
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);

        result.DocumentationBlocks
            .Should()
            .ContainSingle(block => !block.IsAttached);

        diagnostics
            .Where(IsDetachedDocumentation)
            .Should()
            .ContainSingle();
    }

    [Test]
    public async Task DetachedDocumentationShouldProduceOneUtf8AccurateWarningWithoutChangingOtherDiagnostics()
    {
        string source = Header + "/// café 😀\n\n42foo";
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);
        SushiDiagnostic warning = diagnostics.Where(IsDetachedDocumentation).Should().ContainSingle().Subject;
        DocumentationBlock block = result.DocumentationBlocks.Should().ContainSingle().Subject;
        int commentStart = source.IndexOf("///", StringComparison.Ordinal);
        int commentEnd = source.IndexOf('\n', commentStart);

        warning.Span.Snapshot
            .Should()
            .BeSameAs(result.Snapshot);

        warning.Span.Start
            .Should()
            .Be(ByteOffset(source, commentStart));

        warning.Span.End
            .Should()
            .Be(ByteOffset(source, commentEnd));

        block.Span
            .Should()
            .Be(warning.Span);

        diagnostics
            .Should()
            .Contain(diagnostic => diagnostic.IsType(ErrorType.MissingLexicalSeparation));
    }

    [Test]
    public async Task ReanalysisShouldUseNewSnapshotAndLeavePreviousDocumentationUnchanged()
    {
        SourceSnapshot firstSnapshot = SourceSnapshot.FromText(testUri, 1, Header + "/// docs\npublic int32 Calculate() { return 1; }");
        SourceSnapshot secondSnapshot = SourceSnapshot.FromText(testUri, 2, "// inserted\n" + Header + "/// docs\n\npublic int32 Calculate() { return 1; }");
        (SourceAnalysisResult first, IReadOnlyList<SushiDiagnostic> firstDiagnostics) = await Analyze(firstSnapshot);
        (SourceAnalysisResult second, IReadOnlyList<SushiDiagnostic> secondDiagnostics) = await Analyze(secondSnapshot);

        DocumentationBlock firstBlock = first.DocumentationBlocks.Should().ContainSingle().Subject;
        DocumentationBlock secondBlock = second.DocumentationBlocks.Should().ContainSingle().Subject;

        firstBlock.IsAttached
            .Should()
            .BeTrue();

        firstDiagnostics
            .Where(IsDetachedDocumentation)
            .Should()
            .BeEmpty();

        secondBlock.IsAttached
            .Should()
            .BeFalse();

        secondDiagnostics
            .Where(IsDetachedDocumentation)
            .Should()
            .ContainSingle();

        firstBlock.Snapshot
            .Should()
            .BeSameAs(firstSnapshot);

        secondBlock.Snapshot
            .Should()
            .BeSameAs(secondSnapshot);

        firstBlock.Span.Start
            .Should()
            .Be(ByteOffset(firstSnapshot.Text, firstSnapshot.Text.IndexOf("///", StringComparison.Ordinal)));

        secondBlock.Span.Start
            .Should()
            .Be(ByteOffset(secondSnapshot.Text, secondSnapshot.Text.IndexOf("///", StringComparison.Ordinal)));

        firstBlock.Span.Start
            .Should()
            .NotBe(secondBlock.Span.Start);
    }

    [Test]
    public async Task SignatureOnlyAndFullAnalysisShouldEachReportOneDetachedWarning()
    {
        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, version: null, Header + "/// detached\n\npublic int32 Calculate() { return 1; }");
        SourceAnalyzer analyzer = new();
        DiagnosticReporter signatureDiagnostics = new();
        DiagnosticReporter fullDiagnostics = new();

        SourceAnalysisResult signatureResult = (SourceAnalysisResult)await analyzer.AnalyzeDeclarations(snapshot, signatureDiagnostics, CancellationToken.None);
        SourceAnalysisResult fullResult = (SourceAnalysisResult)await analyzer.Analyze(snapshot, fullDiagnostics, CancellationToken.None);

        signatureDiagnostics.ReportDiagnostics()
            .Where(IsDetachedDocumentation)
            .Should()
            .ContainSingle();

        fullDiagnostics.ReportDiagnostics()
            .Where(IsDetachedDocumentation)
            .Should()
            .ContainSingle();

        signatureResult.DocumentationBlocks
            .Should()
            .ContainSingle();

        fullResult.DocumentationBlocks
            .Should()
            .ContainSingle();
    }

    private static Task<(SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics)> Analyze(string source)
        => AnalysisHelper.AnalyzeSource(SourceSnapshot.FromText(testUri, version: null, source));

    private static bool IsDetachedDocumentation(SushiDiagnostic diagnostic)
        => diagnostic.IsType(WarningType.DetachedDocumentationBlock);

    private static int ByteOffset(string source, int characterOffset)
        => Encoding.UTF8.GetByteCount(source.AsSpan(0, characterOffset));
}
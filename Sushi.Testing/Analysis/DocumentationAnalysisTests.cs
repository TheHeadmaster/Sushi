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
    public async Task DocumentationAttachmentShouldRemainOrderedAcrossManyInterleavedDeclarations()
    {
        StringBuilder source = new(Header);
        for (int index = 0; index < 300; index++)
        {
            source.Append("/// docs ").Append(index).Append('\n');
            source.Append("public int32 Function").Append(index).Append("() { return ").Append(index).AppendLine("; }");
            if (index % 3 == 0)
            {
                source.Append("/// activity docs ").Append(index).Append('\n');
                source.Append("public activity Activity").Append(index).AppendLine(" : task { }");
            }
        }

        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source.ToString());

        result.DocumentationBlocks
            .Should()
            .HaveCount(400)
            .And.OnlyContain(block => block.IsAttached);

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

    [Test]
    public async Task DirectivesShouldRetainMultilineMarkdownDescriptionsAndProse()
    {
        string source = Header + "/// Calculates a total for user@example.com.\n///\n/// **Details** are available.\n/// @param value The first line.\n///\n/// The `value` is included.\n/// @return The total.\n/// It is rounded.\npublic int32 Calculate(int32 value) { return value; }";
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);
        DocumentationBlock block = result.DocumentationBlocks.Should().ContainSingle().Subject;

        block.Content.Prose.Text
            .Should()
            .Be("Calculates a total for user@example.com.\n\n**Details** are available.");

        block.Content.Directives
            .Should()
            .HaveCount(2);

        block.Content.Directives[0].ParameterName
            .Should()
            .Be("value");

        block.Content.Directives[0].Description.Text
            .Should()
            .Be("The first line.\n\nThe `value` is included.");

        block.Content.Directives[1].Description.Text
            .Should()
            .Be("The total.\nIt is rounded.");

        DocumentationWarnings(diagnostics)
            .Should()
            .BeEmpty();

        block.Content.Prose.Snapshot
            .Should()
            .BeSameAs(result.Snapshot);
    }

    [Test]
    public async Task DirectivesInsideValidFencesAndEscapedLinesShouldRemainProse()
    {
        string source = Header + "/// Contact user@example.com.\n/// \\@param is literal.\n/// ```sushi\n/// @param hidden ignored\n/// ````\n/// ~~~~text\n/// @return hidden ignored\n/// ~~~\n/// ~~~~\n/// @returns not-an-alias\n/// @throws deferred\n/// @param value provided\n/// @return provided\npublic int32 Calculate(int32 value) { return value; }";
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);
        DocumentationBlock block = result.DocumentationBlocks.Should().ContainSingle().Subject;

        block.Content.Directives
            .Select(directive => directive.Kind)
            .Should()
            .Equal(DocumentationDirectiveKind.Parameter, DocumentationDirectiveKind.Return);

        block.Content.Prose.Text
            .Should()
            .Contain("@param is literal")
            .And.Contain("@param hidden ignored")
            .And.Contain("@return hidden ignored")
            .And.Contain("@returns not-an-alias")
            .And.Contain("@throws deferred");

        DocumentationWarnings(diagnostics)
            .Where(diagnostic => diagnostic.IsType(WarningType.UnrecognizedDocumentationDirective))
            .Should()
            .HaveCount(2);
    }

    [Test]
    public async Task UnknownDirectiveAfterRecognizedDirectiveShouldRemainOrdinaryDescriptionText()
    {
        string source = Header + "/// @param value The first line.\n/// @throws is unsupported text.\n/// Continuation remains part of the parameter description.\n/// @return The value.\npublic int32 Calculate(int32 value) { return value; }";
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);
        DocumentationBlock block = result.DocumentationBlocks.Should().ContainSingle().Subject;

        block.Content.Directives
            .Should()
            .HaveCount(2);

        block.Content.Directives[0].Description.Text
            .Should()
            .Be("The first line.\n@throws is unsupported text.\nContinuation remains part of the parameter description.");

        block.Content.Prose.Text
            .Should()
            .NotContain("@throws is unsupported text.");

        diagnostics
            .Where(diagnostic => diagnostic.DisplayName == "SUSW0004")
            .Should()
            .ContainSingle();
    }

    [Test]
    public async Task PublicUndocumentedCallableShouldProduceOnlyMissingDeclarationWarning()
    {
        string source = Header + "public int32 Calculate(int32 left, int32 right) { return left + right; }";
        (_, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);

        DocumentationWarnings(diagnostics)
            .Select(diagnostic => diagnostic.DisplayName)
            .Should()
            .Equal("SUSW0002");
    }

    [Test]
    public async Task DetachedBlockBeforePublicCallableShouldNotAttachAndShouldPreserveMissingDeclarationPrecedence()
    {
        string source = Header + "/// detached docs\n\npublic int32 Calculate(int32 value) { return value; }";
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);

        result.DocumentationBlocks.Should().ContainSingle(block => !block.IsAttached);
        DocumentationWarnings(diagnostics)
            .Select(diagnostic => diagnostic.DisplayName)
            .Should()
            .Equal("SUSW0003", "SUSW0002");
    }

    [Test]
    public async Task PublicCallableWithPartialDocumentationShouldWarnForEachMissingComponent()
    {
        string source = Header + "/// Adds values.\n/// @param left First operand.\npublic int32 Add(int32 left, int32 right) { return left + right; }";
        (_, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);

        DocumentationWarnings(diagnostics)
            .Select(diagnostic => diagnostic.DisplayName)
            .Should()
            .Equal("SUSW0008", "SUSW0010");
    }

    [Test]
    public async Task PublicActivityAndVisibleLifecycleMethodRequireTheirOwnDocumentation()
    {
        string source = Header + "public activity Application : task\n{\n    public entry void Start() { return; }\n}";
        (_, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);

        DocumentationWarnings(diagnostics)
            .Select(diagnostic => diagnostic.DisplayName)
            .Should()
            .Equal("SUSW0002", "SUSW0002");
    }

    [Test]
    public async Task CompletePublicDocumentationShouldHaveNoDocumentationWarnings()
    {
        string source = Header + "/// Adds values.\n/// @param left First operand.\n/// @param right Second operand.\n/// @return Sum.\npublic int32 Add(int32 left, int32 right) { return left + right; }";
        (_, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);

        DocumentationWarnings(diagnostics)
            .Should()
            .BeEmpty();
    }

    [Test]
    public async Task DirectiveValidationShouldUseExactNamesAndReportDuplicatesAndEmptyDescriptions()
    {
        string source = Header + "/// @param @return \n/// @param @return duplicate\n/// @param Return wrong case\n/// @return\n/// @return duplicate\npublic int32 Calculate(int32 @return) { return @return; }";
        (_, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);

        DocumentationWarnings(diagnostics)
            .GroupBy(diagnostic => diagnostic.DisplayName)
            .ToDictionary(group => group.Key, group => group.Count())
            .Should()
            .BeEquivalentTo(new Dictionary<string, int>
            {
                ["SUSW0005"] = 1,
                ["SUSW0007"] = 1,
                ["SUSW0006"] = 1,
                ["SUSW0011"] = 2
            });
    }

    [Test]
    public async Task NonPublicCallableShouldNotRequireParameterDocumentationButStillRequiresReturnDocumentation()
    {
        string source = Header + "/// Internal helper.\nprivate int32 Helper(int32 value) { return value; }";
        (_, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);

        DocumentationWarnings(diagnostics)
            .Select(diagnostic => diagnostic.DisplayName)
            .Should()
            .Equal("SUSW0010");
    }

    [Test]
    public async Task VoidReturnDirectiveAndMissingPublicParameterShouldUseDistinctWarnings()
    {
        string source = Header + "/// Stops.\n/// @return Nothing.\npublic void Stop(int32 reason) { return; }";
        (_, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);

        DocumentationWarnings(diagnostics)
            .Select(diagnostic => diagnostic.DisplayName)
            .Should()
            .Equal("SUSW0009", "SUSW0008");
    }

    [Test]
    public async Task PublicMethodInsideNonPublicActivityShouldNotBeTreatedAsPublicApi()
    {
        string source = Header + "private activity Worker : task\n{\n    public entry int32 Start(int32 value) { return value; }\n}";
        (_, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);

        DocumentationWarnings(diagnostics)
            .Should()
            .BeEmpty();
    }

    [TestCase("\n")]
    [TestCase("\r\n")]
    [TestCase("\r")]
    public async Task DirectiveAndParameterSpansShouldRemainUtf8AccurateAcrossLineEndings(string lineEnding)
    {
        string source = Header + "/// café 😀" + lineEnding + "/// @param value description" + lineEnding + "/// @return résultat" + lineEnding + "public int32 Calculate(int32 value) { return value; }";
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);
        DocumentationBlock block = result.DocumentationBlocks.Should().ContainSingle().Subject;
        DocumentationDirective parameter = block.Content.Directives[0];
        int directiveOffset = ByteOffset(source, source.IndexOf("@param", StringComparison.Ordinal));
        int parameterOffset = ByteOffset(source, source.IndexOf("value description", StringComparison.Ordinal));

        parameter.NameSpan
            .Should()
            .Be(new SourceSpan(result.Snapshot, directiveOffset, directiveOffset + Encoding.UTF8.GetByteCount("@param")));

        parameter.ParameterNameSpan
            .Should()
            .Be(new SourceSpan(result.Snapshot, parameterOffset, parameterOffset + Encoding.UTF8.GetByteCount("value")));

        parameter.Description.Snapshot
            .Should()
            .BeSameAs(result.Snapshot);

        DocumentationWarnings(diagnostics)
            .Should()
            .BeEmpty();
    }

    [Test]
    public async Task SignatureOnlyAndFullAnalysisShouldProduceTheSameDocumentationWarnings()
    {
        SourceSnapshot snapshot = SourceSnapshot.FromText(testUri, 1, Header + "public int32 Calculate(int32 value) { return value; }");
        SourceAnalyzer analyzer = new();
        DiagnosticReporter signatureDiagnostics = new();
        DiagnosticReporter fullDiagnostics = new();

        await analyzer.AnalyzeDeclarations(snapshot, signatureDiagnostics, CancellationToken.None);
        await analyzer.Analyze(snapshot, fullDiagnostics, CancellationToken.None);

        signatureDiagnostics.ReportDiagnostics()
            .Where(IsDocumentationWarning)
            .Select(diagnostic => diagnostic.DisplayName)
            .Should()
            .Equal("SUSW0002");

        fullDiagnostics.ReportDiagnostics()
            .Where(IsDocumentationWarning)
            .Select(diagnostic => diagnostic.DisplayName)
            .Should()
            .Equal("SUSW0002");
    }


    [Test]
    public async Task InlineReferencesShouldPreserveUtf8SpansAndIgnoreMarkdownCodeBoundaries()
    {
        string source = Header
            + "/// résumé {@link Calculate(int32) | calculation} and {@param value}\n"
            + "/// `{@link InlineCode}` \\{@link Escaped} {@link incomplete\n"
            + "/// ```sushi\n/// {@link FenceExample}\n/// ```\n"
            + "public int32 Calculate(int32 value) { return value; }";
        (SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics) = await Analyze(source);
        DocumentationBlock block = result.DocumentationBlocks.Should().ContainSingle().Subject;
        DocumentationReference link = block.Content.InlineReferences[0];
        DocumentationReference parameter = block.Content.InlineReferences[1];
        int targetStart = ByteOffset(source, source.IndexOf("Calculate(int32)", StringComparison.Ordinal));
        int constructStart = ByteOffset(source, source.IndexOf("{@link Calculate", StringComparison.Ordinal));

        block.Content.InlineReferences
            .Should()
            .HaveCount(2);

        link.Kind.Should().Be(DocumentationReferenceKind.Link);
        link.WrittenTarget.Should().Be("Calculate(int32)");
        link.DisplayText.Should().Be("calculation");
        link.Span.Should().Be(new SourceSpan(result.Snapshot, constructStart,
            constructStart + Encoding.UTF8.GetByteCount("{@link Calculate(int32) | calculation}")));
        link.TargetNameSpan.Should().Be(new SourceSpan(result.Snapshot, targetStart, targetStart + Encoding.UTF8.GetByteCount("Calculate")));
        parameter.Kind.Should().Be(DocumentationReferenceKind.Parameter);
        parameter.TargetNameSpelling.Should().Be("value");

        DocumentationWarnings(diagnostics).Should().BeEmpty();
    }

    private static IEnumerable<SushiDiagnostic> DocumentationWarnings(IEnumerable<SushiDiagnostic> diagnostics)
        => diagnostics.Where(IsDocumentationWarning);

    private static bool IsDocumentationWarning(SushiDiagnostic diagnostic)
        => diagnostic.DisplayName is "SUSW0002" or "SUSW0003" or "SUSW0004" or "SUSW0005" or "SUSW0006" or "SUSW0007" or "SUSW0008" or "SUSW0009" or "SUSW0010" or "SUSW0011" or "SUSW0012" or "SUSW0013" or "SUSW0014" or "SUSW0015";

    private static Task<(SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics)> Analyze(string source)
        => AnalysisHelper.AnalyzeSource(SourceSnapshot.FromText(testUri, version: null, source));

    private static Task<(SourceAnalysisResult result, IReadOnlyList<SushiDiagnostic> diagnostics)> Analyze(SourceSnapshot snapshot)
        => AnalysisHelper.AnalyzeSource(snapshot);

    private static bool IsDetachedDocumentation(SushiDiagnostic diagnostic)
        => diagnostic.IsType(WarningType.DetachedDocumentationBlock);

    private static int ByteOffset(string source, int characterOffset)
        => Encoding.UTF8.GetByteCount(source.AsSpan(0, characterOffset));
}
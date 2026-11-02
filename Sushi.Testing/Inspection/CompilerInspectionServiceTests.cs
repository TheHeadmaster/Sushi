using System.Text;
using FluentAssertions;
using NUnit.Framework;
using Sushi.Analysis;
using Sushi.Diagnostics;
using Sushi.Inspection;
using Sushi.Source;

namespace Sushi.Testing.Inspection;

/// <summary>
/// Exercises real project discovery, authoritative snapshots, and selected-body inspection without native generation.
/// </summary>
[TestFixture]
public class CompilerInspectionServiceTests
{
    private const string Header = "package Example; namespace Example;\n";
    private const string Simple = Header + "public int32 Calculate(int32 value) { int32 total = value + 1; return total; }";

    [TestCase(TestName = "Source Inspection Produces Distinct Stages With Shared Identity And No Native Output Or Root")]
    public async Task InspectionShouldProduceBothStagesWithoutExecutableCompilation()
    {
        using TestProject project = await TestProject.Create(Simple);
        SourceSnapshot snapshot = project.Snapshot(Simple, 7);
        string[] files = Directory.GetFiles(project.DirectoryPath, "*", SearchOption.AllDirectories);
        CompilerInspectionService service = new();
        CompilerInspectionResult structured = await service.InspectAsync(project.Request(snapshot), CancellationToken.None);
        CompilerInspectionResult lowered = await service.InspectAsync(project.Request(snapshot, InspectionStage.LoweredIR), CancellationToken.None);

        structured.Succeeded
            .Should()
            .BeTrue();
        lowered.Succeeded
            .Should()
            .BeTrue();
        structured.Diagnostics
            .Should()
            .BeEmpty();
        lowered.Diagnostics
            .Should()
            .BeEmpty();
        structured.Stage
            .Should()
            .Be(InspectionStage.StructuredIR);
        lowered.Stage
            .Should()
            .Be(InspectionStage.LoweredIR);
        structured.Document!.Stage
            .Should()
            .Be(structured.Stage);
        lowered.Document!.Stage
            .Should()
            .Be(lowered.Stage);
        structured.FunctionSymbol
            .Should()
            .Be("__sushi_package_function_0")
            .And
            .Be(lowered.FunctionSymbol);
        structured.Document.Text
            .Should()
            .Contain("local int32 %local0 = (value + 1);");
        lowered.Document.Text
            .Should()
            .Contain("checked +")
            .And
            .Contain("load %local0")
            .And
            .NotBe(structured.Document.Text);
        foreach (CompilerInspectionResult result in new[] { structured, lowered })
        {
            result.Context.ProjectPath
                .Should()
                .Be(project.ProjectPath);
            result.Context.ProjectSnapshot.Uri
                .Should()
                .Be(new Uri(project.ProjectPath));
            result.Context.Sources
                .Should()
                .ContainSingle()
                .Which
                .Should()
                .BeSameAs(snapshot);
            result.Declaration!.Value.Snapshot
                .Should()
                .BeSameAs(snapshot);
            result.Context.Functions[result.FunctionSymbol!]
                .Should()
                .Be(result.Declaration.Value);
            result.Document!.Occurrences
                .Should()
                .Contain(item => item.IsDefinition && item.Identity == new InspectionSymbolIdentity(InspectionSymbolKind.Function, result.FunctionSymbol!));
            result.Document.Occurrences
                .Should()
                .Contain(item => !item.IsDefinition && item.Identity == new InspectionSymbolIdentity(InspectionSymbolKind.Parameter, result.FunctionSymbol!, 0));
        }

        lowered.Document.Occurrences
            .Should()
            .Contain(item => item.Identity.Kind == InspectionSymbolKind.BasicBlock && item.IsDefinition)
            .And
            .Contain(item => item.Identity.Kind == InspectionSymbolKind.Temporary && !item.IsDefinition);
        Directory.GetFiles(project.DirectoryPath, "*", SearchOption.AllDirectories)
            .Should()
            .BeEquivalentTo(files);
    }

    [TestCase(InspectionStage.StructuredIR, TestName = "Structured Inspection Does Not Bind Invalid Unrelated Or Referenced Bodies")]
    [TestCase(InspectionStage.LoweredIR, TestName = "Lowered Inspection Does Not Bind Invalid Unrelated Or Referenced Bodies")]
    public async Task InspectionShouldBindOnlySelectedBody(InspectionStage stage)
    {
        const string selectedText = Header + "public int32 Good() { return Helper(5); } public int32 Broken() { return absent; }";
        const string dependencyText = Header + "public int32 Helper(int32 value) { return missing; }";
        using TestProject project = await TestProject.Create(selectedText, dependencyText);
        SourceSnapshot selected = project.Snapshot(selectedText);
        SourceSnapshot dependency = project.Snapshot(dependencyText, 3, "B.sus");
        IDiagnosticReporter fullDiagnostics = new DiagnosticReporter();
        await new SourceAnalyzer().Analyze(dependency, fullDiagnostics, CancellationToken.None);
        fullDiagnostics.HasErrors()
            .Should()
            .BeTrue();
        CompilerInspectionService service = new();
        CompilerInspectionResult result = await service.InspectAsync(project.Request(selected, stage, additional: [dependency]), CancellationToken.None);
        result.Succeeded
            .Should()
            .BeTrue();
        result.Diagnostics
            .Should()
            .BeEmpty();
        result.Context.Functions
            .Should()
            .HaveCount(3);
        result.Context.Functions["__sushi_package_function_2"].Snapshot
            .Should()
            .BeSameAs(dependency);
        result.Document!.Occurrences
            .Should()
            .Contain(item => !item.IsDefinition && item.Identity == new InspectionSymbolIdentity(InspectionSymbolKind.Function, "__sushi_package_function_2"));
        CompilerInspectionResult broken = await service.InspectAsync(project.Request(selected, stage, "return absent", [dependency]), CancellationToken.None);
        broken.Succeeded
            .Should()
            .BeFalse();
        broken.Diagnostics
            .Should()
            .Contain(item => item.IsType(ErrorType.UnknownIdentifier));
        CompilerInspectionResult referenced = await service.InspectAsync(project.Request(dependency, stage, additional: [selected]), CancellationToken.None);
        referenced.Succeeded
            .Should()
            .BeFalse();
    }

    [TestCase(InspectionStage.StructuredIR, TestName = "Structured Cross File References Match Callee Inspection Definitions")]
    [TestCase(InspectionStage.LoweredIR, TestName = "Lowered Cross File References Match Callee Inspection Definitions")]
    public async Task CallReferencesShouldMatchInspectedDefinitions(InspectionStage stage)
    {
        const string caller = Header + "public int32 Caller() { return Helper(5); }";
        const string callee = Header + "public int32 Helper(int32 value) { return value; }";
        using TestProject project = await TestProject.Create(caller, callee);
        SourceSnapshot callerSnapshot = project.Snapshot(caller, 10);
        SourceSnapshot calleeSnapshot = project.Snapshot(callee, 20, "B.sus");
        CompilerInspectionService service = new();
        CompilerInspectionResult first = await service.InspectAsync(project.Request(callerSnapshot, stage, additional: [calleeSnapshot]), CancellationToken.None);
        CompilerInspectionResult second = await service.InspectAsync(project.Request(calleeSnapshot, stage, additional: [callerSnapshot]), CancellationToken.None);
        second.Succeeded
            .Should()
            .BeTrue();
        InspectionSymbolIdentity identity = new(InspectionSymbolKind.Function, second.FunctionSymbol!);
        first.Document!.Occurrences
            .Should()
            .Contain(item => !item.IsDefinition && item.Identity == identity);
        second.Document!.Occurrences
            .Should()
            .Contain(item => item.IsDefinition && item.Identity == identity);
        first.Context.Sources.Select(item => item.Uri)
            .Should()
            .Equal(new Uri(project.SourcePath("A.sus")), new Uri(project.SourcePath("B.sus")));
    }

    [TestCase(TestName = "Semantic Resolver Uses Exact Identity And Half Open UTF16 Ranges")]
    public async Task DefinitionResolverShouldMatchRecordedOccurrencesOnly()
    {
        using TestProject project = await TestProject.Create(Simple);
        SourceSnapshot snapshot = project.Snapshot(Simple);
        CompilerInspectionResult result = await new CompilerInspectionService().InspectAsync(project.Request(snapshot), CancellationToken.None);
        InspectionDocument document = result.Document!;
        InspectionSymbolOccurrence reference = document.Occurrences.First(item =>
            !item.IsDefinition && item.Identity == new InspectionSymbolIdentity(InspectionSymbolKind.Parameter, result.FunctionSymbol!, 0));

        (InspectionSymbolOccurrence Origin, InspectionSymbolOccurrence Definition)? resolved =
            InspectionDefinitionResolver.Resolve(document, reference.Start);
        resolved
            .Should()
            .NotBeNull();
        resolved!.Value.Origin.Should().Be(reference);
        resolved.Value.Definition.Should().Be(document.Occurrences.Single(item => item.IsDefinition && item.Identity == reference.Identity));
        InspectionDefinitionResolver.Resolve(document, reference.End)
            .Should()
            .BeNull();
        InspectionDefinitionResolver.Resolve(document, -1)
            .Should()
            .BeNull();
        InspectionDefinitionResolver.Resolve(document, document.Text.Length)
            .Should()
            .BeNull();
    }

    [TestCase("a😀b\r\n\r\nc", 0, 3, 3, TestName = "UTF16 Coordinates Count Supplementary Characters As Two Code Units")]
    [TestCase("a\r\nb", 1, 0, 3, TestName = "UTF16 Coordinates Handle CRLF And Empty Lines")]
    public void GeneratedTextCoordinatesShouldRoundTrip(string text, int line, int character, int expectedOffset)
    {
        InspectionTextCoordinates.GetOffset(text, line, character).Should().Be(expectedOffset);
        InspectionTextCoordinates.GetPosition(text, expectedOffset).Should().Be((line, character));
    }

    [TestCase(TestName = "Same Function Names In Distinct Declaration Contexts Keep Separate Symbols")]
    public async Task IdenticalNamesShouldRemainAnchoredToTheirSourceDeclarations()
    {
        const string firstText = Header + "public int32 Same() { return 1; }";
        const string secondText = "package Other; namespace Other; public int32 Same() { return 2; }";
        using TestProject project = await TestProject.Create(firstText, secondText);
        SourceSnapshot firstSnapshot = project.Snapshot(firstText);
        SourceSnapshot secondSnapshot = project.Snapshot(secondText, fileName: "B.sus");
        CompilerInspectionService service = new();
        CompilerInspectionResult first = await service.InspectAsync(project.Request(firstSnapshot, additional: [secondSnapshot]), CancellationToken.None);
        CompilerInspectionResult second = await service.InspectAsync(project.Request(secondSnapshot, additional: [firstSnapshot]), CancellationToken.None);
        first.FunctionSymbol
            .Should()
            .Be("__sushi_package_function_0");
        second.FunctionSymbol
            .Should()
            .Be("__sushi_package_function_1");
        first.Declaration!.Value.Snapshot
            .Should()
            .BeSameAs(firstSnapshot);
        second.Declaration!.Value.Snapshot
            .Should()
            .BeSameAs(secondSnapshot);
        first.Document!.Text
            .Should()
            .Contain("return 1;");
        second.Document!.Text
            .Should()
            .Contain("return 2;");
    }

    [TestCase(TestName = "Unsaved Snapshot Content Overrides Disk And Preserves Prior Results Even With Equal Versions")]
    public async Task SnapshotContentShouldBeAuthoritativeAndResultsStable()
    {
        const string disk = Header + "public int32 Value() { return absent; }";
        const string firstText = Header + "public int32 Value() { return 11; }";
        const string secondText = Header + "public int32 Value() { return 22; }";
        using TestProject project = await TestProject.Create(disk);
        CompilerInspectionService service = new();
        SourceSnapshot firstSnapshot = project.Snapshot(firstText, 4);
        SourceSnapshot secondSnapshot = project.Snapshot(secondText, 4);
        CompilerInspectionResult first = await service.InspectAsync(project.Request(firstSnapshot), CancellationToken.None);
        CompilerInspectionResult second = await service.InspectAsync(project.Request(secondSnapshot), CancellationToken.None);
        first.Document!.Text
            .Should()
            .Contain("return 11;");
        second.Document!.Text
            .Should()
            .Contain("return 22;");
        first.Declaration!.Value.Snapshot
            .Should()
            .BeSameAs(firstSnapshot);
        first.Context.Sources[0].Text
            .Should()
            .Be(firstText);
        second.Context.Sources[0].Version
            .Should()
            .Be(4);
        (await File.ReadAllTextAsync(project.SourcePath("A.sus")))
            .Should()
            .Be(disk);
        ((IList<SourceSnapshot>)first.Context.Sources).IsReadOnly
            .Should()
            .BeTrue();
        ((IDictionary<string, SourceSpan>)first.Context.Functions).IsReadOnly
            .Should()
            .BeTrue();
    }

    [TestCase(TestName = "Dependency Snapshot Overrides Signature Without Requiring Its Body To Bind")]
    public async Task DependencySnapshotShouldControlSignatureResolution()
    {
        const string caller = Header + "public int32 Caller() { return Helper(5); }";
        const string diskDependency = Header + "public int32 Helper(bool value) { return 0; }";
        const string unsavedDependency = Header + "public int32 Helper(int32 value) { return absent; }";
        using TestProject project = await TestProject.Create(caller, diskDependency);
        SourceSnapshot selected = project.Snapshot(caller);
        SourceSnapshot dependency = project.Snapshot(unsavedDependency, 9, "B.sus");
        CompilerInspectionService service = new();
        CompilerInspectionResult disk = await service.InspectAsync(project.Request(selected), CancellationToken.None);
        disk.Succeeded
            .Should()
            .BeFalse();
        CompilerInspectionResult supplied = await service.InspectAsync(project.Request(selected, additional: [dependency]), CancellationToken.None);
        supplied.Succeeded
            .Should()
            .BeTrue();
        supplied.Context.Sources[1]
            .Should()
            .BeSameAs(dependency);
        supplied.Context.Sources[1].Version
            .Should()
            .Be(9);
        disk.Context.Sources[1].Text
            .Should()
            .Be(diskDependency);
        (await File.ReadAllTextAsync(project.SourcePath("B.sus")))
            .Should()
            .Be(diskDependency);
    }

    [TestCase(TestName = "UTF8 Selection Anchors Distinct Functions Beyond Multibyte Source Text")]
    public async Task FunctionSelectionShouldUseByteOffsetsAndDeclarationSpans()
    {
        string text = Header + "/*" + new string('界', 200) + "*/\npublic int32 First() { return 1; }\npublic int32 Second() { return 2; }\n";
        using TestProject project = await TestProject.Create(text);
        SourceSnapshot snapshot = project.Snapshot(text);
        CompilerInspectionService service = new();
        CompilerInspectionResult first = await service.InspectAsync(project.Request(snapshot, marker: "First"), CancellationToken.None);
        CompilerInspectionResult second = await service.InspectAsync(project.Request(snapshot, marker: "return 2"), CancellationToken.None);
        first.FunctionSymbol
            .Should()
            .Be("__sushi_package_function_0");
        second.FunctionSymbol
            .Should()
            .Be("__sushi_package_function_1");
        second.Declaration!.Value.Start
            .Should()
            .BeGreaterThan(text.IndexOf("Second", StringComparison.Ordinal));
        Encoding.UTF8.GetString(snapshot.Bytes.Span[second.Declaration.Value.Start..second.Declaration.Value.End])
            .Should()
            .Be("public int32 Second() { return 2; }");
        CompilerInspectionRequest byFolder = new(project.DirectoryPath, snapshot, TestProject.Offset(snapshot, "Second"), InspectionStage.StructuredIR);
        (await service.InspectAsync(byFolder, CancellationToken.None)).FunctionSymbol
            .Should()
            .Be(second.FunctionSymbol);
    }

    [TestCase("public", "Example", "Example", true, TestName = "Public Function Is Visible Within Same Package And Namespace")]
    [TestCase("internal", "Example", "Example", true, TestName = "Internal Function Retains Existing Project Visibility")]
    [TestCase("package", "Example", "Example", true, TestName = "Package Function Retains Existing Project Visibility")]
    [TestCase("private", "Example", "Example", false, TestName = "Private Function Does Not Become Visible To Callers")]
    [TestCase("protected", "Example", "Example", false, TestName = "Protected Package Function Does Not Become Visible To Callers")]
    [TestCase("public", "Other", "Example", false, TestName = "Other Package Function Does Not Become Visible")]
    [TestCase("public", "Example", "Other", false, TestName = "Other Namespace Function Does Not Become Visible")]
    public async Task InspectionShouldPreserveCallableVisibility(string access, string package, string ns, bool expected)
    {
        const string caller = Header + "public int32 Caller() { return Helper(); }";
        string helper = $"package {package}; namespace {ns}; {access} int32 Helper() {{ return 5; }}";
        using TestProject project = await TestProject.Create(caller, helper);
        CompilerInspectionService service = new();
        CompilerInspectionResult result = await service.InspectAsync(project.Request(project.Snapshot(caller)), CancellationToken.None);
        result.Succeeded
            .Should()
            .Be(expected);
        if (!expected)
        {
            result.Document
                .Should()
                .BeNull();
            result.Diagnostics
                .Should()
                .NotBeEmpty();
        }

        CompilerInspectionResult own = await service.InspectAsync(project.Request(project.Snapshot(helper, fileName: "B.sus")), CancellationToken.None);
        own.Succeeded
            .Should()
            .BeTrue();
        own.Document!.Text
            .Should()
            .StartWith(access + " function int32");
    }

    [TestCase("return absent;", TestName = "Selected Body Errors Return Diagnostics Without Partial IR")]
    [TestCase("return true;", TestName = "Selected Body Type Errors Return Diagnostics Without Partial IR")]
    public async Task SelectedBodyErrorsShouldReturnDiagnostics(string body)
    {
        string text = Header + $"public int32 Value() {{ {body} }}";
        using TestProject project = await TestProject.Create(text);
        SourceSnapshot snapshot = project.Snapshot(text, 5);
        CompilerInspectionResult result = await new CompilerInspectionService().InspectAsync(project.Request(snapshot), CancellationToken.None);
        result.Succeeded
            .Should()
            .BeFalse();
        result.Document
            .Should()
            .BeNull();
        result.Diagnostics
            .Should()
            .NotBeEmpty()
            .And
            .OnlyContain(item => ReferenceEquals(item.Span.Snapshot, snapshot));
        result.Declaration!.Value.Snapshot.Version
            .Should()
            .Be(5);
    }

    [TestCase("namespace Example; public int32 Value() { return 1; }", TestName = "Missing Package Is A Prerequisite Failure")]
    [TestCase("package Example; public int32 Value() { return 1; }", TestName = "Missing Namespace Is A Prerequisite Failure")]
    [TestCase(Header + "public int32 Value(int32 x, int32 x) { return 1; }", TestName = "Invalid Signature Is A Prerequisite Failure")]
    [TestCase(Header + "public unknown Value() { return 1; }", TestName = "Unsupported Signature Returns Diagnostics Without Partial IR")]
    [TestCase(Header + "public int32 Value() { return 1 }", TestName = "Malformed Source Syntax Returns Diagnostics Without Partial IR")]
    public async Task DeclarationErrorsShouldReturnDiagnostics(string text)
    {
        using TestProject project = await TestProject.Create(text);
        SourceSnapshot snapshot = project.Snapshot(text);
        CompilerInspectionResult result = await new CompilerInspectionService().InspectAsync(project.Request(snapshot, marker: "Value"), CancellationToken.None);
        result.Succeeded
            .Should()
            .BeFalse();
        result.Document
            .Should()
            .BeNull();
        result.Diagnostics
            .Should()
            .NotBeEmpty()
            .And
            .OnlyContain(item => ReferenceEquals(item.Span.Snapshot, snapshot));
    }

    [TestCase(TestName = "Invalid Dependency Declaration Prevents Inspection And Preserves Diagnostic Snapshot")]
    public async Task DependencyDeclarationErrorsShouldRemainPrerequisites()
    {
        using TestProject project = await TestProject.Create(Simple, "namespace Example; public int32 Helper() { return 1; }");
        SourceSnapshot snapshot = project.Snapshot(Simple);
        CompilerInspectionResult result = await new CompilerInspectionService().InspectAsync(project.Request(snapshot), CancellationToken.None);
        result.Succeeded
            .Should()
            .BeFalse();
        result.Diagnostics
            .Should()
            .Contain(item => item.IsType(ErrorType.MissingPackageDeclaration) && item.Span.Snapshot.Uri == new Uri(project.SourcePath("B.sus")));
    }

    [TestCase(TestName = "Invalid Project Configuration Returns Project Snapshot Diagnostics")]
    public async Task InvalidProjectShouldReturnDiagnostics()
    {
        using TestProject project = await TestProject.Create(Simple);
        await File.WriteAllTextAsync(project.ProjectPath, "name = 42");
        CompilerInspectionResult result = await new CompilerInspectionService().InspectAsync(project.Request(project.Snapshot(Simple)), CancellationToken.None);
        result.Document
            .Should()
            .BeNull();
        result.Diagnostics
            .Should()
            .Contain(item => item.IsType(ErrorType.TomlInvalidValueType))
            .And
            .OnlyContain(item => ReferenceEquals(item.Span.Snapshot, result.Context.ProjectSnapshot));
        result.Context.ProjectSnapshot.Text
            .Should()
            .Be("name = 42");
    }

    [TestCase(0, TestName = "Package Declaration Position Does Not Select A Nearby Function")]
    [TestCase(1, TestName = "Whitespace After A Function Does Not Select A Nearby Function")]
    [TestCase(2, TestName = "EOF Does Not Select A Nearby Function")]
    [TestCase(3, TestName = "Activity Lifecycle Is Not Treated As A Package Function")]
    public async Task UnsupportedSelectionsShouldBeRejected(int selection)
    {
        const string text = Simple + "\n\npublic activity Worker : task { public entry void Run() { return; } }\n";
        using TestProject project = await TestProject.Create(text);
        SourceSnapshot snapshot = project.Snapshot(text);
        int offset = selection switch
        {
            0 => 0,
            1 => TestProject.Offset(snapshot, "\n\n"),
            2 => snapshot.SourceLength,
            _ => TestProject.Offset(snapshot, "Run")
        };
        CompilerInspectionRequest request = new(project.ProjectPath, snapshot, offset, InspectionStage.StructuredIR);
        Func<Task> inspect = () => new CompilerInspectionService().InspectAsync(request, CancellationToken.None);
        await inspect
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("*package-level function*");
    }

    [TestCase(false, TestName = "Excluded Selected Source Is Rejected")]
    [TestCase(true, TestName = "Excluded Dependency Snapshot Is Rejected")]
    public async Task ExcludedSourcesShouldBeRejected(bool dependency)
    {
        using TestProject project = await TestProject.Create(Simple, Header + "public int32 Helper() { return 1; }");
        string configuration = TestProject.Configuration.Replace("[build]", "[source]\nexclude = [\"Sources/B.sus\"]\n[build]", StringComparison.Ordinal);
        await File.WriteAllTextAsync(project.ProjectPath, configuration);
        SourceSnapshot included = project.Snapshot(Simple);
        SourceSnapshot excluded = project.Snapshot(Header + "public int32 Helper() { return 1; }", fileName: "B.sus");
        CompilerInspectionRequest request = dependency
            ? project.Request(included, additional: [excluded])
            : project.Request(excluded);
        Func<Task> inspect = () => new CompilerInspectionService().InspectAsync(request, CancellationToken.None);
        await inspect
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("*not a discovered project source*");
    }

    [TestCase(false, TestName = "Duplicate Identical Source Snapshot Is Rejected")]
    [TestCase(true, TestName = "Conflicting Source Snapshots For One File Are Rejected")]
    public async Task DuplicateSnapshotsShouldBeRejected(bool conflict)
    {
        using TestProject project = await TestProject.Create(Simple);
        SourceSnapshot snapshot = project.Snapshot(Simple);
        SourceSnapshot duplicate = conflict ? project.Snapshot(Header + "public int32 Other() { return 1; }", 2) : snapshot;
        Func<Task> inspect = () => new CompilerInspectionService().InspectAsync(project.Request(snapshot, additional: [duplicate]), CancellationToken.None);
        await inspect
            .Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("*Duplicate inspection snapshot*");
    }

    [TestCase(TestName = "Absent Source And Non File Snapshot URIs Are Rejected")]
    public async Task InvalidSourceIdentitiesShouldBeRejected()
    {
        using TestProject project = await TestProject.Create(Simple);
        SourceSnapshot absent = project.Snapshot(Simple, fileName: "Absent.sus");
        Func<Task> missing = () => new CompilerInspectionService().InspectAsync(project.Request(absent), CancellationToken.None);
        await missing
            .Should()
            .ThrowAsync<ArgumentException>();
        SourceSnapshot remote = SourceSnapshot.FromText(new Uri("https://example.test/Source.sus"), 1, Simple);
        Func<Task> invalid = () => new CompilerInspectionService().InspectAsync(project.Request(remote), CancellationToken.None);
        await invalid
            .Should()
            .ThrowAsync<ArgumentException>();
    }

    [TestCase(TestName = "Malformed Request Arguments And Unsupported Stages Fail Explicitly")]
    public void MalformedRequestsShouldBeRejected()
    {
        SourceSnapshot snapshot = SourceSnapshot.FromText(new Uri(Path.Combine(Path.GetTempPath(), "unused.sus")), 1, Simple);
        Action negative = () => _ = new CompilerInspectionRequest("unused.susproj", snapshot, -1, InspectionStage.StructuredIR);
        Action tooLarge = () => _ = new CompilerInspectionRequest("unused.susproj", snapshot, snapshot.SourceLength + 1, InspectionStage.StructuredIR);
        Action stage = () => _ = new CompilerInspectionRequest("unused.susproj", snapshot, 0, (InspectionStage)99);
        Action missing = () => _ = new CompilerInspectionRequest("unused.susproj", null!, 0, InspectionStage.StructuredIR);
        negative
            .Should()
            .Throw<ArgumentOutOfRangeException>();
        tooLarge
            .Should()
            .Throw<ArgumentOutOfRangeException>();
        stage
            .Should()
            .Throw<ArgumentOutOfRangeException>();
        missing
            .Should()
            .Throw<ArgumentNullException>();
    }

    [TestCase(TestName = "Inspection Cancellation Stops Before Project IO And Returns No Partial Result")]
    public async Task CancellationShouldPropagate()
    {
        SourceSnapshot snapshot = SourceSnapshot.FromText(new Uri(Path.Combine(Path.GetTempPath(), "unused.sus")), 1, Simple);
        CompilerInspectionRequest request = new("nonexistent.susproj", snapshot, 0, InspectionStage.LoweredIR);
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();
        Func<Task> inspect = () => new CompilerInspectionService().InspectAsync(request, cancellation.Token);
        await inspect
            .Should()
            .ThrowAsync<OperationCanceledException>();
        Func<Task> missing = () => new CompilerInspectionService().InspectAsync(null!, CancellationToken.None);
        await missing
            .Should()
            .ThrowAsync<ArgumentNullException>();
    }

    private sealed class TestProject(string directory) : IDisposable
    {
        public const string Configuration = """
            name = "Inspection"
            assembly = "Inspection"
            language-version = "1.0"
            [build]
            default = "debug"
            sources = ["Sources/**/*.sus"]
            [build.targets.debug]
            type = "Sushi.MVP.Debug"
            """;

        public string DirectoryPath { get; } = directory;
        public string ProjectPath => Path.Combine(this.DirectoryPath, "Inspection.susproj");
        public string SourcePath(string name) => Path.Combine(this.DirectoryPath, "Sources", name);

        public static async Task<TestProject> Create(string main, string? dependency = null)
        {
            string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", Guid.NewGuid().ToString("N"));
            TestProject project = new(directory);
            Directory.CreateDirectory(Path.Combine(directory, "Sources"));
            try
            {
                await File.WriteAllTextAsync(project.ProjectPath, Configuration);
                await File.WriteAllTextAsync(project.SourcePath("A.sus"), main);
                if (dependency is not null)
                {
                    await File.WriteAllTextAsync(project.SourcePath("B.sus"), dependency);
                }

                return project;
            }
            catch
            {
                project.Dispose();
                throw;
            }
        }

        public SourceSnapshot Snapshot(string text, int? version = 1, string fileName = "A.sus") => SourceSnapshot.FromText(new Uri(this.SourcePath(fileName)), version, text);

        public CompilerInspectionRequest Request(SourceSnapshot snapshot, InspectionStage stage = InspectionStage.StructuredIR,
            string marker = "return", IReadOnlyList<SourceSnapshot>? additional = null) => new(this.ProjectPath, snapshot, Offset(snapshot, marker), stage, additional);

        public static int Offset(SourceSnapshot snapshot, string marker)
        {
            int character = snapshot.Text.IndexOf(marker, StringComparison.Ordinal);
            if (character < 0)
            {
                throw new ArgumentException("The fixture selection marker must occur in the supplied source.", nameof(marker));
            }

            return Encoding.UTF8.GetByteCount(snapshot.Text.AsSpan(0, character));
        }

        public void Dispose() => Directory.Delete(this.DirectoryPath, recursive: true);
    }
}
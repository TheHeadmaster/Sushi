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
        using CompilerInspectionService service = new();
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

    [TestCase(TestName = "Inspection Reuses Structured IR Across Stages And Retains Fresh Snapshot Ownership")]
    public async Task InspectionShouldReuseRepresentationsAcrossCompatibleRequests()
    {
        using TestProject project = await TestProject.Create(Simple);
        using CompilerInspectionService service = new();
        SourceSnapshot firstSnapshot = project.Snapshot(Simple, 7);
        CompilerInspectionResult structured = await service.InspectAsync(project.Request(firstSnapshot, marker: "Calculate"), CancellationToken.None);
        CompilerInspectionCacheKey cacheKey = CacheKey(structured);
        service.Cache.TryGet(cacheKey, out CompilerInspectionCacheEntry structuredEntry)
            .Should()
            .BeTrue();
        structuredEntry.StructuredIR
            .Should()
            .NotBeNull();
        structuredEntry.StructuredDocument
            .Should()
            .BeSameAs(structured.Document);

        CompilerInspectionResult lowered = await service.InspectAsync(
            project.Request(firstSnapshot, InspectionStage.LoweredIR, "Calculate"), CancellationToken.None);
        service.Cache.TryGet(cacheKey, out CompilerInspectionCacheEntry loweredEntry)
            .Should()
            .BeTrue();
        loweredEntry.StructuredIR
            .Should()
            .BeSameAs(structuredEntry.StructuredIR);
        loweredEntry.LoweredIR
            .Should()
            .NotBeNull();
        loweredEntry.LoweredDocument
            .Should()
            .BeSameAs(lowered.Document);
        lowered.Stage
            .Should()
            .Be(InspectionStage.LoweredIR);

        SourceSnapshot equivalentSnapshot = project.Snapshot(Simple, 8);
        CompilerInspectionResult repeatedStructured = await service.InspectAsync(
            project.Request(equivalentSnapshot, marker: "Calculate"), CancellationToken.None);
        CompilerInspectionResult repeatedLowered = await service.InspectAsync(
            project.Request(equivalentSnapshot, InspectionStage.LoweredIR, "Calculate"), CancellationToken.None);
        repeatedStructured.Document
            .Should()
            .BeSameAs(structured.Document);
        repeatedLowered.Document
            .Should()
            .BeSameAs(lowered.Document);
        repeatedStructured.Context.Sources[0]
            .Should()
            .BeSameAs(equivalentSnapshot);
        repeatedStructured.Declaration!.Value.Snapshot
            .Should()
            .BeSameAs(equivalentSnapshot);
        repeatedStructured.Context.Functions[repeatedStructured.FunctionSymbol!].Snapshot
            .Should()
            .BeSameAs(equivalentSnapshot);
    }

    [TestCase(TestName = "Lowered First Request Retains Structured IR For A Later Structured Request")]
    public async Task LoweredFirstShouldRetainStructuredIntermediateRepresentation()
    {
        using TestProject project = await TestProject.Create(Simple);
        using CompilerInspectionService service = new();
        SourceSnapshot snapshot = project.Snapshot(Simple);
        CompilerInspectionResult lowered = await service.InspectAsync(
            project.Request(snapshot, InspectionStage.LoweredIR, "Calculate"), CancellationToken.None);
        service.Cache.TryGet(CacheKey(lowered), out CompilerInspectionCacheEntry loweredEntry)
            .Should()
            .BeTrue();
        loweredEntry.StructuredIR
            .Should()
            .NotBeNull();
        loweredEntry.LoweredIR
            .Should()
            .NotBeNull();

        CompilerInspectionResult structured = await service.InspectAsync(project.Request(snapshot, marker: "Calculate"), CancellationToken.None);
        service.Cache.TryGet(CacheKey(structured), out CompilerInspectionCacheEntry structuredEntry)
            .Should()
            .BeTrue();
        structuredEntry.StructuredIR
            .Should()
            .BeSameAs(loweredEntry.StructuredIR);
        structuredEntry.StructuredDocument
            .Should()
            .BeSameAs(structured.Document);
        lowered.Document!.Text
            .Should()
            .Contain("load %local0");
        structured.Document!.Text
            .Should()
            .Contain("local int32 %local0");
    }

    [TestCase(TestName = "Inspection Cache Separates Changed Content Project Configuration And Source Membership")]
    public async Task InspectionCacheShouldUseCompleteProjectSnapshotIdentity()
    {
        const string main = Header + "public int32 First() { return 1; }";
        const string dependency = Header + "public int32 Helper() { return 2; }";
        using TestProject project = await TestProject.Create(main, dependency);
        using CompilerInspectionService service = new();
        SourceSnapshot firstSnapshot = project.Snapshot(main, 4);
        CompilerInspectionResult first = await service.InspectAsync(project.Request(firstSnapshot, marker: "First"), CancellationToken.None);
        CompilerInspectionResult equivalent = await service.InspectAsync(
            project.Request(project.Snapshot(main, 9), marker: "First"), CancellationToken.None);
        equivalent.Document
            .Should()
            .BeSameAs(first.Document);

        const string changedMain = Header + "public int32 First() { return 3; }";
        CompilerInspectionResult changedBody = await service.InspectAsync(
            project.Request(project.Snapshot(changedMain, 4), marker: "First"), CancellationToken.None);
        changedBody.Document!.Text
            .Should()
            .Contain("return 3;");
        changedBody.Document
            .Should()
            .NotBeSameAs(first.Document);

        await File.AppendAllTextAsync(project.ProjectPath, "\n# retained configuration revision\n");
        CompilerInspectionResult changedConfiguration = await service.InspectAsync(
            project.Request(project.Snapshot(main, 4), marker: "First"), CancellationToken.None);
        changedConfiguration.Document
            .Should()
            .NotBeSameAs(first.Document);

        string updatedConfiguration = TestProject.Configuration.Replace(
            "sources = [\"Sources/**/*.sus\"]", "sources = [\"Sources/A.sus\"]", StringComparison.Ordinal);
        await File.WriteAllTextAsync(project.ProjectPath, updatedConfiguration);
        CompilerInspectionResult changedMembership = await service.InspectAsync(
            project.Request(project.Snapshot(main, 4), marker: "First"), CancellationToken.None);
        changedMembership.Context.Sources
            .Should()
            .ContainSingle();
        changedMembership.Document
            .Should()
            .NotBeSameAs(changedConfiguration.Document);
        service.Cache.Count
            .Should()
            .Be(4);
    }

    [TestCase(TestName = "Inspection Cache Invalidates Callers When A Dependency Signature Changes")]
    public async Task InspectionCacheShouldInvalidateWhenDependencySignatureChanges()
    {
        const string caller = Header + "public int32 Caller() { return Helper(1); }";
        const string callee = Header + "public int32 Helper(int32 value) { return value; }";
        const string changedCallee = Header + "public int32 Helper(bool value) { return 1; }";
        using TestProject project = await TestProject.Create(caller, callee);
        using CompilerInspectionService service = new();
        SourceSnapshot callerSnapshot = project.Snapshot(caller);
        SourceSnapshot calleeSnapshot = project.Snapshot(callee, 4, "B.sus");
        CompilerInspectionResult original = await service.InspectAsync(
            project.Request(callerSnapshot, marker: "Caller", additional: [calleeSnapshot]), CancellationToken.None);
        CompilerInspectionResult invalid = await service.InspectAsync(
            project.Request(callerSnapshot, marker: "Caller", additional: [project.Snapshot(changedCallee, 4, "B.sus")]), CancellationToken.None);

        original.Succeeded
            .Should()
            .BeTrue();
        invalid.Succeeded
            .Should()
            .BeFalse();
        invalid.Diagnostics
            .Should()
            .NotBeEmpty();
        service.Cache.Count
            .Should()
            .Be(1);

        CompilerInspectionResult restored = await service.InspectAsync(
            project.Request(project.Snapshot(caller, 9), marker: "Caller", additional: [project.Snapshot(callee, 9, "B.sus")]), CancellationToken.None);
        restored.Succeeded
            .Should()
            .BeTrue();
        restored.Document
            .Should()
            .BeSameAs(original.Document);
    }

    [TestCase(TestName = "Inspection Cache Reuses Callers Across Unrelated And Called Function Body Changes")]
    public async Task InspectionCacheShouldReuseCallerWhenCallableContractsRemainEquivalent()
    {
        const string caller = Header + "public int32 Calculate(int32 value) { return Double(value) + 1; }";
        const string dependency = Header + "public int32 Double(int32 value) { return value * 2; } public int32 Unrelated(int32 value) { return value + 100; }";
        const string changedDependency = Header + "public int32 Double(int32 value) { return value * 3; } public int32 Unrelated(bool value) { return 100; }";
        const string changedAgain = Header + "public int32 Double(int32 value) { return value * 4; } public int32 Unrelated(bool value) { return 101; }";
        using TestProject project = await TestProject.Create(caller, dependency);
        using CompilerInspectionService service = new();
        CompilerInspectionResult original = await service.InspectAsync(
            project.Request(project.Snapshot(caller, 3, "A.sus"), marker: "Calculate", additional: [project.Snapshot(dependency, 3, "B.sus")]),
            CancellationToken.None);
        service.Cache.TryGet(CacheKey(original), out CompilerInspectionCacheEntry originalEntry)
            .Should()
            .BeTrue();
        CompilerInspectionResult originalLowered = await service.InspectAsync(
            project.Request(project.Snapshot(caller, 3, "A.sus"), InspectionStage.LoweredIR, "Calculate",
                [project.Snapshot(dependency, 3, "B.sus")]), CancellationToken.None);
        CompilerInspectionResult changed = await service.InspectAsync(
            project.Request(project.Snapshot(caller, 4, "A.sus"), marker: "Calculate", additional: [project.Snapshot(changedDependency, 4, "B.sus")]),
            CancellationToken.None);
        CompilerInspectionResult changedLowered = await service.InspectAsync(
            project.Request(project.Snapshot(caller, 4, "A.sus"), InspectionStage.LoweredIR, "Calculate",
                [project.Snapshot(changedDependency, 4, "B.sus")]), CancellationToken.None);
        CompilerInspectionResult changedAgainResult = await service.InspectAsync(
            project.Request(project.Snapshot(caller, 5, "A.sus"), marker: "Calculate", additional: [project.Snapshot(changedAgain, 5, "B.sus")]),
            CancellationToken.None);

        original.Succeeded
            .Should()
            .BeTrue();
        changed.Succeeded
            .Should()
            .BeTrue();
        changed.Document
            .Should()
            .BeSameAs(original.Document);
        changedAgainResult.Document
            .Should()
            .BeSameAs(original.Document);
        changedLowered.Document
            .Should()
            .BeSameAs(originalLowered.Document);
        changed.Context.Sources[1]
            .Should()
            .NotBeSameAs(original.Context.Sources[1]);
        changed.Context.Sources[1].Text
            .Should()
            .Be(changedDependency);
        service.Cache.TryGet(CacheKey(changedAgainResult), out CompilerInspectionCacheEntry latestEntry)
            .Should()
            .BeTrue();
        latestEntry.StructuredIR
            .Should()
            .BeSameAs(originalEntry.StructuredIR);

        CompilerInspectionResult oldCallee = await service.InspectFunctionAsync(original.Context, "__sushi_package_function_1",
            InspectionStage.StructuredIR, CancellationToken.None);
        CompilerInspectionResult currentCallee = await service.InspectFunctionAsync(changed.Context, "__sushi_package_function_1",
            InspectionStage.StructuredIR, CancellationToken.None);
        oldCallee.Document!.Text
            .Should()
            .Contain("value * 2");
        currentCallee.Document!.Text
            .Should()
            .Contain("value * 3");
    }

    [TestCase(TestName = "Inspection Cache Reuses A Function After A Same-File Sibling Moves Its Declaration")]
    public async Task InspectionCacheShouldRebaseSelectedDeclarationOwnershipAcrossSameFileEdits()
    {
        const string firstSource = Header + "public int32 Unrelated() { return 1; } public int32 Calculate(int32 value) { return value + 1; }";
        const string changedSource = Header + "public int32 Unrelated() { return 123456789; } public int32 Calculate(int32 value) { return value + 1; }";
        using TestProject project = await TestProject.Create(firstSource);
        using CompilerInspectionService service = new();
        CompilerInspectionResult first = await service.InspectAsync(
            project.Request(project.Snapshot(firstSource, 1), marker: "Calculate"), CancellationToken.None);
        SourceSnapshot changedSnapshot = project.Snapshot(changedSource, 1);
        CompilerInspectionResult changed = await service.InspectAsync(
            project.Request(changedSnapshot, marker: "Calculate"), CancellationToken.None);

        changed.Succeeded
            .Should()
            .BeTrue();
        changed.Document
            .Should()
            .BeSameAs(first.Document);
        changed.Declaration!.Value.Start
            .Should()
            .NotBe(first.Declaration!.Value.Start);
        changed.Declaration.Value.Snapshot
            .Should()
            .BeSameAs(changedSnapshot);
        changed.Context.Functions[changed.FunctionSymbol!].Snapshot
            .Should()
            .BeSameAs(changedSnapshot);
    }

    [TestCase("public int32 Helper(int32 input) { return input; }")]
    [TestCase("private int32 Helper(int32 value) { return value; }")]
    [TestCase("public int32 Other(int32 value) { return value; }")]
    public async Task InspectionCacheShouldRebindWhenNamedCallableResolutionChanges(string changedCallee)
    {
        const string caller = Header + "public int32 Caller() { return Helper(value: 1); }";
        const string callee = Header + "public int32 Helper(int32 value) { return value; }";
        using TestProject project = await TestProject.Create(caller, callee);
        using CompilerInspectionService service = new();
        CompilerInspectionResult original = await service.InspectAsync(
            project.Request(project.Snapshot(caller, 1, "A.sus"), marker: "Caller", additional: [project.Snapshot(callee, 1, "B.sus")]),
            CancellationToken.None);
        CompilerInspectionResult changed = await service.InspectAsync(
            project.Request(project.Snapshot(caller, 2, "A.sus"), marker: "Caller", additional: [project.Snapshot(changedCallee, 2, "B.sus")]),
            CancellationToken.None);

        original.Succeeded
            .Should()
            .BeTrue();
        changed.Succeeded
            .Should()
            .BeFalse();
        changed.Document
            .Should()
            .BeNull();
        changed.Diagnostics
            .Should()
            .NotBeEmpty();
    }

    [TestCase(TestName = "Inspection Cache Invalidates When Earlier Declarations Shift A Selected Compiler Symbol")]
    public async Task InspectionCacheShouldInvalidateWhenSelectedSymbolChanges()
    {
        const string originalSource = Header + "public int32 Calculate() { return Helper(1); } public int32 Helper(int32 value) { return value; }";
        const string changedSource = Header + "public int32 Added() { return 0; } public int32 Calculate() { return Helper(1); } public int32 Helper(int32 value) { return value; }";
        using TestProject project = await TestProject.Create(originalSource);
        using CompilerInspectionService service = new();
        CompilerInspectionResult original = await service.InspectAsync(
            project.Request(project.Snapshot(originalSource), marker: "Calculate"), CancellationToken.None);
        CompilerInspectionResult changed = await service.InspectAsync(
            project.Request(project.Snapshot(changedSource), marker: "Calculate"), CancellationToken.None);

        original.FunctionSymbol
            .Should()
            .Be("__sushi_package_function_0");
        changed.FunctionSymbol
            .Should()
            .Be("__sushi_package_function_1");
        changed.Document
            .Should()
            .NotBeSameAs(original.Document);
    }

    [TestCase(TestName = "Inspection Cache Does Not Share Equal Generated Symbols Between Projects")]
    public async Task InspectionCacheShouldIsolateDistinctProjectIdentities()
    {
        using TestProject firstProject = await TestProject.Create(Simple);
        using TestProject secondProject = await TestProject.Create(Simple);
        using CompilerInspectionService service = new();
        CompilerInspectionResult first = await service.InspectAsync(
            firstProject.Request(firstProject.Snapshot(Simple), marker: "Calculate"), CancellationToken.None);
        CompilerInspectionResult second = await service.InspectAsync(
            secondProject.Request(secondProject.Snapshot(Simple), marker: "Calculate"), CancellationToken.None);

        first.FunctionSymbol
            .Should()
            .Be(second.FunctionSymbol);
        second.Document
            .Should()
            .NotBeSameAs(first.Document);
        service.Cache.Count
            .Should()
            .Be(2);
    }

    [TestCase(TestName = "Inspection Cache LRU Eviction Preserves Returned Documents And Disposal Clears Entries")]
    public async Task InspectionCacheShouldEvictWithoutInvalidatingReturnedDocuments()
    {
        const string text = Header + "public int32 First() { return 1; } public int32 Second() { return 2; }";
        using TestProject project = await TestProject.Create(text);
        using CompilerInspectionService service = CompilerInspectionService.CreateForTesting(1);
        SourceSnapshot snapshot = project.Snapshot(text);
        CompilerInspectionResult first = await service.InspectAsync(project.Request(snapshot, marker: "First"), CancellationToken.None);
        string firstText = first.Document!.Text;
        CompilerInspectionResult second = await service.InspectAsync(project.Request(snapshot, marker: "Second"), CancellationToken.None);
        service.Cache.Count
            .Should()
            .Be(1);
        first.Document.Text
            .Should()
            .Be(firstText);

        CompilerInspectionResult firstAgain = await service.InspectAsync(project.Request(snapshot, marker: "First"), CancellationToken.None);
        firstAgain.Document
            .Should()
            .NotBeSameAs(first.Document);
        firstAgain.Document!.Text
            .Should()
            .Be(firstText);
        second.Document!.Text
            .Should()
            .Contain("return 2;");
        service.Dispose();
        service.Cache.Count
            .Should()
            .Be(0);
    }

    [TestCase(TestName = "Inspection Cache Leaves Failed And Cancelled Requests Uncached")]
    public async Task FailedAndCancelledInspectionShouldNotPopulateTheCache()
    {
        const string invalid = Header + "public int32 Broken() { return absent; }";
        using TestProject project = await TestProject.Create(invalid);
        using CompilerInspectionService service = new();
        CompilerInspectionResult failed = await service.InspectAsync(project.Request(project.Snapshot(invalid)), CancellationToken.None);
        failed.Succeeded
            .Should()
            .BeFalse();
        service.Cache.Count
            .Should()
            .Be(0);

        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();
        Func<Task> inspect = () => service.InspectAsync(project.Request(project.Snapshot(Simple)), cancellation.Token);
        await inspect
            .Should()
            .ThrowAsync<OperationCanceledException>();
        service.Cache.Count
            .Should()
            .Be(0);
    }

    [TestCase(TestName = "Concurrent Compatible And Different Snapshot Requests Remain Correct")]
    public async Task ConcurrentInspectionRequestsShouldRemainIsolated()
    {
        using TestProject project = await TestProject.Create(Simple);
        using CompilerInspectionService service = new();
        SourceSnapshot firstSnapshot = project.Snapshot(Simple, 4);
        SourceSnapshot equivalentSnapshot = project.Snapshot(Simple, 5);
        string changed = Simple.Replace("value + 1", "value + 2", StringComparison.Ordinal);
        SourceSnapshot changedSnapshot = project.Snapshot(changed, 4);

        Task<CompilerInspectionResult> firstTask = service.InspectAsync(project.Request(firstSnapshot, marker: "Calculate"), CancellationToken.None);
        Task<CompilerInspectionResult> equivalentTask = service.InspectAsync(project.Request(equivalentSnapshot, marker: "Calculate"), CancellationToken.None);
        Task<CompilerInspectionResult> changedTask = service.InspectAsync(project.Request(changedSnapshot, marker: "Calculate"), CancellationToken.None);
        CompilerInspectionResult[] results = await Task.WhenAll(firstTask, equivalentTask, changedTask);

        results[0].Succeeded
            .Should()
            .BeTrue();
        results[1].Succeeded
            .Should()
            .BeTrue();
        results[2].Succeeded
            .Should()
            .BeTrue();
        results[0].Document!.Text
            .Should()
            .Be(results[1].Document!.Text);
        results[2].Document!.Text
            .Should()
            .Contain("value + 2");
        results[2].Context.Sources[0]
            .Should()
            .BeSameAs(changedSnapshot);
        service.Cache.Count
            .Should()
            .Be(2);
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
        using CompilerInspectionService service = new();
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
        using CompilerInspectionService service = new();
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
        using CompilerInspectionService service = new();
        CompilerInspectionResult result = await service.InspectAsync(project.Request(snapshot), CancellationToken.None);
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
        InspectionDefinitionResolver.Resolve(document, document.Text.IndexOf(' '))
            .Should()
            .BeNull("whitespace is not a semantic occurrence");
        InspectionDefinitionResolver.Resolve(document, document.Text.IndexOf(';'))
            .Should()
            .BeNull("punctuation is not a semantic occurrence");
        InspectionDefinitionResolver.Resolve(document, -1)
            .Should()
            .BeNull();
        InspectionDefinitionResolver.Resolve(document, document.Text.Length)
            .Should()
            .BeNull();

        InspectionSymbolOccurrence definition = document.Occurrences.Single(item => item.IsDefinition && item.Identity == reference.Identity);
        InspectionDefinitionResolver.Resolve(document, definition.Start)
            .Should()
            .BeNull("a definition is already at its destination and must not create a self-targeting link");
    }

    [TestCase(TestName = "Recursive Function References Navigate To Their Own Declaration")]
    public async Task RecursiveFunctionReferenceShouldResolveToItsDefinition()
    {
        const string recursive = Header + "public int32 Recurse(int32 value) { if (value <= 0) { return 0; } return Recurse(value - 1); }";
        using TestProject project = await TestProject.Create(recursive);
        SourceSnapshot snapshot = project.Snapshot(recursive);
        using CompilerInspectionService service = new();
        CompilerInspectionResult result = await service.InspectAsync(project.Request(snapshot, marker: "Recurse(value"), CancellationToken.None);

        result.Succeeded
            .Should()
            .BeTrue();
        InspectionDocument document = result.Document!;
        InspectionSymbolIdentity identity = new(InspectionSymbolKind.Function, result.FunctionSymbol!);
        InspectionSymbolOccurrence definition = document.Occurrences.Single(item => item.IsDefinition && item.Identity == identity);
        InspectionSymbolOccurrence recursiveCall = document.Occurrences.Single(item => !item.IsDefinition && item.Identity == identity);

        InspectionDefinitionResolver.Resolve(document, definition.Start)
            .Should()
            .BeNull();
        (InspectionSymbolOccurrence Origin, InspectionSymbolOccurrence Definition)? resolved =
            InspectionDefinitionResolver.Resolve(document, recursiveCall.Start);
        resolved
            .Should()
            .NotBeNull();
        resolved!.Value.Origin
            .Should()
            .Be(recursiveCall);
        resolved.Value.Definition
            .Should()
            .Be(definition);
    }

    [TestCase(TestName = "Local Parameters Slots Blocks And Temporaries Resolve By Semantic Identity")]
    public async Task LocalGeneratedReferencesShouldResolveToTheirDefinitions()
    {
        const string source = Header + "public int32 Select(int32 value) { int32 doubled = value + value; if (doubled > 10) { return doubled; } return doubled + 1; }";
        using TestProject project = await TestProject.Create(source);
        SourceSnapshot snapshot = project.Snapshot(source);
        using CompilerInspectionService service = new();
        CompilerInspectionResult result = await service.InspectAsync(project.Request(snapshot, InspectionStage.LoweredIR, "Select"), CancellationToken.None);

        result.Succeeded
            .Should()
            .BeTrue();
        InspectionDocument document = result.Document!;
        foreach (InspectionSymbolKind kind in new[]
        {
            InspectionSymbolKind.Parameter,
            InspectionSymbolKind.Local,
            InspectionSymbolKind.BasicBlock,
            InspectionSymbolKind.Temporary
        })
        {
            InspectionSymbolOccurrence[] references = [.. document.Occurrences.Where(item => !item.IsDefinition && item.Identity.Kind == kind)];
            references
                .Should()
                .NotBeEmpty($"the Lowered IR records {kind} references");

            foreach (InspectionSymbolOccurrence reference in references)
            {
                (InspectionSymbolOccurrence Origin, InspectionSymbolOccurrence Definition)? resolved =
                    InspectionDefinitionResolver.Resolve(document, reference.Start);
                resolved
                    .Should()
                    .NotBeNull();
                resolved!.Value.Origin
                    .Should()
                    .Be(reference);
                resolved.Value.Definition.IsDefinition
                    .Should()
                    .BeTrue();
                resolved.Value.Definition.Identity
                    .Should()
                    .Be(reference.Identity);
            }
        }
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
        using CompilerInspectionService service = new();
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
        using CompilerInspectionService service = new();
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
        using CompilerInspectionService service = new();
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
        using CompilerInspectionService service = new();
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
        using CompilerInspectionService service = new();
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
        using CompilerInspectionService service = new();
        CompilerInspectionResult result = await service.InspectAsync(project.Request(snapshot), CancellationToken.None);
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
        using CompilerInspectionService service = new();
        CompilerInspectionResult result = await service.InspectAsync(project.Request(snapshot, marker: "Value"), CancellationToken.None);
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
        using CompilerInspectionService service = new();
        CompilerInspectionResult result = await service.InspectAsync(project.Request(snapshot), CancellationToken.None);
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
        using CompilerInspectionService service = new();
        CompilerInspectionResult result = await service.InspectAsync(project.Request(project.Snapshot(Simple)), CancellationToken.None);
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
        Func<Task> inspect = () => InspectWithDisposableService(request, CancellationToken.None);
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
        Func<Task> inspect = () => InspectWithDisposableService(request, CancellationToken.None);
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
        Func<Task> inspect = () => InspectWithDisposableService(project.Request(snapshot, additional: [duplicate]), CancellationToken.None);
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
        Func<Task> missing = () => InspectWithDisposableService(project.Request(absent), CancellationToken.None);
        await missing
            .Should()
            .ThrowAsync<ArgumentException>();
        SourceSnapshot remote = SourceSnapshot.FromText(new Uri("https://example.test/Source.sus"), 1, Simple);
        Func<Task> invalid = () => InspectWithDisposableService(project.Request(remote), CancellationToken.None);
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
        Func<Task> inspect = () => InspectWithDisposableService(request, cancellation.Token);
        await inspect
            .Should()
            .ThrowAsync<OperationCanceledException>();
        Func<Task> missing = () => InspectWithDisposableService(null!, CancellationToken.None);
        await missing
            .Should()
            .ThrowAsync<ArgumentNullException>();
    }

    private static CompilerInspectionCacheKey CacheKey(CompilerInspectionResult result)
        => CompilerInspectionCache.CreateKey(result.Context.ProjectPath, result.Context.ProjectSnapshot, result.Context.Sources,
            result.FunctionSymbol!, result.Declaration!.Value);

    private static async Task<CompilerInspectionResult> InspectWithDisposableService(CompilerInspectionRequest request,
        CancellationToken cancellationToken)
    {
        using CompilerInspectionService service = new();
        return await service.InspectAsync(request, cancellationToken);
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

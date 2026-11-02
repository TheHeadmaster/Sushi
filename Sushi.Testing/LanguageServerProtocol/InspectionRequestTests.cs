using FluentAssertions;
using NUnit.Framework;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sushi.Inspection;
using Sushi.LanguageServerProtocol;
using Sushi.Source;

namespace Sushi.Testing.LanguageServerProtocol;

/// <summary>
/// Exercises actual workspace capture, project membership, compiler inspection, and artifact ownership.
/// </summary>
[TestFixture]
public class InspectionRequestTests
{
    private const string Header = "package Example; namespace Example;\n";
    private const string Source = Header + "public int32 Value(int32 input) { return input + 1; }\n";

    [TestCase("structuredIR", InspectionStage.StructuredIR, "return (input + 1);")]
    [TestCase("loweredIR", InspectionStage.LoweredIR, "checked +")]
    public async Task RequestShouldInspectCurrentUnsavedSourceWithoutNativeGeneration(string stage, InspectionStage expected, string fragment)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        using Project project = await Project.Create(Source);
        string unsaved = Source.Replace("+ 1", "+ 2", StringComparison.Ordinal);
        await project.Workspace.OpenDocument(project.SourceUri, 7, unsaved, CancellationToken.None);
        string[] before = Directory.GetFiles(project.DirectoryPath, "*", SearchOption.AllDirectories);
        InspectFunctionResponse response = await project.Inspect(stage, version: 7);
        response.Succeeded
            .Should()
            .BeTrue();
        response.Stage
            .Should()
            .Be(stage);
        response.FunctionLabel
            .Should()
            .Be("Value");
        response.SourceVersion
            .Should()
            .Be(7);
        response.SourceUri
            .Should()
            .Be(project.SourceUri.AbsoluteUri);
        response.Text
            .Should()
            .Contain(fragment.Replace("+ 1", "+ 2", StringComparison.Ordinal));
        InspectionArtifact artifact = project.Artifacts.Find(response.ArtifactId!)!;
        artifact.DocumentUri
            .Should()
            .Be(response.DocumentUri);
        new Uri(artifact.DocumentUri).Scheme
            .Should()
            .Be("sushi-inspect");
        artifact.Result.Stage
            .Should()
            .Be(expected);
        artifact.Result.Document!.Text
            .Should()
            .Be(response.Text);
        artifact.Result.Document.Occurrences
            .Should()
            .NotBeEmpty();
        artifact.Result.Context.Sources[0].Text
            .Should()
            .Be(unsaved);
        (await File.ReadAllTextAsync(project.SourceUri.LocalPath))
            .Should()
            .Be(Source);
        Directory.GetFiles(project.DirectoryPath, "*", SearchOption.AllDirectories)
            .Should()
            .BeEquivalentTo(before);
    }

    [TestCase(TestName = "Version Mismatch Fails Without Registering A Stale Artifact")]
    public async Task VersionMismatchShouldFailExplicitly()
    {
        using Project project = await Project.Create(Source);
        InspectFunctionResponse response = await project.Inspect("structuredIR", version: 99);
        response.Succeeded
            .Should()
            .BeFalse();
        response.Error
            .Should()
            .Contain("version")
            .And
            .Contain("Retry");
        project.Artifacts.Count
            .Should()
            .Be(0);
    }

    [TestCase(TestName = "Workspace Capture Releases Mutation Gate And Keeps Original Snapshot")]
    public async Task CapturedSnapshotShouldNotChangeWhenWorkspaceChanges()
    {
        using Project project = await Project.Create(Source);
        InspectionWorkspaceSnapshot captured = await project.Workspace.CaptureInspectionInputs(project.SourceUri, 1, CancellationToken.None);
        string changed = Source.Replace("+ 1", "+ 3", StringComparison.Ordinal);
        await project.Workspace.ChangeDocument(project.SourceUri, 2, changed, CancellationToken.None);
        captured.Selected.Text
            .Should()
            .Be(Source);
        captured.Selected.Version
            .Should()
            .Be(1);
        CompilerInspectionRequest request = new(project.ProjectPath, captured.Selected,
            captured.Selected.GetByteOffset(1, 35), InspectionStage.StructuredIR);
        using CompilerInspectionService inspection = new();
        CompilerInspectionResult old = await inspection.InspectAsync(request, CancellationToken.None);
        old.Document!.Text
            .Should()
            .Contain("input + 1");
        InspectFunctionResponse current = await project.Inspect("structuredIR", version: 2);
        current.Text
            .Should()
            .Contain("input + 3");
        (await project.Workspace.GetSemanticSyntax(project.SourceUri, CancellationToken.None))!.Snapshot.Text
            .Should()
            .Be(changed);
    }

    [TestCase(TestName = "Supplementary And Multibyte Text Before Function Uses UTF16 Request Coordinates")]
    public async Task RequestPositionsShouldUseCapturedUnicodeSnapshot()
    {
        string source = Header + "/* " + new string('界', 100) + " 😀 */ public int32 Value() { return 8; }\r\n";
        using Project project = await Project.Create(source);
        int character = source.Split('\n')[1].IndexOf("return", StringComparison.Ordinal);
        InspectFunctionResponse response = await project.Handler.Handle(new InspectFunctionParams
        {
            SourceUri = project.SourceUri.AbsoluteUri, SourceVersion = 1, Stage = "structuredIR", Position = new Position(1, character)
        }, CancellationToken.None);
        response.Succeeded
            .Should()
            .BeTrue();
        response.Text
            .Should()
            .Contain("return 8;");
    }

    [TestCase(TestName = "Open Dependency Snapshots Are Forwarded Without Binding Their Bodies")]
    public async Task UnsavedDependencySignatureShouldBeUsed()
    {
        const string source = Header + "public int32 Value() { return Helper(5); }";
        using Project project = await Project.Create(source, Header + "public int32 Helper(bool value) { return 0; }");
        string dependency = Header + "public int32 Helper(int32 value) { return absent; }";
        Uri dependencyUri = new(Path.Combine(project.DirectoryPath, "Sources", "B.sus"));
        await project.Workspace.OpenDocument(dependencyUri, 8, dependency, CancellationToken.None);
        InspectFunctionResponse response = await project.Inspect("loweredIR");
        response.Succeeded
            .Should()
            .BeTrue();
        InspectionArtifact artifact = project.Artifacts.Find(response.ArtifactId!)!;
        artifact.Result.Context.Sources.Single(snapshot => snapshot.Uri == dependencyUri).Version
            .Should()
            .Be(8);
        artifact.Result.Context.Sources.Single(snapshot => snapshot.Uri == dependencyUri).Text
            .Should()
            .Be(dependency);
        artifact.Result.Document!.Occurrences
            .Should()
            .Contain(item => item.Identity.Kind == InspectionSymbolKind.Function && !item.IsDefinition);
    }

    [TestCase(TestName = "Multiple Projects Return Deterministic Choices Until Explicitly Selected")]
    public async Task AmbiguousProjectMembershipShouldRequireSelection()
    {
        using Project project = await Project.Create(Source, secondProject: true);
        InspectFunctionResponse ambiguous = await project.Inspect("structuredIR");
        ambiguous.Succeeded
            .Should()
            .BeFalse();
        ambiguous.Projects
            .Should()
            .Equal(new Uri(project.ProjectPath).AbsoluteUri, new Uri(Path.Combine(project.DirectoryPath, "Second.susproj")).AbsoluteUri);
        project.Artifacts.Count
            .Should()
            .Be(0);
        InspectFunctionResponse selected = await project.Inspect("structuredIR", projectUri: ambiguous.Projects[1]);
        selected.Succeeded
            .Should()
            .BeTrue();
        project.Artifacts.Find(selected.ArtifactId!)!.Result.Context.ProjectPath
            .Should()
            .Be(Path.Combine(project.DirectoryPath, "Second.susproj"));
    }

    [TestCase(TestName = "Source In Nested Project Resolves Independently Of Workspace Root")]
    public async Task NestedProjectShouldResolveByConfiguredSourceMembership()
    {
        using Project project = await Project.Create(Source);
        string nestedDirectory = Path.Combine(project.DirectoryPath, "Nested", "Project");
        string nestedProject = Path.Combine(nestedDirectory, "Nested.susproj");
        string nestedSource = Path.Combine(nestedDirectory, "Sources", "A.sus");
        Directory.CreateDirectory(Path.GetDirectoryName(nestedSource)!);
        await File.WriteAllTextAsync(nestedProject, await File.ReadAllTextAsync(project.ProjectPath));
        await File.WriteAllTextAsync(nestedSource, Source);
        await project.Workspace.Initialize([new WorkspaceFolder
        {
            Uri = DocumentUri.FromFileSystemPath(project.DirectoryPath), Name = "Inspection"
        }], CancellationToken.None);
        await project.Workspace.OpenDocument(new Uri(nestedSource), 4, Source, CancellationToken.None);
        InspectFunctionResponse response = await project.Handler.Handle(project.Request("structuredIR") with
        {
            SourceUri = new Uri(nestedSource).AbsoluteUri, SourceVersion = 4
        }, CancellationToken.None);
        response.Succeeded
            .Should()
            .BeTrue();
        project.Artifacts.Find(response.ArtifactId!)!.Result.Context.ProjectPath
            .Should()
            .Be(nestedProject);
    }

    [TestCase(TestName = "Explicit Nonmember Project Does Not Broaden Source Inclusion")]
    public async Task ExplicitProjectMustIncludeSelectedSource()
    {
        using Project project = await Project.Create(Source, secondProject: true, secondSources: "Elsewhere/**/*.sus");
        InspectFunctionResponse response = await project.Inspect("structuredIR",
            projectUri: new Uri(Path.Combine(project.DirectoryPath, "Second.susproj")).AbsoluteUri);
        response.Succeeded
            .Should()
            .BeFalse();
        project.Artifacts.Count
            .Should()
            .Be(0);
    }

    [TestCase(TestName = "Configured Excludes Prevent Project Membership")]
    public async Task ExcludedSourceShouldHaveNoProject()
    {
        using Project project = await Project.Create(Source, exclude: true);
        InspectFunctionResponse response = await project.Inspect("structuredIR");
        response.Succeeded
            .Should()
            .BeFalse();
        response.Error
            .Should()
            .Contain("No Sushi project includes");
        project.Artifacts.Count
            .Should()
            .Be(0);
    }

    [TestCase(TestName = "Unrelated Nested Projects Do Not Become Arbitrary Inspection Owners")]
    public async Task MembershipShouldSelectOnlyTheMatchingProject()
    {
        using Project project = await Project.Create(Source, secondProject: true, secondSources: "Elsewhere/**/*.sus");
        InspectFunctionResponse response = await project.Inspect("loweredIR");
        response.Succeeded
            .Should()
            .BeTrue();
        project.Artifacts.Find(response.ArtifactId!)!.Result.Context.ProjectPath
            .Should()
            .Be(project.ProjectPath);
    }

    [TestCase("return absent;", TestName = "Selected Body Errors Are Protocol Diagnostics Without Artifacts")]
    [TestCase("return true;", TestName = "Selected Type Errors Are Protocol Diagnostics Without Artifacts")]
    public async Task SourceFailuresShouldNotCreateSuccessfulArtifacts(string body)
    {
        using Project project = await Project.Create(Header + $"public int32 Value() {{ {body} }}");
        InspectFunctionResponse response = await project.Inspect("structuredIR");
        response.Succeeded
            .Should()
            .BeFalse();
        response.Text
            .Should()
            .BeNull();
        response.Diagnostics
            .Should()
            .NotBeEmpty()
            .And
            .OnlyContain(diagnostic => diagnostic.Uri == project.SourceUri.AbsoluteUri && diagnostic.Version == 1);
        project.Artifacts.Count
            .Should()
            .Be(0);
    }

    [TestCase(TestName = "Invalid Project Configuration Is A Clear Failure Instead Of A Guessed Membership")]
    public async Task InvalidConfigurationShouldFail()
    {
        using Project project = await Project.Create(Source);
        await File.WriteAllTextAsync(project.ProjectPath, "name = 42");
        InspectFunctionResponse response = await project.Inspect("structuredIR");
        response.Succeeded
            .Should()
            .BeFalse();
        response.Error
            .Should()
            .Contain("project configuration");
        project.Artifacts.Count
            .Should()
            .Be(0);
    }

    [TestCase("llvm", 1, 0, TestName = "Unsupported Protocol Stage Fails")]
    [TestCase("structuredIR", 0, 0, TestName = "Non Function Selection Fails")]
    [TestCase("structuredIR", 999, 0, TestName = "Out Of Range Editor Position Fails")]
    public async Task InvalidRequestsShouldFailWithoutArtifacts(string stage, int line, int character)
    {
        using Project project = await Project.Create(Source);
        InspectFunctionResponse response = await project.Handler.Handle(new InspectFunctionParams
        {
            SourceUri = project.SourceUri.AbsoluteUri, SourceVersion = 1, Stage = stage, Position = new Position(line, character)
        }, CancellationToken.None);
        response.Succeeded
            .Should()
            .BeFalse();
        response.Error
            .Should()
            .NotBeNullOrEmpty()
            .And
            .NotContain("StackTrace");
        project.Artifacts.Count
            .Should()
            .Be(0);
    }

    [TestCase(TestName = "Artifacts Remain Distinct Across Versions And Stages Until Release Or Session Shutdown")]
    public async Task ArtifactLifecycleShouldRetainMetadataWithoutReplacement()
    {
        using Project project = await Project.Create(Source, capacity: 3);
        InspectFunctionResponse first = await project.Inspect("structuredIR");
        InspectFunctionResponse lowered = await project.Inspect("loweredIR");
        await project.Workspace.ChangeDocument(project.SourceUri, 2, Source.Replace("+ 1", "+ 9", StringComparison.Ordinal), CancellationToken.None);
        InspectFunctionResponse changed = await project.Inspect("structuredIR", version: 2);
        new[] { first.ArtifactId, lowered.ArtifactId, changed.ArtifactId }
            .Should()
            .OnlyHaveUniqueItems();
        new[] { first.DocumentUri, lowered.DocumentUri, changed.DocumentUri }
            .Should()
            .OnlyHaveUniqueItems();
        project.Artifacts.Find(first.ArtifactId!)!.Result.Context.Sources[0].Version
            .Should()
            .Be(1);
        project.Artifacts.Find(first.ArtifactId!)!.Result.Document!.Text
            .Should()
            .Contain("input + 1");
        InspectFunctionResponse full = await project.Inspect("structuredIR", version: 2);
        full.Succeeded
            .Should()
            .BeFalse();
        full.Error
            .Should()
            .Contain("session limit");
        await new ReleaseInspectionHandler(project.Artifacts).Handle(new ReleaseInspectionParams { ArtifactId = first.ArtifactId! }, CancellationToken.None);
        project.Artifacts.Find(first.ArtifactId!)
            .Should()
            .BeNull();
        (await project.Inspect("structuredIR", version: 2)).Succeeded
            .Should()
            .BeTrue();
        project.Artifacts.Find(lowered.ArtifactId!)
            .Should()
            .NotBeNull();
        project.Artifacts.Dispose();
        project.Artifacts.Count
            .Should()
            .Be(0);
    }

    [TestCase(TestName = "Cancelled Protocol Request Propagates Without Registering An Artifact")]
    public async Task CancellationShouldPropagate()
    {
        using Project project = await Project.Create(Source);
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();
        Func<Task> inspect = () => project.Handler.Handle(project.Request("structuredIR"), cancellation.Token);
        await inspect
            .Should()
            .ThrowAsync<OperationCanceledException>();
        project.Artifacts.Count
            .Should()
            .Be(0);
    }

    [TestCase(InspectionStage.StructuredIR, "structuredIR", TestName = "Call Navigation Generates And Reuses Same Stage Target From Captured Sources")]
    [TestCase(InspectionStage.LoweredIR, "loweredIR", TestName = "Lowered Call Navigation Keeps The Captured Stage And Snapshot")]
    public async Task DefinitionNavigationShouldUseRetainedContextAndReuseCompatibleArtifacts(InspectionStage stage, string stageName)
    {
        const string caller = "package Example; namespace Example;\npublic int32 Caller(int32 value) { return Helper(value); }";
        const string callee = "package Example; namespace Example;\npublic int32 Helper(int32 input) { return input + 1; }";
        using Project project = await Project.Create(caller, callee);
        InspectFunctionResponse response = await project.Inspect(stageName);
        response.Succeeded
            .Should()
            .BeTrue();
        InspectionArtifact sourceArtifact = project.Artifacts.Find(response.ArtifactId!)!;
        InspectionDocument sourceDocument = sourceArtifact.Result.Document!;
        InspectionSymbolOccurrence argument = sourceDocument.Occurrences.Single(item =>
            item.Identity == new InspectionSymbolIdentity(InspectionSymbolKind.Parameter, "__sushi_package_function_1", 0) && !item.IsDefinition);
        (int line, int character) = InspectionTextCoordinates.GetPosition(sourceDocument.Text, argument.Start)!.Value;
        string changedCallee = callee.Replace("input + 1", "input + 99", StringComparison.Ordinal);
        await File.WriteAllTextAsync(Path.Combine(project.DirectoryPath, "Sources", "B.sus"), changedCallee);

        using CompilerInspectionService inspection = new();
        InspectionDefinitionHandler handler = new(inspection, project.Artifacts);
        InspectionDefinitionParams request = new()
        {
            DocumentUri = DocumentUri.Parse(response.DocumentUri!), ArtifactId = response.ArtifactId!, Position = new Position(line, character)
        };
        InspectionDefinitionResponse target = await handler.Handle(request, CancellationToken.None);
        target.Found
            .Should()
            .BeTrue();
        target.Generated
            .Should()
            .BeTrue();
        target.Stage
            .Should()
            .Be(stageName);
        if (stage is InspectionStage.StructuredIR)
        {
            target.Text
                .Should()
                .Contain("input + 1")
                .And
                .NotContain("input + 99");
        }
        else
        {
            target.Text
                .Should()
                .Contain("checked +")
                .And
                .Contain("input")
                .And
                .NotContain("99");
        }
        target.TargetRange
            .Should()
            .NotBeNull();
        target.TargetArtifactId
            .Should()
            .NotBe(response.ArtifactId);
        project.Artifacts.Count
            .Should()
            .Be(2);

        InspectionDefinitionResponse repeated = await handler.Handle(request, CancellationToken.None);
        repeated.TargetArtifactId
            .Should()
            .Be(target.TargetArtifactId);
        repeated.Generated
            .Should()
            .BeFalse();
        project.Artifacts.Count
            .Should()
            .Be(2);

        InspectionSymbolOccurrence functionReference = sourceDocument.Occurrences.Single(item =>
            item.Identity == new InspectionSymbolIdentity(InspectionSymbolKind.Function, "__sushi_package_function_1") && !item.IsDefinition);
        (int functionLine, int functionCharacter) = InspectionTextCoordinates.GetPosition(sourceDocument.Text, functionReference.Start)!.Value;
        InspectionDefinitionResponse functionTarget = await handler.Handle(request with
        {
            Position = new Position(functionLine, functionCharacter)
        }, CancellationToken.None);
        functionTarget.Found
            .Should()
            .BeTrue();
        functionTarget.TargetArtifactId
            .Should()
            .Be(target.TargetArtifactId);
        functionTarget.Generated
            .Should()
            .BeFalse();
    }

    [TestCase(TestName = "Same File Callee Uses The Same Lazy Same Stage Navigation")]
    public async Task SameFileDefinitionShouldUseOriginatingStageAndContext()
    {
        const string source = "package Example; namespace Example;\npublic int32 Caller() { return Helper(5); } public int32 Helper(int32 input) { return input; }";
        using Project project = await Project.Create(source);
        InspectFunctionResponse response = await project.Inspect("structuredIR");
        InspectionArtifact artifact = project.Artifacts.Find(response.ArtifactId!)!;
        InspectionSymbolOccurrence reference = artifact.Result.Document!.Occurrences.Single(item =>
            item.Identity == new InspectionSymbolIdentity(InspectionSymbolKind.Function, "__sushi_package_function_1") && !item.IsDefinition);
        (int line, int character) = InspectionTextCoordinates.GetPosition(artifact.Result.Document.Text, reference.Start)!.Value;
        using CompilerInspectionService inspection = new();
        InspectionDefinitionResponse target = await new InspectionDefinitionHandler(inspection, project.Artifacts).Handle(
            new InspectionDefinitionParams { DocumentUri = DocumentUri.Parse(response.DocumentUri!), ArtifactId = response.ArtifactId!, Position = new Position(line, character) },
            CancellationToken.None);
        target.Found
            .Should()
            .BeTrue();
        target.Generated
            .Should()
            .BeTrue();
        target.Stage
            .Should()
            .Be("structuredIR");
        target.FunctionLabel
            .Should()
            .Be("Helper");
    }

    [TestCase(TestName = "Forged And Released Inspection Document URIs Do Not Resolve")]
    public async Task DefinitionNavigationShouldRejectMismatchedAndReleasedArtifacts()
    {
        using Project project = await Project.Create(Source);
        InspectFunctionResponse response = await project.Inspect("structuredIR");
        InspectionArtifact artifact = project.Artifacts.Find(response.ArtifactId!)!;
        InspectionDocument document = artifact.Result.Document!;
        InspectionSymbolOccurrence occurrence = document.Occurrences.First(item => item.IsDefinition);
        (int line, int character) = InspectionTextCoordinates.GetPosition(document.Text, occurrence.Start)!.Value;
        using CompilerInspectionService inspection = new();
        InspectionDefinitionHandler handler = new(inspection, project.Artifacts);
        InspectionDefinitionResponse forged = await handler.Handle(new InspectionDefinitionParams
        {
            DocumentUri = DocumentUri.Parse("sushi-inspect://other/forged/F.structuredIR"), ArtifactId = response.ArtifactId!, Position = new Position(line, character)
        }, CancellationToken.None);
        forged.Found
            .Should()
            .BeFalse();
        await new ReleaseInspectionHandler(project.Artifacts).Handle(new ReleaseInspectionParams { ArtifactId = response.ArtifactId! }, CancellationToken.None);
        InspectionDefinitionResponse released = await handler.Handle(new InspectionDefinitionParams
        {
            DocumentUri = DocumentUri.Parse(response.DocumentUri!), ArtifactId = response.ArtifactId!, Position = new Position(line, character)
        }, CancellationToken.None);
        released.Found
            .Should()
            .BeFalse();
        released.Error
            .Should()
            .Contain("no longer available");
    }

    private sealed class Project : IDisposable
    {
        private const string Configuration = """
            name = "Inspection"
            assembly = "Inspection"
            language-version = "1.0"
            [build]
            default = "debug"
            sources = ["Sources/**/*.sus"]
            [build.targets.debug]
            type = "Sushi.MVP.Debug"
            """;
        public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "Sushi.Testing", Guid.NewGuid().ToString("N"));
        public string ProjectPath => Path.Combine(this.DirectoryPath, "Inspection.susproj");
        public Uri SourceUri => new(Path.Combine(this.DirectoryPath, "Sources", "A.sus"));
        public WorkspaceOrchestrator Workspace { get; } = new();
        public InspectionArtifactStore Artifacts { get; }
        public InspectFunctionHandler Handler { get; }
        public CompilerInspectionService Inspection { get; } = new();

        private Project(int capacity)
        {
            this.Artifacts = new InspectionArtifactStore(capacity);
            this.Handler = new InspectFunctionHandler(this.Workspace, this.Inspection, this.Artifacts);
        }

        public static async Task<Project> Create(string source, string? dependency = null, bool secondProject = false,
            bool exclude = false, string secondSources = "Sources/**/*.sus", int capacity = 32)
        {
            Project project = new(capacity);
            Directory.CreateDirectory(Path.GetDirectoryName(project.SourceUri.LocalPath)!);
            try
            {
                string config = exclude ? Configuration.Replace("[build]", "[source]\nexclude = [\"Sources/A.sus\"]\n[build]", StringComparison.Ordinal) : Configuration;
                await File.WriteAllTextAsync(project.ProjectPath, config);
                await File.WriteAllTextAsync(project.SourceUri.LocalPath, source);
                if (dependency is not null)
                {
                    await File.WriteAllTextAsync(Path.Combine(project.DirectoryPath, "Sources", "B.sus"), dependency);
                }

                if (secondProject)
                {
                    await File.WriteAllTextAsync(Path.Combine(project.DirectoryPath, "Second.susproj"), Configuration.Replace("Sources/**/*.sus", secondSources, StringComparison.Ordinal));
                }

                await project.Workspace.Initialize([new WorkspaceFolder { Uri = DocumentUri.FromFileSystemPath(project.DirectoryPath), Name = "Inspection" }], CancellationToken.None);
                await project.Workspace.OpenDocument(project.SourceUri, 1, source, CancellationToken.None);
                return project;
            }
            catch
            {
                project.Dispose();
                throw;
            }
        }

        public InspectFunctionParams Request(string stage, int version = 1, string? projectUri = null) => new()
        {
            SourceUri = this.SourceUri.AbsoluteUri,
            SourceVersion = version,
            Position = new Position(1, 35),
            Stage = stage,
            ProjectUri = projectUri is null ? null : DocumentUri.Parse(projectUri)
        };

        public Task<InspectFunctionResponse> Inspect(string stage, int version = 1, string? projectUri = null) => this.Handler.Handle(this.Request(stage, version, projectUri), CancellationToken.None);

        public void Dispose()
        {
            this.Artifacts.Dispose();
            this.Workspace.Dispose();
            Directory.Delete(this.DirectoryPath, recursive: true);
        }
    }
}

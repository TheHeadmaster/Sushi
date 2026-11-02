using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Newtonsoft.Json;
using NUnit.Framework;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sushi.LanguageServerProtocol;
using Sushi.Source;
using Sushi.Workspaces;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Sushi.Testing.LanguageServerProtocol;

/// <summary>
/// Regresses the URI boundary shared by synchronization, semantic tokens, and inspection, including a real initialized server.
/// </summary>
[TestFixture]
public class InspectionDocumentLookupTests
{
    private static readonly string[] tokenTypes = ["class", "function", "parameter", "variable", "namespace"];
    private static readonly string[] tokenModifiers = ["declaration"];
    private static readonly string[] tokenFormats = ["relative"];
    private const string Source = "package Example; namespace Example;\npublic int32 Value() { return 1; }\npublic activity Application : task { public entry void Start() { return; } }";

    [TestCase("file:///c%3A/Projects/Sushi/Program.sus")]
    [TestCase("file:///C%3a/Projects/Space%20Name/%E7%95%8C.sus")]
    [TestCase("file:///C:/Projects/Sushi/Program.sus")]
    [TestCase("file:///tmp/Colon%3A%20Name/Program.sus")]
    [TestCase("file:///tmp/Space%20Name/%E7%95%8C%23File.sus")]
    [TestCase("file://server/share/Space%20Name/Program.sus")]
    public void ProtocolAndInspectionUrisShouldUseTheSameDocumentIdentity(string spelling)
    {
        ArgumentNullException.ThrowIfNull(spelling);
        Uri semanticUri = DocumentUri.Parse(spelling).ToUri();
        SourceSnapshot snapshot = SourceSnapshot.FromText(semanticUri, 7, Source);
        SushiWorkspace workspace = new();
        SourceDocument document = workspace.AddDocument(semanticUri, snapshot);
        InspectFunctionParams request = JsonConvert.DeserializeObject<InspectFunctionParams>(JsonConvert.SerializeObject(new
        {
            sourceUri = spelling, sourceVersion = 7, position = new { line = 1, character = 25 }, stage = "structuredIR", projectUri = spelling
        }))!;

        request.SourceUri
            .Should()
            .BeOfType<DocumentUri>();
        Uri inspectionUri = request.SourceUri.ToUri();
        inspectionUri.IsFile
            .Should()
            .BeTrue();
        Path.GetExtension(inspectionUri.LocalPath)
            .Should()
            .Be(".sus");
        inspectionUri.IsSamePath(semanticUri)
            .Should()
            .BeTrue();
        workspace.TryGetDocument(inspectionUri, out SourceDocument? found)
            .Should()
            .BeTrue();
        found
            .Should()
            .BeSameAs(document);
        request.ProjectUri!.ToUri().IsSamePath(semanticUri)
            .Should()
            .BeTrue();
    }

    [TestCase(TestName = "Escaped Drive Colon Demonstrates All Three Original Lookup Predicates")]
    public void EncodedDriveColonShouldNotBeParsedAsAnOrdinaryNetUri()
    {
        const string wire = "file:///c%3A/Projects/Sushi/Program.sus";
        Uri lspUri = DocumentUri.Parse(wire).ToUri();
        Uri.TryCreate(wire, UriKind.Absolute, out Uri? direct)
            .Should()
            .BeTrue();
        direct!.IsFile
            .Should()
            .BeTrue();
        Path.GetExtension(direct.LocalPath)
            .Should()
            .Be(".sus");
        SushiWorkspace workspace = new();
        workspace.AddDocument(lspUri, SourceSnapshot.FromText(lspUri, 1, Source));
        workspace.TryGetDocument(lspUri, out _)
            .Should()
            .BeTrue();
        workspace.TryGetDocument(direct, out _)
            .Should()
            .BeFalse();
    }

    [TestCase(TestName = "Semantic Syntax And Inspection Capture Share An Authoritative Unsaved Snapshot")]
    public async Task SemanticAndInspectionCaptureShouldResolveTheSameSnapshot()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "Lookup " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        using WorkspaceOrchestrator workspace = new();
        try
        {
            string file = Path.Combine(directory, "Space 界.sus");
            await File.WriteAllTextAsync(file, Source);
            string wire = ExtensionUri(new Uri(file));
            Uri syncUri = DocumentUri.Parse(wire).ToUri();
            string unsaved = Source.Replace("return 1", "return 7", StringComparison.Ordinal);
            await workspace.OpenDocument(syncUri, 7, unsaved, CancellationToken.None);
            InspectFunctionParams request = JsonConvert.DeserializeObject<InspectFunctionParams>(JsonConvert.SerializeObject(new { sourceUri = wire }))!;
            SourceSnapshot semantic = (await workspace.GetSemanticSyntax(DocumentUri.Parse(wire).ToUri(), CancellationToken.None))!.Snapshot;
            InspectionWorkspaceSnapshot capture = await workspace.CaptureInspectionInputs(request.SourceUri.ToUri(), 7, CancellationToken.None);
            capture.Selected
                .Should()
                .BeSameAs(semantic);
            capture.Selected.Text
                .Should()
                .Be(unsaved);
            await workspace.ChangeDocument(syncUri, 8, Source.Replace("return 1", "return 8", StringComparison.Ordinal), CancellationToken.None);
            await workspace.ChangeDocument(syncUri, 6, Source, CancellationToken.None);
            (await workspace.CaptureInspectionInputs(request.SourceUri.ToUri(), 8, CancellationToken.None)).Selected.Version
                .Should()
                .Be(8);
            await workspace.CloseDocument(syncUri, CancellationToken.None);
            await workspace.OpenDocument(syncUri, 1, unsaved, CancellationToken.None);
            (await workspace.CaptureInspectionInputs(request.SourceUri.ToUri(), 1, CancellationToken.None)).Selected.Text
                .Should()
                .Be(unsaved);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestCase(6)]
    [TestCase(7)]
    [TestCase(null)]
    public void StaleOrUnversionedUpdatesShouldNotReplaceAVersionedOpenDocument(int? incomingVersion)
    {
        Uri uri = new("file:///Version.sus");
        SourceSnapshot disk = SourceSnapshot.FromText(uri, null, Source);
        SourceSnapshot current = SourceSnapshot.FromText(uri, 7, Source.Replace("return 1", "return 7", StringComparison.Ordinal));
        SourceDocument document = new(uri, disk);
        document.UpdateEditor(current);
        document.UpdateEditor(SourceSnapshot.FromText(uri, incomingVersion, Source));
        document.CurrentSnapshot
            .Should()
            .BeSameAs(current);
        document.Close(disk);
        SourceSnapshot reopened = SourceSnapshot.FromText(uri, 1, Source);
        document.UpdateEditor(reopened);
        document.CurrentSnapshot
            .Should()
            .BeSameAs(reopened);
    }

    [TestCase(TestName = "Initialized LSP Shares Workspace Across Sync Semantic Tokens Inspection And Reopen")]
    public async Task InitializedServerShouldInspectTheDocumentUsedBySemanticTokens()
    {
        // A colon in a Unix directory reproduces the same escaped-path distinction as a Windows drive colon.
        string name = OperatingSystem.IsWindows() ? "Lookup " : "Lookup: ";
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", name + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string file = Path.Combine(directory, "Source 界.sus");
            string wire = ExtensionUri(new Uri(file));
            await File.WriteAllTextAsync(file, Source);
            await File.WriteAllTextAsync(Path.Combine(directory, "Inspection.susproj"), """
                name = "Lookup"
                assembly = "Lookup"
                language-version = "1.0"
                [build]
                default = "debug"
                sources = ["*.sus"]
                [build.targets.debug]
                type = "Sushi.MVP.Debug"
                """);
            await using ServerSession session = new(directory);
            JsonElement initialized = await session.Request("initialize", new
            {
                processId = (int?)null,
                capabilities = new { textDocument = new { semanticTokens = new
                {
                    requests = new { full = true }, tokenTypes = tokenTypes,
                    tokenModifiers = tokenModifiers, formats = tokenFormats
                } } },
                workspaceFolders = new[] { new { uri = ExtensionUri(new Uri(directory)), name = "Lookup" } }
            });
            string[] legend = [.. initialized.GetProperty("capabilities").GetProperty("semanticTokensProvider").GetProperty("legend").GetProperty("tokenTypes").EnumerateArray().Select(item => item.GetString()!)];
            await session.Notify("initialized", new { });
            string unsaved = Source.Replace("return 1", "return 7", StringComparison.Ordinal).Replace("Application", "EditorApplication", StringComparison.Ordinal);
            await session.Notify("textDocument/didOpen", new { textDocument = new { uri = wire, languageId = "sushi", version = 7, text = unsaved } });
            JsonElement tokens = await session.Request("textDocument/semanticTokens/full", new { textDocument = new { uri = wire } });
            HasClassToken(tokens, legend, unsaved, "EditorApplication")
                .Should()
                .BeTrue("semantic highlighting must see the unsaved didOpen snapshot in the production server's workspace");
            JsonElement inspected = await Inspect(session, wire, 7);
            inspected.GetProperty("succeeded").GetBoolean()
                .Should()
                .BeTrue(inspected.ToString());
            inspected.GetProperty("sourceVersion").GetInt32()
                .Should()
                .Be(7);
            inspected.GetProperty("text").GetString()
                .Should()
                .Contain("return 7;");
            await session.Notify("sourceInspection/release", new { artifactId = inspected.GetProperty("artifactId").GetString() });

            string newest = unsaved.Replace("return 7", "return 9", StringComparison.Ordinal).Replace("EditorApplication", "NewestApplication", StringComparison.Ordinal);
            await session.Notify("textDocument/didChange", new { textDocument = new { uri = wire, version = 8 }, contentChanges = new[] { new { text = newest } } });
            await session.Notify("textDocument/didChange", new { textDocument = new { uri = wire, version = 6 }, contentChanges = new[] { new { text = Source } } });
            JsonElement current = await Inspect(session, wire, 8);
            current.GetProperty("succeeded").GetBoolean()
                .Should()
                .BeTrue(current.ToString());
            current.GetProperty("text").GetString()
                .Should()
                .Contain("return 9;");
            (await Inspect(session, wire, 6)).GetProperty("succeeded").GetBoolean()
                .Should()
                .BeFalse();
            HasClassToken(await session.Request("textDocument/semanticTokens/full", new { textDocument = new { uri = wire } }), legend, newest, "NewestApplication")
                .Should()
                .BeTrue();

            await session.Notify("textDocument/didClose", new { textDocument = new { uri = wire } });
            string reopened = unsaved.Replace("return 7", "return 11", StringComparison.Ordinal);
            await session.Notify("textDocument/didOpen", new { textDocument = new { uri = wire, languageId = "sushi", version = 1, text = reopened } });
            JsonElement again = await Inspect(session, wire, 1);
            again.GetProperty("succeeded").GetBoolean()
                .Should()
                .BeTrue(again.ToString());
            again.GetProperty("text").GetString()
                .Should()
                .Contain("return 11;");

            string invalid = reopened.Replace("return 11", "return missing", StringComparison.Ordinal);
            await session.Notify("textDocument/didChange", new { textDocument = new { uri = wire, version = 2 }, contentChanges = new[] { new { text = invalid } } });
            await session.Notify("textDocument/didSave", new { textDocument = new { uri = wire } });
            JsonElement failed = await Inspect(session, wire, 2);
            failed.GetProperty("succeeded").GetBoolean()
                .Should()
                .BeFalse();
            failed.GetProperty("diagnostics").GetArrayLength()
                .Should()
                .BeGreaterThan(0);
            await session.WaitForDiagnostics(2);
            (await File.ReadAllTextAsync(file))
                .Should()
                .Be(Source);
            Directory.Exists(Path.Combine(directory, "bin"))
                .Should()
                .BeFalse();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestCase(TestName = "Initialized LSP Resolves Lazy Cross File Definition From The Captured Snapshot")]
    public async Task InitializedServerShouldResolveInspectionDefinitionRequest()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "Navigation " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory, "Sources"));
        try
        {
            string configuration = """
                name = "Navigation"
                assembly = "Navigation"
                language-version = "1.0"
                [build]
                default = "debug"
                sources = ["Sources/**/*.sus"]
                [build.targets.debug]
                type = "Sushi.MVP.Debug"
                """;
            string caller = "package Example; namespace Example;\npublic int32 Caller(int32 value) { return Helper(value); }";
            string callee = "package Example; namespace Example;\npublic int32 Helper(int32 input) { return input + 1; }";
            string unsavedCallee = callee.Replace("input + 1", "input + 2", StringComparison.Ordinal);
            string projectPath = Path.Combine(directory, "Navigation.susproj");
            string callerPath = Path.Combine(directory, "Sources", "A.sus");
            string calleePath = Path.Combine(directory, "Sources", "B.sus");
            await File.WriteAllTextAsync(projectPath, configuration);
            await File.WriteAllTextAsync(callerPath, caller);
            await File.WriteAllTextAsync(calleePath, callee);
            string callerUri = ExtensionUri(new Uri(callerPath));
            string calleeUri = ExtensionUri(new Uri(calleePath));
            await using ServerSession session = new(directory);
            await session.Request("initialize", new
            {
                processId = (int?)null,
                capabilities = new { textDocument = new { } },
                workspaceFolders = new[] { new { uri = ExtensionUri(new Uri(directory)), name = "Navigation" } }
            });
            await session.Notify("initialized", new { });
            await session.Notify("textDocument/didOpen", new { textDocument = new { uri = callerUri, languageId = "sushi", version = 7, text = caller } });
            await session.Notify("textDocument/didOpen", new { textDocument = new { uri = calleeUri, languageId = "sushi", version = 4, text = unsavedCallee } });

            JsonElement inspected = await session.Request("sourceInspection/inspectFunction", new
            {
                sourceUri = callerUri, sourceVersion = 7,
                position = new { line = 1, character = caller.Split('\n')[1].IndexOf("Caller", StringComparison.Ordinal) + 10 }, stage = "loweredIR"
            });
            inspected.GetProperty("succeeded").GetBoolean()
                .Should()
                .BeTrue(inspected.ToString());
            string text = inspected.GetProperty("text").GetString()!;
            int offset = text.IndexOf("arg0", StringComparison.Ordinal);
            offset
                .Should()
                .BeGreaterThanOrEqualTo(0);
            int line = text[..offset].Count(character => character == '\n');
            int character = offset - (text.LastIndexOf('\n', Math.Max(0, offset - 1)) + 1);

            string changedCallee = unsavedCallee.Replace("input + 2", "input + 99", StringComparison.Ordinal);
            await session.Notify("textDocument/didChange", new
            {
                textDocument = new { uri = calleeUri, version = 5 }, contentChanges = new[] { new { text = changedCallee } }
            });
            await File.WriteAllTextAsync(projectPath, "name = false");
            JsonElement definition = await session.Request("sourceInspection/definition", new
            {
                documentUri = inspected.GetProperty("documentUri").GetString(),
                artifactId = inspected.GetProperty("artifactId").GetString(), position = new { line, character }
            });
            definition.GetProperty("found").GetBoolean()
                .Should()
                .BeTrue(definition.ToString());
            definition.GetProperty("generated").GetBoolean()
                .Should()
                .BeTrue();
            definition.GetProperty("stage").GetString()
                .Should()
                .Be("loweredIR");
            definition.GetProperty("text").GetString()
                .Should()
                .Contain("checked +")
                .And
                .Contain("input")
                .And
                .Contain("2")
                .And
                .NotContain("99");
            definition.GetProperty("targetRange").GetProperty("start").GetProperty("line").GetInt32()
                .Should()
                .BeGreaterThanOrEqualTo(0);

            JsonElement reused = await session.Request("sourceInspection/definition", new
            {
                documentUri = inspected.GetProperty("documentUri").GetString(),
                artifactId = inspected.GetProperty("artifactId").GetString(), position = new { line, character }
            });
            reused.GetProperty("generated").GetBoolean()
                .Should()
                .BeFalse();
            reused.GetProperty("targetArtifactId").GetString()
                .Should()
                .Be(definition.GetProperty("targetArtifactId").GetString());
            await session.Notify("sourceInspection/release", new { artifactId = definition.GetProperty("targetArtifactId").GetString() });
            await session.Notify("sourceInspection/release", new { artifactId = inspected.GetProperty("artifactId").GetString() });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestCase(TestName = "Initialized LSP Binds Cross File Calls And Invalidates Caller Diagnostics From Unsaved Signatures")]
    public async Task InitializedServerShouldBindProjectFunctionsAndRefreshAffectedCallers()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "Project Diagnostics " + Guid.NewGuid().ToString("N"));
        string sourcesDirectory = Path.Combine(directory, "Sources");
        Directory.CreateDirectory(sourcesDirectory);
        try
        {
            string configuration = """
                name = "Diagnostics"
                assembly = "Diagnostics"
                language-version = "1.0"
                [build]
                default = "debug"
                sources = ["Sources/**/*.sus"]
                [build.targets.debug]
                type = "Sushi.MVP.Debug"
                """;
            string caller = """
                package Example;
                namespace Example;

                public int32 InspectMe(int32 input)
                {
                    int32 doubled = InspectMe2(input);

                    if (doubled > 10)
                    {
                        return doubled;
                    }

                    return doubled + 1;
                }
                """;
            string callee = """
                package Example;
                namespace Example;

                public int32 InspectMe2(int32 input)
                {
                    return input * 2;
                }
                """;
            string projectPath = Path.Combine(directory, "Diagnostics.susproj");
            string callerPath = Path.Combine(sourcesDirectory, "InspectMe.sus");
            string calleePath = Path.Combine(sourcesDirectory, "InspectMe2.sus");
            await File.WriteAllTextAsync(projectPath, configuration);
            await File.WriteAllTextAsync(callerPath, caller);
            await File.WriteAllTextAsync(calleePath, callee);
            string callerUri = ExtensionUri(new Uri(callerPath));
            string calleeUri = ExtensionUri(new Uri(calleePath));

            await using ServerSession session = new(directory);
            await session.Request("initialize", new
            {
                processId = (int?)null,
                capabilities = new { textDocument = new { } },
                workspaceFolders = new[] { new { uri = ExtensionUri(new Uri(directory)), name = "Diagnostics" } }
            });
            await session.Notify("initialized", new { });
            await session.Notify("textDocument/didOpen", new { textDocument = new { uri = callerUri, languageId = "sushi", version = 7, text = caller } });
            await session.Notify("textDocument/didOpen", new { textDocument = new { uri = calleeUri, languageId = "sushi", version = 4, text = callee } });

            JsonElement valid = await session.WaitForDiagnostics(callerUri, 7, diagnostics => diagnostics.GetArrayLength() == 0);
            valid.GetArrayLength()
                .Should()
                .Be(0);

            string changedSignature = callee.Replace("int32 input", "bool input", StringComparison.Ordinal);
            await session.Notify("textDocument/didChange", new
            {
                textDocument = new { uri = calleeUri, version = 5 }, contentChanges = new[] { new { text = changedSignature } }
            });
            JsonElement invalidated = await session.WaitForDiagnostics(callerUri, 7, diagnostics => diagnostics.GetArrayLength() > 0);
            invalidated.GetArrayLength()
                .Should()
                .BeGreaterThan(0);

            await session.Notify("textDocument/didChange", new
            {
                textDocument = new { uri = calleeUri, version = 6 }, contentChanges = new[] { new { text = callee } }
            });
            JsonElement restored = await session.WaitForDiagnostics(callerUri, 7, diagnostics => diagnostics.GetArrayLength() == 0);
            restored.GetArrayLength()
                .Should()
                .Be(0);

            Directory.Exists(Path.Combine(directory, "bin"))
                .Should()
                .BeFalse("diagnostic analysis must not invoke native compilation");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestCase(TestName = "Initialized LSP Publishes Project And Solution Diagnostics Once Per Analysis")]
    public async Task InitializedServerShouldPublishProjectDiagnosticsOncePerAnalysis()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "Diagnostic Ownership " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string configuration = """
                name = "Ownership"
                assembly = "Ownership"
                language-version = "1.0"
                [build]
                default = "debug"
                sources = ["*.sus"]
                [build.targets.debug]
                type = "Sushi.MVP.Debug"
                """;
            string source = "package Example; namespace Example; public int32 Value() { return 1; }";
            string projectPath = Path.Combine(directory, "Ownership.susproj");
            string solutionPath = Path.Combine(directory, "Workspace.susln");
            string sourcePath = Path.Combine(directory, "Value.sus");
            await File.WriteAllTextAsync(projectPath, configuration);
            await File.WriteAllTextAsync(solutionPath, "name = [");
            await File.WriteAllTextAsync(sourcePath, source);
            string projectUri = ExtensionUri(new Uri(projectPath));
            string solutionUri = ExtensionUri(new Uri(solutionPath));
            string sourceUri = ExtensionUri(new Uri(sourcePath));

            await using ServerSession session = new(directory);
            await session.Request("initialize", new
            {
                processId = (int?)null,
                capabilities = new { textDocument = new { } },
                workspaceFolders = new[] { new { uri = ExtensionUri(new Uri(directory)), name = "Ownership" } }
            });
            await session.Notify("initialized", new { });

            IReadOnlyList<JsonElement> initialProject = await session.CollectDiagnosticPublications(projectUri, null);
            initialProject
                .Should()
                .ContainSingle()
                .Which
                .GetArrayLength()
                .Should()
                .Be(0);
            IReadOnlyList<JsonElement> initialSolution = await session.CollectDiagnosticPublications(solutionUri, null);
            initialSolution
                .Should()
                .ContainSingle()
                .Which
                .GetArrayLength()
                .Should()
                .BeGreaterThan(0);
            IReadOnlyList<JsonElement> initialSource = await session.CollectDiagnosticPublications(sourceUri, null);
            initialSource
                .Should()
                .ContainSingle()
                .Which
                .GetArrayLength()
                .Should()
                .Be(0);

            await session.Notify("textDocument/didOpen", new
            {
                textDocument = new { uri = projectUri, languageId = "susproj", version = 1, text = configuration }
            });
            IReadOnlyList<JsonElement> openedProject = await session.CollectDiagnosticPublications(projectUri, 1);
            openedProject
                .Should()
                .ContainSingle()
                .Which
                .GetArrayLength()
                .Should()
                .Be(0);

            string invalidConfiguration = configuration.Replace("name = \"Ownership\"", "name = \"\"", StringComparison.Ordinal);
            await session.Notify("textDocument/didChange", new
            {
                textDocument = new { uri = projectUri, version = 2 }, contentChanges = new[] { new { text = invalidConfiguration } }
            });
            IReadOnlyList<JsonElement> invalidProject = await session.CollectDiagnosticPublications(projectUri, 2);
            invalidProject
                .Should()
                .ContainSingle()
                .Which
                .GetArrayLength()
                .Should()
                .BeGreaterThan(0);

            await session.Notify("textDocument/didChange", new
            {
                textDocument = new { uri = projectUri, version = 3 }, contentChanges = new[] { new { text = configuration } }
            });
            IReadOnlyList<JsonElement> restoredProject = await session.CollectDiagnosticPublications(projectUri, 3);
            restoredProject
                .Should()
                .ContainSingle()
                .Which
                .GetArrayLength()
                .Should()
                .Be(0);

            Directory.Exists(Path.Combine(directory, "bin"))
                .Should()
                .BeFalse("diagnostic analysis must not invoke native compilation");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static Task<JsonElement> Inspect(ServerSession session, string uri, int version) => session.Request("sourceInspection/inspectFunction",
        new { sourceUri = uri, sourceVersion = version, position = new { line = 1, character = 25 }, stage = "structuredIR" });

    private static string ExtensionUri(Uri uri)
        => "file://" + uri.AbsoluteUri["file://".Length..].Replace(":", "%3A", StringComparison.Ordinal);

    private static bool HasClassToken(JsonElement response, string[] legend, string source, string name)
    {
        int[] data = [.. response.GetProperty("data").EnumerateArray().Select(item => item.GetInt32())];
        string[] lines = source.Split('\n');
        int line = 0;
        int character = 0;
        for (int i = 0; i < data.Length; i += 5)
        {
            line += data[i];
            character = data[i] == 0 ? character + data[i + 1] : data[i + 1];
            if (legend[data[i + 3]] == "class" && lines[line].Substring(character, data[i + 2]) == name)
            {
                return true;
            }
        }

        return false;
    }

    private sealed class ServerSession : IAsyncDisposable
    {
        private static readonly JsonSerializerOptions serializerOptions = new()
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        private readonly Process process;
        private readonly Task<string> errors;
        private readonly List<JsonElement> notifications = [];
        private readonly CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
        private Task<JsonElement>? pendingRead;
        private int nextId;

        public ServerSession(string directory)
        {
            string host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")
                ?? Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..", OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
            this.process = new Process { StartInfo = new ProcessStartInfo
            {
                FileName = host, WorkingDirectory = directory, UseShellExecute = false,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
            } };
            this.process.StartInfo.ArgumentList.Add(typeof(WorkspaceOrchestrator).Assembly.Location);
            this.process.StartInfo.ArgumentList.Add("-lsp");
            this.process.StartInfo.ArgumentList.Add("-stdio");
            this.process.Start();
            this.errors = this.process.StandardError.ReadToEndAsync(this.timeout.Token);
        }

        public Task Notify(string method, object parameters) => this.Send(method, parameters, null);

        public async Task<JsonElement> Request(string method, object? parameters)
        {
            int id = ++this.nextId;
            await this.Send(method, parameters, id);
            while (true)
            {
                JsonElement message = await this.ReadNextMessage();
                if (message.TryGetProperty("id", out JsonElement responseId) && responseId.GetInt32() == id)
                {
                    if (message.TryGetProperty("error", out JsonElement error))
                    {
                        throw new InvalidOperationException(error.ToString());
                    }

                    return message.GetProperty("result").Clone();
                }

                this.notifications.Add(message);
            }
        }

        public async Task WaitForDiagnostics(int version)
        {
            while (!this.notifications.Any(message => message.TryGetProperty("method", out JsonElement method)
                && method.GetString() == "textDocument/publishDiagnostics"
                && message.GetProperty("params").TryGetProperty("version", out JsonElement actual) && actual.ValueKind == JsonValueKind.Number
                && actual.GetInt32() == version && message.GetProperty("params").GetProperty("diagnostics").GetArrayLength() > 0))
            {
                this.notifications.Add(await this.ReadNextMessage());
            }
        }

        public async Task<IReadOnlyList<JsonElement>> CollectDiagnosticPublications(string uri, int? version)
        {
            ArgumentNullException.ThrowIfNull(uri);
            List<JsonElement> publications = [];
            while (true)
            {
                int match = this.notifications.FindIndex(message => IsDiagnosticPublication(message, uri, version));
                if (match < 0)
                {
                    break;
                }

                publications.Add(this.notifications[match].GetProperty("params").GetProperty("diagnostics").Clone());
                this.notifications.RemoveAt(match);
            }

            while (publications.Count == 0)
            {
                JsonElement message = await this.ReadNextMessage();
                if (IsDiagnosticPublication(message, uri, version))
                {
                    publications.Add(message.GetProperty("params").GetProperty("diagnostics").Clone());
                }
                else
                {
                    this.notifications.Add(message);
                }
            }

            while (true)
            {
                Task<JsonElement> next = this.pendingRead ??= this.ReadMessage();
                Task quietPeriod = Task.Delay(TimeSpan.FromMilliseconds(150), this.timeout.Token);
                if (await Task.WhenAny(next, quietPeriod) != next)
                {
                    return publications;
                }

                this.pendingRead = null;
                JsonElement message = await next;
                if (IsDiagnosticPublication(message, uri, version))
                {
                    publications.Add(message.GetProperty("params").GetProperty("diagnostics").Clone());
                }
                else
                {
                    this.notifications.Add(message);
                }
            }
        }

        public async Task<JsonElement> WaitForDiagnostics(string uri, int version, Func<JsonElement, bool> predicate)
        {
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(predicate);
            while (true)
            {
                int match = this.notifications.FindIndex(message => message.TryGetProperty("method", out JsonElement method)
                    && method.GetString() == "textDocument/publishDiagnostics"
                    && DocumentUri.Parse(message.GetProperty("params").GetProperty("uri").GetString()!).ToUri()
                        .IsSamePath(DocumentUri.Parse(uri).ToUri())
                    && message.GetProperty("params").TryGetProperty("version", out JsonElement actual)
                    && actual.ValueKind == JsonValueKind.Number && actual.GetInt32() == version);
                if (match >= 0)
                {
                    JsonElement diagnostics = this.notifications[match].GetProperty("params").GetProperty("diagnostics").Clone();
                    this.notifications.RemoveAt(match);
                    if (predicate(diagnostics))
                    {
                        return diagnostics;
                    }

                    continue;
                }

                this.notifications.Add(await this.ReadNextMessage());
            }
        }

        private static bool IsDiagnosticPublication(JsonElement message, string uri, int? version)
        {
            if (!message.TryGetProperty("method", out JsonElement method) || method.GetString() != "textDocument/publishDiagnostics")
            {
                return false;
            }

            JsonElement parameters = message.GetProperty("params");
            if (!DocumentUri.Parse(parameters.GetProperty("uri").GetString()!).ToUri()
                .IsSamePath(DocumentUri.Parse(uri).ToUri()))
            {
                return false;
            }

            bool hasVersion = parameters.TryGetProperty("version", out JsonElement actual);
            return version is int expected
                ? hasVersion && actual.ValueKind == JsonValueKind.Number && actual.GetInt32() == expected
                : !hasVersion || actual.ValueKind == JsonValueKind.Null;
        }

        private async Task<JsonElement> ReadNextMessage()
        {
            Task<JsonElement> read = this.pendingRead ??= this.ReadMessage();
            try
            {
                return await read;
            }
            finally
            {
                if (ReferenceEquals(this.pendingRead, read))
                {
                    this.pendingRead = null;
                }
            }
        }

        private async Task Send(string method, object? parameters, int? id)
        {
            byte[] body = JsonSerializer.SerializeToUtf8Bytes(new { jsonrpc = "2.0", id, method, @params = parameters },
                serializerOptions);
            byte[] header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length.ToString(CultureInfo.InvariantCulture)}\r\n\r\n");
            await this.process.StandardInput.BaseStream.WriteAsync(header, this.timeout.Token);
            await this.process.StandardInput.BaseStream.WriteAsync(body, this.timeout.Token);
            await this.process.StandardInput.BaseStream.FlushAsync(this.timeout.Token);
        }

        private async Task<JsonElement> ReadMessage()
        {
            Stream input = this.process.StandardOutput.BaseStream;
            StringBuilder header = new();
            byte[] one = new byte[1];
            while (!header.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
            {
                int count = await input.ReadAsync(one, this.timeout.Token);
                if (count == 0)
                {
#pragma warning disable VSTHRD003 // This concurrent child-process stderr drain has no dependency on the test synchronization context.
                    throw new InvalidOperationException("LSP server exited: " + await this.errors);
#pragma warning restore VSTHRD003
                }

                header.Append((char)one[0]);
            }

            string length = header.ToString().Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
                .Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)).Split(':')[1];
            byte[] body = new byte[int.Parse(length, CultureInfo.InvariantCulture)];
            await input.ReadExactlyAsync(body, this.timeout.Token);
            using JsonDocument document = JsonDocument.Parse(body);
            return document.RootElement.Clone();
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (!this.process.HasExited)
                {
                    await this.Request("shutdown", null);
                    await this.Notify("exit", new { });
                    await this.process.WaitForExitAsync(this.timeout.Token);
                }
            }
            finally
            {
                if (!this.process.HasExited)
                {
                    this.process.Kill(entireProcessTree: true);
                    await this.process.WaitForExitAsync();
                }

                this.process.Dispose();
                this.timeout.Dispose();
            }
        }
    }
}

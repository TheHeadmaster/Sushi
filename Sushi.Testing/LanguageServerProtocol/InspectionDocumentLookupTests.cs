using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Newtonsoft.Json;
using NUnit.Framework;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
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
    private static readonly string[] hoverContentFormats = ["markdown"];
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

    [TestCaseSource(nameof(DocumentAttributeCases))]
    public void TextDocumentMatchingShouldClassifyOnlyManagedSushiFileTypes(DocumentUri documentUri, string languageId)
    {
        using WorkspaceOrchestrator workspace = new();
        TextDocumentSyncHandler handler = new(workspace);
        TextDocumentAttributes attributes = handler.GetTextDocumentAttributes(documentUri);

        attributes.LanguageId
            .Should()
            .Be(languageId);
    }

    private static IEnumerable<TestCaseData> DocumentAttributeCases()
    {
        yield return new TestCaseData(DocumentUri.Parse("file:///tmp/Source.sus"), "sushi");
        yield return new TestCaseData(DocumentUri.Parse("file:///tmp/Project.susproj"), "susproj");
        yield return new TestCaseData(DocumentUri.Parse("file:///tmp/Solution.susln"), "susln");
        yield return new TestCaseData(DocumentUri.Parse("sushi-source-snapshot:/snapshot/Source.sus%20(Historical)"), "plaintext");
        yield return new TestCaseData(DocumentUri.Parse("untitled:Untitled-1"), "plaintext");
        yield return new TestCaseData(DocumentUri.Parse("file:///tmp/Notes.txt"), "plaintext");
    }

    [Test]
    public void SemanticTokenRegistrationShouldRequireSushiFileDocuments()
    {
        using WorkspaceOrchestrator workspace = new();
        SushiSemanticTokensHandler handler = new(workspace);
        MethodInfo createOptions = typeof(SushiSemanticTokensHandler).GetMethod("CreateRegistrationOptions", BindingFlags.Instance | BindingFlags.NonPublic)!;
        SemanticTokensRegistrationOptions options = (SemanticTokensRegistrationOptions)createOptions.Invoke(handler, [null!, null!])!;
        string selector = JsonConvert.SerializeObject(options.DocumentSelector);

        selector
            .Should()
            .Contain("\"Language\":\"sushi\"")
            .And
            .Contain("\"Scheme\":\"file\"")
            .And
            .Contain("**/*.sus")
            .And
            .NotContain("sushi-source-snapshot");
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
            await session.Notify("textDocument/didOpen", new
            {
                textDocument = new
                {
                    uri = "sushi-source-snapshot:/snapshot/Source.sus%20(Historical)",
                    languageId = "sushi",
                    version = 1,
                    text = Source
                }
            });
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
            string caller = "package Example; namespace Example;\npublic int32 Caller(int32 value) { int32 result = Helper(value); return result; }";
            string callee = "package Example; namespace Example;\r\n// 🍣\r\npublic int32 Helper(int32 input) { return input + 1; }";
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

            int selectionCharacter = caller.Split('\n')[1].IndexOf("Caller", StringComparison.Ordinal) + 10;
            JsonElement structured = await session.Request("sourceInspection/inspectFunction", new
            {
                sourceUri = callerUri, sourceVersion = 7,
                position = new { line = 1, character = selectionCharacter }, stage = "structuredIR"
            });
            structured.GetProperty("succeeded").GetBoolean()
                .Should()
                .BeTrue(structured.ToString());
            structured.GetProperty("text").GetString()
                .Should()
                .Contain("function int32 __sushi_package_function_0");

            JsonElement inspected = await session.Request("sourceInspection/inspectFunction", new
            {
                sourceUri = callerUri, sourceVersion = 7,
                position = new { line = 1, character = selectionCharacter }, stage = "loweredIR"
            });
            inspected.GetProperty("succeeded").GetBoolean()
                .Should()
                .BeTrue(inspected.ToString());
            inspected.GetProperty("text").GetString()
                .Should()
                .Contain("entry:");
            JsonElement structuredAgain = await session.Request("sourceInspection/inspectFunction", new
            {
                sourceUri = callerUri, sourceVersion = 7,
                position = new { line = 1, character = selectionCharacter }, stage = "structuredIR"
            });
            structuredAgain.GetProperty("text").GetString()
                .Should()
                .Be(structured.GetProperty("text").GetString());
            structuredAgain.GetProperty("documentUri").GetString()
                .Should()
                .Be(structured.GetProperty("documentUri").GetString());
            structuredAgain.GetProperty("artifactId").GetString()
                .Should()
                .Be(structured.GetProperty("artifactId").GetString());

            string structuredText = structured.GetProperty("text").GetString()!;
            (int functionLine, int functionCharacter) = GetGeneratedPosition(structuredText, "__sushi_package_function_1");
            JsonElement functionHover = await session.Request("sourceInspection/hover", new
            {
                documentUri = structured.GetProperty("documentUri").GetString(),
                artifactId = structured.GetProperty("artifactId").GetString(),
                position = new { line = functionLine, character = functionCharacter }
            });
            functionHover.GetProperty("found").GetBoolean()
                .Should()
                .BeTrue(functionHover.ToString());
            functionHover.GetProperty("signature").GetString()
                .Should()
                .Be("public int32 Helper(int32 input)");
            functionHover.GetProperty("range").GetProperty("start").GetProperty("line").GetInt32()
                .Should()
                .Be(functionLine);

            JsonElement functionSource = await session.Request("sourceInspection/sourceDefinition", new
            {
                documentUri = structured.GetProperty("documentUri").GetString(),
                artifactId = structured.GetProperty("artifactId").GetString(),
                position = new { line = functionLine, character = functionCharacter }
            });
            functionSource.GetProperty("found").GetBoolean()
                .Should()
                .BeTrue(functionSource.ToString());
            functionSource.GetProperty("sourceUri").GetString()
                .Should()
                .Be(DocumentUri.FromFileSystemPath(calleePath).ToString());
            functionSource.GetProperty("text").GetString()
                .Should()
                .Be(unsavedCallee);
            AssertSourceTokenRange(functionSource, unsavedCallee, "Helper");

            (int structuredFunctionLine, int structuredFunctionCharacter) = GetGeneratedPosition(structuredText, "__sushi_package_function_0");
            JsonElement structuredDeclarationSource = await session.Request("sourceInspection/sourceDefinition", new
            {
                documentUri = structured.GetProperty("documentUri").GetString(),
                artifactId = structured.GetProperty("artifactId").GetString(),
                position = new { line = structuredFunctionLine, character = structuredFunctionCharacter }
            });
            structuredDeclarationSource.GetProperty("sourceUri").GetString()
                .Should()
                .Be(DocumentUri.FromFileSystemPath(callerPath).ToString());

            (int callerParameterLine, int callerParameterCharacter) = GetGeneratedPosition(structuredText, "value");
            JsonElement callerParameterSource = await session.Request("sourceInspection/sourceDefinition", new
            {
                documentUri = structured.GetProperty("documentUri").GetString(),
                artifactId = structured.GetProperty("artifactId").GetString(),
                position = new { line = callerParameterLine, character = callerParameterCharacter }
            });
            callerParameterSource.GetProperty("sourceUri").GetString()
                .Should()
                .Be(DocumentUri.FromFileSystemPath(callerPath).ToString());

            IReadOnlyList<(int Line, int Character)> structuredLocalPositions = GetGeneratedPositions(structuredText, "%local0");
            structuredLocalPositions
                .Should()
                .NotBeEmpty();
            foreach ((int localLine, int localCharacter) in structuredLocalPositions)
            {
                JsonElement localSource = await session.Request("sourceInspection/sourceDefinition", new
                {
                    documentUri = structured.GetProperty("documentUri").GetString(),
                    artifactId = structured.GetProperty("artifactId").GetString(),
                    position = new { line = localLine, character = localCharacter }
                });
                localSource.GetProperty("found").GetBoolean()
                    .Should()
                    .BeTrue(localSource.ToString());
                localSource.GetProperty("sourceUri").GetString()
                    .Should()
                    .Be(DocumentUri.FromFileSystemPath(callerPath).ToString());
                AssertSourceTokenRange(localSource, caller, "result");
            }

            (int parameterLine, int parameterCharacter) = GetGeneratedPosition(structuredText, "arg0");
            JsonElement parameterHover = await session.Request("sourceInspection/hover", new
            {
                documentUri = structured.GetProperty("documentUri").GetString(),
                artifactId = structured.GetProperty("artifactId").GetString(),
                position = new { line = parameterLine, character = parameterCharacter }
            });
            parameterHover.GetProperty("found").GetBoolean()
                .Should()
                .BeTrue(parameterHover.ToString());
            parameterHover.GetProperty("details").GetString()
                .Should()
                .Contain("Parameter 0: input")
                .And
                .Contain("Function: Helper");

            JsonElement parameterSource = await session.Request("sourceInspection/sourceDefinition", new
            {
                documentUri = structured.GetProperty("documentUri").GetString(),
                artifactId = structured.GetProperty("artifactId").GetString(),
                position = new { line = parameterLine, character = parameterCharacter }
            });
            parameterSource.GetProperty("found").GetBoolean()
                .Should()
                .BeTrue(parameterSource.ToString());
            parameterSource.GetProperty("sourceUri").GetString()
                .Should()
                .Be(DocumentUri.FromFileSystemPath(calleePath).ToString());
            AssertSourceTokenRange(parameterSource, unsavedCallee, "input");

            (int loweredFunctionLine, int loweredFunctionCharacter) = GetGeneratedPosition(inspected.GetProperty("text").GetString()!, "__sushi_package_function_0");
            JsonElement loweredFunctionSource = await session.Request("sourceInspection/sourceDefinition", new
            {
                documentUri = inspected.GetProperty("documentUri").GetString(),
                artifactId = inspected.GetProperty("artifactId").GetString(),
                position = new { line = loweredFunctionLine, character = loweredFunctionCharacter }
            });
            loweredFunctionSource.GetProperty("sourceUri").GetString()
                .Should()
                .Be(DocumentUri.FromFileSystemPath(callerPath).ToString());

            JsonElement forgedSource = await session.Request("sourceInspection/sourceDefinition", new
            {
                documentUri = "sushi-inspect://other/forged/Caller.structuredIR",
                artifactId = structured.GetProperty("artifactId").GetString(),
                position = new { line = functionLine, character = functionCharacter }
            });
            forgedSource.GetProperty("found").GetBoolean()
                .Should()
                .BeFalse();

            string loweredText = inspected.GetProperty("text").GetString()!;
            foreach ((string symbolText, string expectedDetail) in new[]
            {
                ("%local0", "Lowered IR local slot: %local0"),
                ("%temp0", "SSA temporary: %temp0"),
                ("entry", "Basic block: entry")
            })
            {
                (int hoverLine, int hoverCharacter) = GetGeneratedPosition(loweredText, symbolText);
                JsonElement hover = await session.Request("sourceInspection/hover", new
                {
                    documentUri = inspected.GetProperty("documentUri").GetString(),
                    artifactId = inspected.GetProperty("artifactId").GetString(),
                    position = new { line = hoverLine, character = hoverCharacter }
                });
                hover.GetProperty("details").GetString()
                    .Should()
                    .Contain(expectedDetail);
            }

            IReadOnlyList<(int Line, int Character)> loweredLocalPositions = GetGeneratedPositions(loweredText, "%local0");
            loweredLocalPositions
                .Should()
                .NotBeEmpty();
            foreach ((int localLine, int localCharacter) in loweredLocalPositions)
            {
                JsonElement localSource = await session.Request("sourceInspection/sourceDefinition", new
                {
                    documentUri = inspected.GetProperty("documentUri").GetString(),
                    artifactId = inspected.GetProperty("artifactId").GetString(),
                    position = new { line = localLine, character = localCharacter }
                });
                localSource.GetProperty("found").GetBoolean()
                    .Should()
                    .BeTrue(localSource.ToString());
                AssertSourceTokenRange(localSource, caller, "result");
            }

            foreach (string symbolText in new[] { "%temp0", "entry" })
            {
                (int unsupportedLine, int unsupportedCharacter) = GetGeneratedPosition(loweredText, symbolText);
                JsonElement sourceResult = await session.Request("sourceInspection/sourceDefinition", new
                {
                    documentUri = inspected.GetProperty("documentUri").GetString(),
                    artifactId = inspected.GetProperty("artifactId").GetString(),
                    position = new { line = unsupportedLine, character = unsupportedCharacter }
                });
                sourceResult.GetProperty("found").GetBoolean()
                    .Should()
                    .BeFalse();
            }

            JsonElement noHover = await session.Request("sourceInspection/hover", new
            {
                documentUri = structured.GetProperty("documentUri").GetString(),
                artifactId = structured.GetProperty("artifactId").GetString(),
                position = new { line = 1, character = 0 }
            });
            noHover.GetProperty("found").GetBoolean()
                .Should()
                .BeFalse();
            JsonElement forgedHover = await session.Request("sourceInspection/hover", new
            {
                documentUri = "sushi-inspect://other/forged/Caller.structuredIR",
                artifactId = structured.GetProperty("artifactId").GetString(),
                position = new { line = functionLine, character = functionCharacter }
            });
            forgedHover.GetProperty("found").GetBoolean()
                .Should()
                .BeFalse();

            string changedCaller = caller.Replace("Helper(value)", "Helper(value + 1)", StringComparison.Ordinal);
            await session.Notify("textDocument/didChange", new
            {
                textDocument = new { uri = callerUri, version = 8 }, contentChanges = new[] { new { text = changedCaller } }
            });
            JsonElement changedInspection = await session.Request("sourceInspection/inspectFunction", new
            {
                sourceUri = callerUri, sourceVersion = 8,
                position = new { line = 1, character = changedCaller.Split('\n')[1].IndexOf("Caller", StringComparison.Ordinal) + 10 }, stage = "loweredIR"
            });
            changedInspection.GetProperty("text").GetString()
                .Should()
                .Contain("checked +");
            inspected.GetProperty("text").GetString()
                .Should()
                .NotContain("checked +");
            JsonElement historicalLocal = await session.Request("sourceInspection/sourceDefinition", new
            {
                documentUri = structured.GetProperty("documentUri").GetString(),
                artifactId = structured.GetProperty("artifactId").GetString(),
                position = new { line = structuredLocalPositions[0].Line, character = structuredLocalPositions[0].Character }
            });
            historicalLocal.GetProperty("text").GetString()
                .Should()
                .Be(caller);
            AssertSourceTokenRange(historicalLocal, caller, "result");
            string text = inspected.GetProperty("text").GetString()!;
            int offset = text.IndexOf("arg0", StringComparison.Ordinal);
            offset
                .Should()
                .BeGreaterThanOrEqualTo(0);
            int line = text[..offset].Count(character => character == '\n');
            int character = offset - (text.LastIndexOf('\n', Math.Max(0, offset - 1)) + 1);

            string changedCallee = unsavedCallee.Replace("int32 input", "int32 renamed", StringComparison.Ordinal)
                .Replace("return input + 2", "return renamed + 99", StringComparison.Ordinal);
            await session.Notify("textDocument/didChange", new
            {
                textDocument = new { uri = calleeUri, version = 5 }, contentChanges = new[] { new { text = changedCallee } }
            });

            JsonElement currentInspection = await session.Request("sourceInspection/inspectFunction", new
            {
                sourceUri = callerUri, sourceVersion = 8,
                position = new { line = 1, character = changedCaller.Split('\n')[1].IndexOf("Caller", StringComparison.Ordinal) + 10 }, stage = "structuredIR"
            });
            (int currentArgumentLine, int currentArgumentCharacter) = GetGeneratedPosition(currentInspection.GetProperty("text").GetString()!, "arg0");
            JsonElement currentParameterHover = await session.Request("sourceInspection/hover", new
            {
                documentUri = currentInspection.GetProperty("documentUri").GetString(),
                artifactId = currentInspection.GetProperty("artifactId").GetString(),
                position = new { line = currentArgumentLine, character = currentArgumentCharacter }
            });
            currentParameterHover.GetProperty("details").GetString()
                .Should()
                .Contain("Parameter 0: renamed");

            JsonElement historicalParameter = await session.Request("sourceInspection/sourceDefinition", new
            {
                documentUri = structured.GetProperty("documentUri").GetString(),
                artifactId = structured.GetProperty("artifactId").GetString(),
                position = new { line = parameterLine, character = parameterCharacter }
            });
            historicalParameter.GetProperty("text").GetString()
                .Should()
                .Be(unsavedCallee);
            AssertSourceTokenRange(historicalParameter, unsavedCallee, "input");

            JsonElement currentParameter = await session.Request("sourceInspection/sourceDefinition", new
            {
                documentUri = currentInspection.GetProperty("documentUri").GetString(),
                artifactId = currentInspection.GetProperty("artifactId").GetString(),
                position = new { line = currentArgumentLine, character = currentArgumentCharacter }
            });
            currentParameter.GetProperty("text").GetString()
                .Should()
                .Be(changedCallee);
            AssertSourceTokenRange(currentParameter, changedCallee, "renamed");

            string shiftedCaller = "// prefix\n" + changedCaller;
            await session.Notify("textDocument/didChange", new
            {
                textDocument = new { uri = callerUri, version = 9 }, contentChanges = new[] { new { text = shiftedCaller } }
            });
            JsonElement shiftedInspection = await session.Request("sourceInspection/inspectFunction", new
            {
                sourceUri = callerUri, sourceVersion = 9,
                position = new { line = 2, character = shiftedCaller.Split('\n')[2].IndexOf("Caller", StringComparison.Ordinal) + 10 }, stage = "structuredIR"
            });
            (int shiftedFunctionLine, int shiftedFunctionCharacter) = GetGeneratedPosition(shiftedInspection.GetProperty("text").GetString()!, "__sushi_package_function_0");
            JsonElement shiftedSource = await session.Request("sourceInspection/sourceDefinition", new
            {
                documentUri = shiftedInspection.GetProperty("documentUri").GetString(),
                artifactId = shiftedInspection.GetProperty("artifactId").GetString(),
                position = new { line = shiftedFunctionLine, character = shiftedFunctionCharacter }
            });
            shiftedSource.GetProperty("text").GetString()
                .Should()
                .Be(shiftedCaller);
            AssertSourceTokenRange(shiftedSource, shiftedCaller, "Caller");

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
                .NotContain("99")
                .And
                .NotContain("renamed");
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
            await session.Notify("sourceInspection/release", new { artifactId = structured.GetProperty("artifactId").GetString() });
            await session.Notify("sourceInspection/release", new { artifactId = structuredAgain.GetProperty("artifactId").GetString() });
            await session.Notify("sourceInspection/release", new { artifactId = changedInspection.GetProperty("artifactId").GetString() });
            await session.Notify("sourceInspection/release", new { artifactId = currentInspection.GetProperty("artifactId").GetString() });
            await session.Notify("sourceInspection/release", new { artifactId = shiftedInspection.GetProperty("artifactId").GetString() });

            JsonElement releasedHover = await session.Request("sourceInspection/hover", new
            {
                documentUri = structured.GetProperty("documentUri").GetString(),
                artifactId = structured.GetProperty("artifactId").GetString(),
                position = new { line = functionLine, character = functionCharacter }
            });
            releasedHover.GetProperty("found").GetBoolean()
                .Should()
                .BeFalse();
            JsonElement releasedSource = await session.Request("sourceInspection/sourceDefinition", new
            {
                documentUri = structured.GetProperty("documentUri").GetString(),
                artifactId = structured.GetProperty("artifactId").GetString(),
                position = new { line = functionLine, character = functionCharacter }
            });
            releasedSource.GetProperty("found").GetBoolean()
                .Should()
                .BeFalse();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static (int Line, int Character) GetGeneratedPosition(string text, string value)
    {
        int offset = text.IndexOf(value, StringComparison.Ordinal);
        offset
            .Should()
            .BeGreaterThanOrEqualTo(0);
        int line = text[..offset].Count(character => character == '\n');
        int character = offset - (text.LastIndexOf('\n', Math.Max(0, offset - 1)) + 1);
        return (line, character);
    }

    private static IReadOnlyList<(int Line, int Character)> GetGeneratedPositions(string text, string value)
    {
        List<(int Line, int Character)> positions = [];
        int searchStart = 0;
        while (searchStart < text.Length)
        {
            int offset = text.IndexOf(value, searchStart, StringComparison.Ordinal);
            if (offset < 0)
            {
                break;
            }

            int line = text[..offset].Count(character => character == '\n');
            int character = offset - (text.LastIndexOf('\n', Math.Max(0, offset - 1)) + 1);
            positions.Add((line, character));
            searchStart = offset + value.Length;
        }

        return positions;
    }

    private static (int Line, int Character) GetSourcePositionAtOffset(string text, int offset)
    {
        offset.Should().BeInRange(0, text.Length);
        int line = 0;
        int lineStart = 0;
        for (int index = 0; index < offset; index++)
        {
            if (text[index] == '\r')
            {
                line++;
                if (index + 1 < offset && text[index + 1] == '\n')
                {
                    index++;
                }

                lineStart = index + 1;
            }
            else if (text[index] == '\n')
            {
                line++;
                lineStart = index + 1;
            }
        }

        return (line, offset - lineStart);
    }

    private static (int Line, int Character) GetSourcePosition(string text, string token)
    {
        int offset = text.IndexOf(token, StringComparison.Ordinal);
        offset
            .Should()
            .BeGreaterThanOrEqualTo(0);
        int line = text[..offset].Count(character => character == '\n');
        int character = offset - (text.LastIndexOf('\n', Math.Max(0, offset - 1)) + 1);
        return (line, character);
    }

    private static void AssertSourceTokenRange(JsonElement response, string sourceText, string token)
    {
        (int line, int character) = GetSourcePosition(sourceText, token);
        JsonElement range = response.GetProperty("sourceRange");
        range.GetProperty("start").GetProperty("line").GetInt32()
            .Should()
            .Be(line);
        range.GetProperty("start").GetProperty("character").GetInt32()
            .Should()
            .Be(character);
        range.GetProperty("end").GetProperty("line").GetInt32()
            .Should()
            .Be(line);
        range.GetProperty("end").GetProperty("character").GetInt32()
            .Should()
            .Be(character + token.Length);
    }

    [TestCase(TestName = "Initialized LSP Reuses A Caller Representation Without Replacing Its Compilation Snapshot")]
    public async Task InitializedServerShouldKeepDependencyCompatibleCallerSnapshotsIndependent()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "Cache Navigation " + Guid.NewGuid().ToString("N"));
        string sourcesDirectory = Path.Combine(directory, "Sources");
        Directory.CreateDirectory(sourcesDirectory);
        try
        {
            string configuration = """
                name = "CacheNavigation"
                assembly = "CacheNavigation"
                language-version = "1.0"
                [build]
                default = "debug"
                sources = ["Sources/**/*.sus"]
                [build.targets.debug]
                type = "Sushi.MVP.Debug"
                """;
            string caller = "package Example; namespace Example;\npublic int32 Caller(int32 value) { return Helper(value); }";
            string callee = "package Example; namespace Example;\npublic int32 Helper(int32 input) { return input + 1; }";
            string projectPath = Path.Combine(directory, "CacheNavigation.susproj");
            string callerPath = Path.Combine(sourcesDirectory, "A.sus");
            string calleePath = Path.Combine(sourcesDirectory, "B.sus");
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
                workspaceFolders = new[] { new { uri = ExtensionUri(new Uri(directory)), name = "CacheNavigation" } }
            });
            await session.Notify("initialized", new { });
            await session.Notify("textDocument/didOpen", new { textDocument = new { uri = callerUri, languageId = "sushi", version = 1, text = caller } });
            await session.Notify("textDocument/didOpen", new { textDocument = new { uri = calleeUri, languageId = "sushi", version = 1, text = callee } });

            JsonElement original = await InspectFunction(session, callerUri, caller, "Caller", "structuredIR");
            string changedCallee = callee.Replace("input + 1", "input + 10", StringComparison.Ordinal);
            await session.Notify("textDocument/didChange", new
            {
                textDocument = new { uri = calleeUri, version = 2 }, contentChanges = new[] { new { text = changedCallee } }
            });
            JsonElement current = await InspectFunction(session, callerUri, caller, "Caller", "structuredIR");

            original.GetProperty("succeeded").GetBoolean()
                .Should()
                .BeTrue(original.ToString());
            current.GetProperty("succeeded").GetBoolean()
                .Should()
                .BeTrue(current.ToString());
            current.GetProperty("text").GetString()
                .Should()
                .Be(original.GetProperty("text").GetString());
            current.GetProperty("documentUri").GetString()
                .Should()
                .NotBe(original.GetProperty("documentUri").GetString());

            JsonElement historicalTarget = await NavigateToCallee(session, original);
            JsonElement currentTarget = await NavigateToCallee(session, current);
            historicalTarget.GetProperty("text").GetString()
                .Should()
                .Contain("input + 1")
                .And
                .NotContain("input + 10");
            currentTarget.GetProperty("text").GetString()
                .Should()
                .Contain("input + 10");
            await session.Notify("sourceInspection/release", new { artifactId = historicalTarget.GetProperty("targetArtifactId").GetString() });
            await session.Notify("sourceInspection/release", new { artifactId = currentTarget.GetProperty("targetArtifactId").GetString() });
            await session.Notify("sourceInspection/release", new { artifactId = original.GetProperty("artifactId").GetString() });
            await session.Notify("sourceInspection/release", new { artifactId = current.GetProperty("artifactId").GetString() });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestCase(TestName = "Initialized LSP Resolves References Only Within The Originating Structured Or Lowered Artifact")]
    public async Task InitializedServerShouldResolveInspectionReferencesWithinTheRequestedArtifact()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "Inspection References " + Guid.NewGuid().ToString("N"));
        string sourcesDirectory = Path.Combine(directory, "Sources");
        Directory.CreateDirectory(sourcesDirectory);
        try
        {
            string configuration = """
                name = "InspectionReferences"
                assembly = "InspectionReferences"
                language-version = "1.0"
                [build]
                default = "debug"
                sources = ["Sources/**/*.sus"]
                [build.targets.debug]
                type = "Sushi.MVP.Debug"
                """;
            string source = "package Example; namespace Example;\npublic int32 Calculate(int32 input) { int32 local = input; local = local + 1; if (local > 2) { local = local + input; } return Helper(local) + Helper(input); }\npublic int32 Helper(int32 value) { return value; }";
            string projectPath = Path.Combine(directory, "InspectionReferences.susproj");
            string sourcePath = Path.Combine(sourcesDirectory, "References.sus");
            await File.WriteAllTextAsync(projectPath, configuration);
            await File.WriteAllTextAsync(sourcePath, source);
            string sourceUri = ExtensionUri(new Uri(sourcePath));
            await using ServerSession session = new(directory);
            await session.Request("initialize", new
            {
                processId = (int?)null,
                capabilities = new { textDocument = new { } },
                workspaceFolders = new[] { new { uri = ExtensionUri(new Uri(directory)), name = "InspectionReferences" } }
            });
            await session.Notify("initialized", new { });
            await session.Notify("textDocument/didOpen", new { textDocument = new { uri = sourceUri, languageId = "sushi", version = 1, text = source } });

            foreach (string stage in new[] { "structuredIR", "loweredIR" })
            {
                JsonElement inspection = await InspectFunction(session, sourceUri, source, "Calculate", stage);
                inspection.GetProperty("succeeded").GetBoolean()
                    .Should()
                    .BeTrue(inspection.ToString());
                string text = inspection.GetProperty("text").GetString()!;
                IReadOnlyList<(int Line, int Character)> localPositions = GetGeneratedPositions(text, "%local0");
                localPositions
                    .Should()
                    .NotBeEmpty();
                (int localLine, int localCharacter) = localPositions[^1];
                object request = new
                {
                    documentUri = inspection.GetProperty("documentUri").GetString(),
                    artifactId = inspection.GetProperty("artifactId").GetString(),
                    position = new { line = localLine, character = localCharacter },
                    includeDeclaration = true
                };
                JsonElement withDeclaration = await session.Request("sourceInspection/references", request);
                JsonElement[] localRanges = [.. withDeclaration.GetProperty("ranges").EnumerateArray()];
                localRanges
                    .Should()
                    .HaveCount(localPositions.Count);
                JsonElement repeated = await session.Request("sourceInspection/references", request);
                repeated.GetRawText()
                    .Should()
                    .Be(withDeclaration.GetRawText());

                JsonElement withoutDeclaration = await session.Request("sourceInspection/references", new
                {
                    documentUri = inspection.GetProperty("documentUri").GetString(),
                    artifactId = inspection.GetProperty("artifactId").GetString(),
                    position = new { line = localLine, character = localCharacter },
                    includeDeclaration = false
                });
                withoutDeclaration.GetProperty("ranges").GetArrayLength()
                    .Should()
                    .Be(localPositions.Count - 1);

                (int callLine, int callCharacter) = GetGeneratedPosition(text, "__sushi_package_function_1");
                JsonElement calls = await session.Request("sourceInspection/references", new
                {
                    documentUri = inspection.GetProperty("documentUri").GetString(),
                    artifactId = inspection.GetProperty("artifactId").GetString(),
                    position = new { line = callLine, character = callCharacter },
                    includeDeclaration = false
                });
                calls.GetProperty("ranges").GetArrayLength()
                    .Should()
                    .Be(2);

                JsonElement noSymbol = await session.Request("sourceInspection/references", new
                {
                    documentUri = inspection.GetProperty("documentUri").GetString(),
                    artifactId = inspection.GetProperty("artifactId").GetString(),
                    position = new { line = 0, character = 0 },
                    includeDeclaration = true
                });
                noSymbol.GetProperty("ranges").GetArrayLength()
                    .Should()
                    .Be(0);

                JsonElement forged = await session.Request("sourceInspection/references", new
                {
                    documentUri = "sushi-inspect://another-session/forged/Calculate.structuredIR",
                    artifactId = inspection.GetProperty("artifactId").GetString(),
                    position = new { line = localLine, character = localCharacter },
                    includeDeclaration = true
                });
                forged.GetProperty("error").GetString()
                    .Should()
                    .Contain("no longer available");
                await session.Notify("sourceInspection/release", new { artifactId = inspection.GetProperty("artifactId").GetString() });
                JsonElement released = await session.Request("sourceInspection/references", request);
                released.GetProperty("error").GetString()
                    .Should()
                    .Contain("no longer available");
            }
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

            JsonElement valid = await session.WaitForDiagnostics(callerUri, 7, diagnostics => diagnostics.GetArrayLength() == 1);
            valid.GetArrayLength()
                .Should()
                .Be(1, "the caller has one missing-public-documentation warning while its call remains valid");

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
            JsonElement restored = await session.WaitForDiagnostics(callerUri, 7, diagnostics => diagnostics.GetArrayLength() == 1);
            restored.GetArrayLength()
                .Should()
                .Be(1, "restoring the valid signature should clear the call error and leave the documentation warning");

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
                .Be(1, "the public function receives one missing-documentation warning");

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

    [Test]
    public async Task InitializedServerShouldProvideProjectResolvedSourceHoverWithCurrentDocumentation()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "Source Hover " + Guid.NewGuid().ToString("N"));
        string sourcesDirectory = Path.Combine(directory, "Sources");
        Directory.CreateDirectory(sourcesDirectory);
        try
        {
            string configuration = """
                name = "SourceHover"
                assembly = "SourceHover"
                language-version = "1.0"
                [build]
                default = "debug"
                sources = ["Sources/**/*.sus"]
                [build.targets.debug]
                type = "Sushi.MVP.Debug"
                """;
            string caller = "package Example; namespace Example;\npublic int32 Caller(int32 value) { int32 result = Helper(input: value); return result; }";
            string callee = "package Example; namespace Example;\n/// Adds one to its input. It uses **care** and `precision`.\n/// [guide](../guide.md) [web](https://example.com) [unsafe](JaVaScRiPt:alert(1))\n/// <script>alert(1)</script>\n/// <script\n/// onload=\"alert(1)\">alert(1)</script>\n/// @param input The supplied **value**.\n/// continued over another **careful** line.\n/// @return The incremented **value**.\npublic int32 Helper(int32 input) { return input + 1; }";
            string projectPath = Path.Combine(directory, "SourceHover.susproj");
            string callerPath = Path.Combine(sourcesDirectory, "Caller.sus");
            string calleePath = Path.Combine(sourcesDirectory, "Helper.sus");
            await File.WriteAllTextAsync(projectPath, configuration);
            await File.WriteAllTextAsync(callerPath, caller);
            await File.WriteAllTextAsync(calleePath, callee);
            string callerUri = ExtensionUri(new Uri(callerPath));
            string calleeUri = ExtensionUri(new Uri(calleePath));
            await using ServerSession session = new(directory);
            JsonElement initialization = await session.Request("initialize", new
            {
                processId = (int?)null,
                capabilities = new { textDocument = new { hover = new { contentFormat = hoverContentFormats } } },
                workspaceFolders = new[] { new { uri = ExtensionUri(new Uri(directory)), name = "SourceHover" } }
            });
            initialization.GetProperty("capabilities").TryGetProperty("hoverProvider", out _)
                .Should()
                .BeTrue("ordinary source hover is registered with the language server");
            await session.Notify("initialized", new { });
            await session.Notify("textDocument/didOpen", new { textDocument = new { uri = callerUri, languageId = "sushi", version = 1, text = caller } });
            await session.Notify("textDocument/didOpen", new { textDocument = new { uri = calleeUri, languageId = "sushi", version = 1, text = callee } });

            (int callLine, int callCharacter) = GetSourcePosition(caller, "Helper");
            JsonElement callHover = await session.Request("textDocument/hover", new
            {
                textDocument = new { uri = callerUri },
                position = new { line = callLine, character = callCharacter }
            });
            string callMarkdown = callHover.GetProperty("contents").GetProperty("value").GetString()!;
            callHover.GetProperty("contents").GetProperty("kind").GetString().Should().Be("markdown");
            JsonElement callRange = callHover.GetProperty("range");
            callRange.GetProperty("start").GetProperty("line").GetInt32().Should().Be(callLine);
            callRange.GetProperty("start").GetProperty("character").GetInt32().Should().Be(callCharacter);
            callRange.GetProperty("end").GetProperty("character").GetInt32().Should().Be(callCharacter + "Helper".Length);
            callMarkdown
                .Should()
                .Contain("```sushi")
                .And.Contain("public int32 Helper(int32 input)")
                .And.Contain("Adds one to its input.")
                .And.Contain("The supplied **value**.")
                .And.Contain("**care**")
                .And.Contain("`precision`")
                .And.Contain("[guide](<../guide.md>)")
                .And.Contain("[web](<https://example.com/>)")
                .And.Contain("continued over another **careful** line.")
                .And.Contain("**Returns**")
                .And.Contain("The incremented **value**.")
                .And.NotContain("javascript:")
                .And.NotContain("<a href=")
                .And.NotContain("<strong>")
                .And.NotContain("onload=\"");

            MarkdownDocument callMarkdownSyntax = Markdown.Parse(callMarkdown, new MarkdownPipelineBuilder().Build());
            callMarkdownSyntax.Descendants().OfType<LinkInline>().Where(link => !link.IsImage).Select(link => link.Url)
                .Should()
                .Equal("../guide.md", "https://example.com/");
            callMarkdownSyntax.Descendants().OfType<HtmlInline>().Should().BeEmpty();
            callMarkdownSyntax.Descendants().OfType<HtmlBlock>().Should().BeEmpty();

            (int argumentLine, int argumentCharacter) = GetSourcePosition(caller, "input:");
            JsonElement argumentHover = await session.Request("textDocument/hover", new
            {
                textDocument = new { uri = callerUri },
                position = new { line = argumentLine, character = argumentCharacter }
            });
            argumentHover.GetProperty("contents").GetProperty("value").GetString()
                .Should()
                .Contain("parameter int32 input in Helper")
                .And.Contain("The supplied **value**.")
                .And.Contain("continued over another **careful** line.")
                .And.NotContain("Adds one to its input.");

            string changedCallee = callee.Replace("Adds one to its input.", "Increments the supplied value.", StringComparison.Ordinal);
            await session.Notify("textDocument/didChange", new
            {
                textDocument = new { uri = calleeUri, version = 2 },
                contentChanges = new[] { new { text = changedCallee } }
            });
            JsonElement updatedHover = await session.Request("textDocument/hover", new
            {
                textDocument = new { uri = callerUri },
                position = new { line = callLine, character = callCharacter }
            });
            updatedHover.GetProperty("contents").GetProperty("value").GetString()
                .Should()
                .Contain("Increments the supplied value.")
                .And.NotContain("Adds one to its input.");

            int localOffset = caller.IndexOf("return result", StringComparison.Ordinal) + "return ".Length;
            (int localLine, int localCharacter) = GetSourcePositionAtOffset(caller, localOffset);
            JsonElement localHover = await session.Request("textDocument/hover", new
            {
                textDocument = new { uri = callerUri },
                position = new { line = localLine, character = localCharacter }
            });
            localHover.GetProperty("contents").GetProperty("value").GetString()
                .Should()
                .Contain("local int32 result");

            int punctuationOffset = caller.IndexOf("return result;", StringComparison.Ordinal) + "return result".Length;
            (int punctuationLine, int punctuationCharacter) = GetSourcePositionAtOffset(caller, punctuationOffset);
            JsonElement punctuationHover = await RequestSourceHover(session, callerUri, punctuationLine, punctuationCharacter);
            punctuationHover.ValueKind.Should().Be(JsonValueKind.Null, "punctuation has no semantic symbol identity");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task InitializedServerShouldNotHoverUnresolvedOrInvalidInvocationTargets()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "Hover Resolution " + Guid.NewGuid().ToString("N"));
        string sourcesDirectory = Path.Combine(directory, "Sources");
        Directory.CreateDirectory(sourcesDirectory);
        try
        {
            const string configuration = """
                name = "HoverResolution"
                assembly = "HoverResolution"
                language-version = "1.0"
                [build]
                default = "debug"
                sources = ["Sources/**/*.sus"]
                [build.targets.debug]
                type = "Sushi.MVP.Debug"
                """;
            string caller = "package Example; namespace Example;\npublic int32 Caller(int32 value) { return Helper(input: value); }";
            string callee = "package Example; namespace Example;\n/// Returns its input.\n/// @param input The input.\n/// @return The same value.\npublic int32 Helper(int32 input) { return input; }";
            string projectPath = Path.Combine(directory, "HoverResolution.susproj");
            string callerPath = Path.Combine(sourcesDirectory, "Caller.sus");
            string calleePath = Path.Combine(sourcesDirectory, "Helper.sus");
            await File.WriteAllTextAsync(projectPath, configuration);
            await File.WriteAllTextAsync(callerPath, caller);
            await File.WriteAllTextAsync(calleePath, callee);
            string callerUri = ExtensionUri(new Uri(callerPath));
            string calleeUri = ExtensionUri(new Uri(calleePath));
            await using ServerSession session = new(directory);
            _ = await session.Request("initialize", new
            {
                processId = (int?)null,
                capabilities = new { textDocument = new { hover = new { contentFormat = hoverContentFormats } } },
                workspaceFolders = new[] { new { uri = ExtensionUri(new Uri(directory)), name = "HoverResolution" } }
            });
            await session.Notify("initialized", new { });
            await session.Notify("textDocument/didOpen", new { textDocument = new { uri = callerUri, languageId = "sushi", version = 1, text = caller } });
            await session.Notify("textDocument/didOpen", new { textDocument = new { uri = calleeUri, languageId = "sushi", version = 1, text = callee } });

            async Task<JsonElement> HoverOn(string source, string name)
            {
                (int line, int character) = GetSourcePosition(source, name);
                return await RequestSourceHover(session, callerUri, line, character);
            }

            JsonElement resolvedHover = await HoverOn(caller, "Helper");
            resolvedHover.GetProperty("contents").GetProperty("value").GetString()
                .Should()
                .Contain("public int32 Helper(int32 input)")
                .And.Contain("Returns its input.");

            string intermediateDocumentation = callee.Replace("Returns its input.", "Intermediate description.", StringComparison.Ordinal);
            string latestDocumentation = callee.Replace("Returns its input.", "Latest description.", StringComparison.Ordinal);
            await session.Notify("textDocument/didChange", new
            {
                textDocument = new { uri = calleeUri, version = 2 },
                contentChanges = new[] { new { text = intermediateDocumentation } }
            });
            await session.Notify("textDocument/didChange", new
            {
                textDocument = new { uri = calleeUri, version = 3 },
                contentChanges = new[] { new { text = latestDocumentation } }
            });
            JsonElement latestHover = await HoverOn(caller, "Helper");
            latestHover.GetProperty("contents").GetProperty("value").GetString()
                .Should()
                .Contain("Latest description.")
                .And.NotContain("Intermediate description.")
                .And.NotContain("Returns its input.");

            int version = 1;
            async Task AssertNoHoverFor(string changedCaller, string target)
            {
                version++;
                await session.Notify("textDocument/didChange", new
                {
                    textDocument = new { uri = callerUri, version },
                    contentChanges = new[] { new { text = changedCaller } }
                });
                JsonElement hover = await HoverOn(changedCaller, target);
                hover.ValueKind.Should().Be(JsonValueKind.Null, $"{target} has no valid compiler-selected callable");
            }

            await AssertNoHoverFor(caller.Replace("Helper(input: value)", "Missing(value)", StringComparison.Ordinal), "Missing");
            await AssertNoHoverFor(caller.Replace("input: value", "unknown: value", StringComparison.Ordinal), "Helper");
            await AssertNoHoverFor(caller.Replace("input: value", "input: true", StringComparison.Ordinal), "Helper");
            string incompleteCaller = "package Example; namespace Example;\npublic int32 Caller(int32 value) { return Helper(";
            await AssertNoHoverFor(incompleteCaller, "Helper");
            JsonElement outOfRangeHover = await RequestSourceHover(session, callerUri, 1, 10000);
            outOfRangeHover.ValueKind.Should().Be(JsonValueKind.Null, "an invalid editor position cannot identify a source symbol");

            await session.Notify("textDocument/didChange", new
            {
                textDocument = new { uri = calleeUri, version = 4 },
                contentChanges = new[] { new { text = latestDocumentation.Replace("int32 input", "bool input", StringComparison.Ordinal)
                    .Replace("return input;", "return 1;", StringComparison.Ordinal) } }
            });
            await AssertNoHoverFor(caller, "Helper");

            await session.Notify("textDocument/didChange", new
            {
                textDocument = new { uri = calleeUri, version = 5 },
                contentChanges = new[] { new { text = latestDocumentation } }
            });
            (int restoredLine, int restoredCharacter) = GetSourcePosition(caller, "Helper");
            JsonElement restoredHover = await RequestSourceHover(session, callerUri, restoredLine, restoredCharacter);
            restoredHover.GetProperty("contents").GetProperty("value").GetString()
                .Should()
                .Contain("Latest description.");

            await session.Notify("textDocument/didChange", new
            {
                textDocument = new { uri = calleeUri, version = 6 },
                contentChanges = new[] { new { text = latestDocumentation + "\npublic int32 Helper(bool input) { return 1; }" } }
            });
            await AssertNoHoverFor(caller.Replace("input: value", "value", StringComparison.Ordinal), "Helper");

            await session.Notify("textDocument/didChange", new
            {
                textDocument = new { uri = calleeUri, version = 7 },
                contentChanges = new[] { new { text = latestDocumentation.Replace("public int32 Helper", "private int32 Helper", StringComparison.Ordinal) } }
            });
            await AssertNoHoverFor(caller.Replace("input: value", "value", StringComparison.Ordinal), "Helper");

            await session.Notify("textDocument/didChange", new
            {
                textDocument = new { uri = calleeUri, version = 8 },
                contentChanges = new[] { new { text = latestDocumentation.Replace("Helper", "Renamed", StringComparison.Ordinal) } }
            });
            await AssertNoHoverFor(caller.Replace("input: value", "value", StringComparison.Ordinal), "Helper");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task InitializedServerShouldResolveSourceDefinitionsUsingCurrentCompilerSelectedSymbols()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "Source Definition " + Guid.NewGuid().ToString("N"));
        string sourcesDirectory = Path.Combine(directory, "Sources");
        Directory.CreateDirectory(sourcesDirectory);
        try
        {
            const string configuration = """
                name = "SourceDefinition"
                assembly = "SourceDefinition"
                language-version = "1.0"
                [build]
                default = "debug"
                sources = ["Sources/**/*.sus"]
                [build.targets.debug]
                type = "Sushi.MVP.Debug"
                """;
            string caller = "package Example;\rnamespace Example;\rpublic int32 Calculate(int32 input) { int32 result = Double(value: input); return result; }";
            string callee = "package Example;\r\nnamespace Example;\r\n/// 🍣 doubles its input.\r\npublic int32 Double(int32 value) { return value * 2; }";
            string activity = "package Example;\r\nnamespace Example;\r\npublic activity Application : task { public entry void Start() { return; } }";
            string projectPath = Path.Combine(directory, "SourceDefinition.susproj");
            string callerPath = Path.Combine(sourcesDirectory, "Caller.sus");
            string calleePath = Path.Combine(sourcesDirectory, "Math.sus");
            string activityPath = Path.Combine(sourcesDirectory, "Application.sus");
            await File.WriteAllTextAsync(projectPath, configuration);
            await File.WriteAllTextAsync(callerPath, caller);
            await File.WriteAllTextAsync(calleePath, callee);
            await File.WriteAllTextAsync(activityPath, activity);
            string callerUri = ExtensionUri(new Uri(callerPath));
            string calleeUri = ExtensionUri(new Uri(calleePath));
            string activityUri = ExtensionUri(new Uri(activityPath));
            await using ServerSession session = new(directory);
            JsonElement initialization = await session.Request("initialize", new
            {
                processId = (int?)null,
                capabilities = new { textDocument = new { hover = new { contentFormat = hoverContentFormats } } },
                workspaceFolders = new[] { new { uri = ExtensionUri(new Uri(directory)), name = "SourceDefinition" } }
            });
            initialization.GetProperty("capabilities").TryGetProperty("definitionProvider", out _)
                .Should()
                .BeTrue("ordinary Sushi source definition navigation is registered");
            await session.Notify("initialized", new { });
            await session.Notify("textDocument/didOpen", new { textDocument = new { uri = callerUri, languageId = "sushi", version = 1, text = caller } });
            await session.Notify("textDocument/didOpen", new { textDocument = new { uri = calleeUri, languageId = "sushi", version = 1, text = callee } });
            await session.Notify("textDocument/didOpen", new { textDocument = new { uri = activityUri, languageId = "sushi", version = 1, text = activity } });

            async Task AssertTarget(string source, string token, string targetSource, string targetToken)
            {
                (int line, int character) = GetUtf16SourcePosition(source, token);
                JsonElement location = await RequestSourceDefinition(session, callerUri, line, character);
                AssertLocationUri(location, calleeUri);
                (int targetLine, int targetCharacter) = GetUtf16SourcePosition(targetSource, targetToken);
                JsonElement range = location.GetProperty("range");
                range.GetProperty("start").GetProperty("line").GetInt32().Should().Be(targetLine);
                range.GetProperty("start").GetProperty("character").GetInt32().Should().Be(targetCharacter);
                range.GetProperty("end").GetProperty("line").GetInt32().Should().Be(targetLine);
                range.GetProperty("end").GetProperty("character").GetInt32().Should().Be(targetCharacter + targetToken.Length);
            }

            await AssertTarget(caller, "Double", callee, "Double");
            await AssertTarget(caller, "value:", callee, "value");

            (int calleeDeclarationLine, int calleeDeclarationCharacter) = GetUtf16SourcePosition(callee, "Double");
            JsonElement declarationLocation = await RequestSourceDefinition(session, calleeUri, calleeDeclarationLine, calleeDeclarationCharacter);
            AssertLocationUri(declarationLocation, calleeUri);
            AssertLocationRange(declarationLocation, callee, "Double");

            (int activityLine, int activityCharacter) = GetUtf16SourcePosition(activity, "Application");
            JsonElement activityLocation = await RequestSourceDefinition(session, activityUri, activityLine, activityCharacter);
            AssertLocationUri(activityLocation, activityUri);
            AssertLocationRange(activityLocation, activity, "Application");
            (int entryLine, int entryCharacter) = GetUtf16SourcePosition(activity, "Start");
            JsonElement entryLocation = await RequestSourceDefinition(session, activityUri, entryLine, entryCharacter);
            AssertLocationUri(entryLocation, activityUri);
            AssertLocationRange(entryLocation, activity, "Start");

            (int inputLine, int inputCharacter) = GetUtf16SourcePosition(caller, "input)");
            JsonElement inputLocation = await RequestSourceDefinition(session, callerUri, inputLine, inputCharacter);
            AssertLocationUri(inputLocation, callerUri);
            AssertLocationRange(inputLocation, caller, "input");

            (int localLine, int localCharacter) = GetUtf16SourcePosition(caller, "return result");
            JsonElement localLocation = await RequestSourceDefinition(session, callerUri, localLine, localCharacter + "return ".Length);
            AssertLocationUri(localLocation, callerUri);
            AssertLocationRange(localLocation, caller, "result");

            JsonElement hover = await RequestSourceHover(session, callerUri, GetUtf16SourcePosition(caller, "Double").Line,
                GetUtf16SourcePosition(caller, "Double").Character);
            hover.GetProperty("contents").GetProperty("value").GetString()
                .Should()
                .Contain("public int32 Double(int32 value)");

            (int builtInLine, int builtInCharacter) = GetUtf16SourcePosition(caller, "int32 Calculate");
            JsonElement builtIn = await RequestSourceDefinition(session, callerUri, builtInLine, builtInCharacter);
            builtIn.ValueKind.Should().Be(JsonValueKind.Null, "built-in types have no Sushi source declaration");
            (int punctuationLine, int punctuationCharacter) = GetUtf16SourcePosition(caller, "return result;");
            JsonElement punctuation = await RequestSourceDefinition(session, callerUri, punctuationLine, punctuationCharacter + "return result".Length);
            punctuation.ValueKind.Should().Be(JsonValueKind.Null, "punctuation does not select a semantic occurrence");
            JsonElement outOfRange = await RequestSourceDefinition(session, callerUri, 1000, 0);
            outOfRange.ValueKind.Should().Be(JsonValueKind.Null, "invalid protocol positions have no target");

            string movedCaller = caller.Replace("public int32 Calculate", "/// Current source snapshot.\rpublic int32 Calculate", StringComparison.Ordinal);
            await session.Notify("textDocument/didChange", new
            {
                textDocument = new { uri = callerUri, version = 2 },
                contentChanges = new[] { new { text = movedCaller } }
            });
            await AssertTarget(movedCaller, "Double", callee, "Double");

            string movedCallee = "package Example;\r\nnamespace Example;\r\n\r\n/// 🍣 doubles its input.\r\npublic int32 Double(int32 value) { return value * 2; }";
            await session.Notify("textDocument/didChange", new
            {
                textDocument = new { uri = calleeUri, version = 2 },
                contentChanges = new[] { new { text = movedCallee } }
            });
            await AssertTarget(movedCaller, "Double", movedCallee, "Double");

            string inaccessibleCallee = movedCallee.Replace("public int32 Double", "private int32 Double", StringComparison.Ordinal);
            await session.Notify("textDocument/didChange", new
            {
                textDocument = new { uri = calleeUri, version = 3 },
                contentChanges = new[] { new { text = inaccessibleCallee } }
            });
            (int callLine, int callCharacter) = GetUtf16SourcePosition(movedCaller, "Double");
            JsonElement inaccessible = await RequestSourceDefinition(session, callerUri, callLine, callCharacter);
            inaccessible.ValueKind.Should().Be(JsonValueKind.Null, "an inaccessible call has no selected definition");

            string ambiguousCallee = movedCallee + "\r\npublic bool Double(bool other) { return other; }";
            await session.Notify("textDocument/didChange", new
            {
                textDocument = new { uri = calleeUri, version = 4 },
                contentChanges = new[] { new { text = ambiguousCallee } }
            });
            JsonElement ambiguous = await RequestSourceDefinition(session, callerUri, callLine, callCharacter);
            ambiguous.ValueKind.Should().Be(JsonValueKind.Null, "unsupported overload ambiguity cannot choose a declaration");

            string invalidCaller = movedCaller.Replace("Double(value: input)", "Missing(value: input)", StringComparison.Ordinal);
            await session.Notify("textDocument/didChange", new
            {
                textDocument = new { uri = callerUri, version = 3 },
                contentChanges = new[] { new { text = invalidCaller } }
            });
            (int missingLine, int missingCharacter) = GetUtf16SourcePosition(invalidCaller, "Missing");
            JsonElement missing = await RequestSourceDefinition(session, callerUri, missingLine, missingCharacter);
            missing.ValueKind.Should().Be(JsonValueKind.Null, "unresolved calls have no compiler-selected declaration");

            Directory.Exists(Path.Combine(directory, "bin"))
                .Should()
                .BeFalse("source navigation must not invoke native compilation");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task InitializedServerShouldFindCompilerResolvedSourceReferencesAcrossProjectFiles()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "Source References " + Guid.NewGuid().ToString("N"));
        string sourcesDirectory = Path.Combine(directory, "Sources");
        string unrelatedProjectDirectory = Path.Combine(directory, "OtherProject");
        Directory.CreateDirectory(sourcesDirectory);
        Directory.CreateDirectory(unrelatedProjectDirectory);
        try
        {
            const string configuration = """
                name = "SourceReferences"
                assembly = "SourceReferences"
                language-version = "1.0"
                [build]
                default = "debug"
                sources = ["Sources/**/*.sus"]
                [build.targets.debug]
                type = "Sushi.MVP.Debug"
                """;
            const string unrelatedConfiguration = """
                name = "Unrelated"
                assembly = "Unrelated"
                language-version = "1.0"
                [build]
                default = "debug"
                sources = ["*.sus"]
                [build.targets.debug]
                type = "Sushi.MVP.Debug"
                """;
            string callee = "package Example;\nnamespace Example;\n/*🍣*/ public int32 Double(int32 value) { return value * value; }";
            string caller = "package Example;\r\nnamespace Example;\r\npublic int32 Calculate(int32 input) { /*🍣*/ int32 result = Double(value: input); result += 1; return Double(value: result); }";
            string otherCaller = "package Example;\rnamespace Example;\rpublic int32 Reuse(int32 other) { int32 result = other; return Double(value: result); }";
            string unrelatedCallee = "package Example;\nnamespace Example;\npublic int32 Double(int32 value) { return value + 100; }";
            string activity = "package Example;\nnamespace Example;\npublic activity Application : task { public entry void Start() { return; } }";
            string projectPath = Path.Combine(directory, "SourceReferences.susproj");
            string callerPath = Path.Combine(sourcesDirectory, "Caller.sus");
            string otherCallerPath = Path.Combine(sourcesDirectory, "Other.sus");
            string calleePath = Path.Combine(sourcesDirectory, "Math.sus");
            string unrelatedProjectPath = Path.Combine(unrelatedProjectDirectory, "Unrelated.susproj");
            string unrelatedCalleePath = Path.Combine(unrelatedProjectDirectory, "Math.sus");
            string activityPath = Path.Combine(sourcesDirectory, "Application.sus");
            await File.WriteAllTextAsync(projectPath, configuration);
            await File.WriteAllTextAsync(callerPath, caller);
            await File.WriteAllTextAsync(otherCallerPath, otherCaller);
            await File.WriteAllTextAsync(calleePath, callee);
            await File.WriteAllTextAsync(unrelatedProjectPath, unrelatedConfiguration);
            await File.WriteAllTextAsync(unrelatedCalleePath, unrelatedCallee);
            await File.WriteAllTextAsync(activityPath, activity);
            string callerUri = ExtensionUri(new Uri(callerPath));
            string otherCallerUri = ExtensionUri(new Uri(otherCallerPath));
            string calleeUri = ExtensionUri(new Uri(calleePath));
            string activityUri = ExtensionUri(new Uri(activityPath));
            await using ServerSession session = new(directory);
            JsonElement initialization = await session.Request("initialize", new
            {
                processId = (int?)null,
                capabilities = new { textDocument = new { hover = new { contentFormat = hoverContentFormats } } },
                workspaceFolders = new[] { new { uri = ExtensionUri(new Uri(directory)), name = "SourceReferences" } }
            });
            initialization.GetProperty("capabilities").TryGetProperty("referencesProvider", out _)
                .Should()
                .BeTrue("ordinary Sushi source references are registered");
            await session.Notify("initialized", new { });
            await session.Notify("textDocument/didOpen", new { textDocument = new { uri = callerUri, languageId = "sushi", version = 1, text = caller } });

            List<(string Uri, string Source, int Offset, string Token)> Expected(string uri, string source, string token, int count)
            {
                List<(string Uri, string Source, int Offset, string Token)> locations = [];
                int offset = 0;
                for (int index = 0; index < count; index++)
                {
                    offset = source.IndexOf(token, offset, StringComparison.Ordinal);
                    offset.Should().BeGreaterThanOrEqualTo(0);
                    locations.Add((uri, source, offset, token));
                    offset += token.Length;
                }

                return locations;
            }

            async Task AssertReferences(string requestUri, string source, int selectedOffset,
                bool includeDeclaration, params (string Uri, string Source, int Offset, string Token)[] expected)
            {
                (int line, int character) = GetSourcePositionAtOffset(source, selectedOffset);
                JsonElement result = await RequestSourceReferences(session, requestUri, line, character, includeDeclaration);
                result.ValueKind.Should().Be(JsonValueKind.Array);
                JsonElement[] actual = [.. result.EnumerateArray()];
                (int Line, int Character, string Uri, string Source, int Offset, string Token)[] orderedExpected = [.. expected
                    .Select(item => (GetSourcePositionAtOffset(item.Source, item.Offset).Line,
                        GetSourcePositionAtOffset(item.Source, item.Offset).Character, item.Uri, item.Source, item.Offset, item.Token))
                    .OrderBy(item => DocumentUri.Parse(item.Uri).ToUri().LocalPath,
                        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
                    .ThenBy(item => item.Offset)];
                actual.Should().HaveCount(orderedExpected.Length);
                for (int index = 0; index < actual.Length; index++)
                {
                    JsonElement location = actual[index];
                    AssertLocationUri(location, orderedExpected[index].Uri);
                    JsonElement range = location.GetProperty("range");
                    range.GetProperty("start").GetProperty("line").GetInt32().Should().Be(orderedExpected[index].Line);
                    range.GetProperty("start").GetProperty("character").GetInt32().Should().Be(orderedExpected[index].Character);
                    range.GetProperty("end").GetProperty("line").GetInt32().Should().Be(orderedExpected[index].Line);
                    range.GetProperty("end").GetProperty("character").GetInt32().Should()
                        .Be(orderedExpected[index].Character + orderedExpected[index].Token.Length);
                }
            }

            int calleeDefinition = callee.IndexOf("Double", StringComparison.Ordinal);
            int callerCall = caller.IndexOf("Double", StringComparison.Ordinal);
            int otherCall = otherCaller.IndexOf("Double", StringComparison.Ordinal);
            List<(string Uri, string Source, int Offset, string Token)> functionLocations = [];
            functionLocations.AddRange(Expected(callerUri, caller, "Double", 2));
            functionLocations.AddRange(Expected(otherCallerUri, otherCaller, "Double", 1));
            functionLocations.Add((calleeUri, callee, calleeDefinition, "Double"));
            await AssertReferences(calleeUri, callee, calleeDefinition, true, [.. functionLocations]);
            await AssertReferences(callerUri, caller, callerCall, true, [.. functionLocations]);
            await AssertReferences(callerUri, caller, callerCall, false,
                [.. functionLocations.Where(item => !item.Uri.Equals(calleeUri, StringComparison.Ordinal))]);

            List<(string Uri, string Source, int Offset, string Token)> valueLocations = [];
            valueLocations.AddRange(Expected(calleeUri, callee, "value", 3));
            valueLocations.AddRange(Expected(callerUri, caller, "value", 2));
            valueLocations.AddRange(Expected(otherCallerUri, otherCaller, "value", 1));
            int namedLabel = caller.IndexOf("value:", StringComparison.Ordinal);
            await AssertReferences(callerUri, caller, namedLabel, true, [.. valueLocations]);
            await AssertReferences(calleeUri, callee, callee.IndexOf("value", StringComparison.Ordinal), false,
                [.. valueLocations.Where(item => !(item.Uri.Equals(calleeUri, StringComparison.Ordinal)
                    && item.Offset == callee.IndexOf("value", StringComparison.Ordinal)))]);

            List<(string Uri, string Source, int Offset, string Token)> inputLocations =
            [.. Expected(callerUri, caller, "input", 2)];
            await AssertReferences(callerUri, caller, caller.IndexOf("input", StringComparison.Ordinal), true, [.. inputLocations]);

            List<(string Uri, string Source, int Offset, string Token)> localLocations = [.. Expected(callerUri, caller, "result", 3)];
            await AssertReferences(callerUri, caller, caller.IndexOf("result", StringComparison.Ordinal), true, [.. localLocations]);
            await AssertReferences(callerUri, caller, caller.LastIndexOf("result", StringComparison.Ordinal), false,
                [.. localLocations.Where(item => item.Offset != caller.IndexOf("result", StringComparison.Ordinal))]);

            int activityOffset = activity.IndexOf("Application", StringComparison.Ordinal);
            await AssertReferences(activityUri, activity, activityOffset, true,
                (activityUri, activity, activityOffset, "Application"));
            await AssertReferences(activityUri, activity, activityOffset, false);
            int entryOffset = activity.IndexOf("Start", StringComparison.Ordinal);
            await AssertReferences(activityUri, activity, entryOffset, true, (activityUri, activity, entryOffset, "Start"));
            await AssertReferences(activityUri, activity, entryOffset, false);

            int builtInOffset = caller.IndexOf("int32 Calculate", StringComparison.Ordinal);
            await AssertReferences(callerUri, caller, builtInOffset, true);
            int punctuationOffset = caller.IndexOf("; return", StringComparison.Ordinal);
            await AssertReferences(callerUri, caller, punctuationOffset, true);

            (int definitionLine, int definitionCharacter) = GetSourcePositionAtOffset(caller, callerCall);
            JsonElement definition = await RequestSourceDefinition(session, callerUri, definitionLine, definitionCharacter);
            AssertLocationUri(definition, calleeUri);

            string addedCall = otherCaller.Replace("return Double(value: result);", "int32 extra = Double(value: result); return Double(value: extra);", StringComparison.Ordinal);
            await session.Notify("textDocument/didOpen", new { textDocument = new { uri = otherCallerUri, languageId = "sushi", version = 1, text = otherCaller } });
            await session.Notify("textDocument/didChange", new
            {
                textDocument = new { uri = otherCallerUri, version = 2 },
                contentChanges = new[] { new { text = addedCall } }
            });
            functionLocations.RemoveAll(item => item.Uri.Equals(otherCallerUri, StringComparison.Ordinal));
            functionLocations.AddRange(Expected(otherCallerUri, addedCall, "Double", 2));

            string callerWithAdditionalCall = caller.Replace("return Double(value: result);",
                "int32 extraCaller = Double(value: input); return Double(value: extraCaller);", StringComparison.Ordinal);
            await session.Notify("textDocument/didChange", new
            {
                textDocument = new { uri = callerUri, version = 2 },
                contentChanges = new[] { new { text = callerWithAdditionalCall } }
            });
            functionLocations.RemoveAll(item => item.Uri.Equals(callerUri, StringComparison.Ordinal));
            functionLocations.AddRange(Expected(callerUri, callerWithAdditionalCall, "Double", 3));
            await AssertReferences(callerUri, callerWithAdditionalCall,
                callerWithAdditionalCall.IndexOf("Double", StringComparison.Ordinal), true, [.. functionLocations]);

            await session.Notify("textDocument/didChange", new
            {
                textDocument = new { uri = callerUri, version = 3 },
                contentChanges = new[] { new { text = caller } }
            });
            functionLocations.RemoveAll(item => item.Uri.Equals(callerUri, StringComparison.Ordinal));
            functionLocations.AddRange(Expected(callerUri, caller, "Double", 2));
            await AssertReferences(callerUri, caller, callerCall, true, [.. functionLocations]);

            await session.Notify("textDocument/didChange", new
            {
                textDocument = new { uri = otherCallerUri, version = 3 },
                contentChanges = new[] { new { text = otherCaller } }
            });
            functionLocations.RemoveAll(item => item.Uri.Equals(otherCallerUri, StringComparison.Ordinal));
            functionLocations.AddRange(Expected(otherCallerUri, otherCaller, "Double", 1));
            await AssertReferences(callerUri, caller, callerCall, true, [.. functionLocations]);

            string invalidCaller = caller.Replace("Double(value: input)", "Missing(value: input)", StringComparison.Ordinal);
            await session.Notify("textDocument/didChange", new
            {
                textDocument = new { uri = callerUri, version = 4 },
                contentChanges = new[] { new { text = invalidCaller } }
            });
            await AssertReferences(callerUri, invalidCaller, invalidCaller.IndexOf("Missing", StringComparison.Ordinal), true);

            Directory.Exists(Path.Combine(directory, "bin"))
                .Should()
                .BeFalse("Find All References must not invoke native compilation");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task InitializedServerShouldKeepStandaloneReferencesOutsideSiblingProjectDirectories()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "Standalone References " + Guid.NewGuid().ToString("N"));
        string projectDirectory = Path.Combine(directory, "Project");
        string prefixedSiblingDirectory = Path.Combine(directory, "ProjectExtra");
        string invalidProjectDirectory = Path.Combine(directory, "InvalidProject");
        Directory.CreateDirectory(projectDirectory);
        Directory.CreateDirectory(prefixedSiblingDirectory);
        Directory.CreateDirectory(invalidProjectDirectory);
        try
        {
            const string configuration = """
                name = "UnrelatedProject"
                assembly = "UnrelatedProject"
                language-version = "1.0"
                [build]
                default = "debug"
                sources = ["*.sus"]
                [build.targets.debug]
                type = "Sushi.MVP.Debug"
                """;
            string projectSource = "package Example; namespace Example; public int32 Double(int32 value) { return value; }";
            string standaloneSource = "package Example; namespace Example; public int32 Double(int32 value) { return value; } public int32 Caller() { return Double(1); }";
            string invalidSource = "package Example; namespace Example; public int32 Hidden(int32 value) { return Hidden(value); }";
            string projectPath = Path.Combine(projectDirectory, "Unrelated.susproj");
            string projectSourcePath = Path.Combine(projectDirectory, "Math.sus");
            string standalonePath = Path.Combine(prefixedSiblingDirectory, "Standalone.sus");
            string invalidProjectPath = Path.Combine(invalidProjectDirectory, "Invalid.susproj");
            string invalidSourcePath = Path.Combine(invalidProjectDirectory, "Broken.sus");
            await File.WriteAllTextAsync(projectPath, configuration);
            await File.WriteAllTextAsync(projectSourcePath, projectSource);
            await File.WriteAllTextAsync(standalonePath, standaloneSource);
            await File.WriteAllTextAsync(invalidProjectPath, "name = [invalid");
            await File.WriteAllTextAsync(invalidSourcePath, invalidSource);

            string standaloneUri = ExtensionUri(new Uri(standalonePath));
            string invalidUri = ExtensionUri(new Uri(invalidSourcePath));
            await using ServerSession session = new(directory);
            _ = await session.Request("initialize", new
            {
                processId = (int?)null,
                capabilities = new { textDocument = new { hover = new { contentFormat = hoverContentFormats } } },
                workspaceFolders = new[] { new { uri = ExtensionUri(new Uri(directory)), name = "StandaloneReferences" } }
            });
            await session.Notify("initialized", new { });
            await session.Notify("textDocument/didOpen", new
            {
                textDocument = new { uri = standaloneUri, languageId = "sushi", version = 1, text = standaloneSource }
            });
            await session.Notify("textDocument/didOpen", new
            {
                textDocument = new { uri = invalidUri, languageId = "sushi", version = 1, text = invalidSource }
            });

            int selectedOffset = standaloneSource.LastIndexOf("Double", StringComparison.Ordinal);
            (int line, int character) = GetSourcePositionAtOffset(standaloneSource, selectedOffset);
            JsonElement standaloneReferences = await RequestSourceReferences(session, standaloneUri, line, character, true);
            JsonElement[] locations = [.. standaloneReferences.EnumerateArray()];
            locations.Should().HaveCount(2);
            foreach (JsonElement location in locations)
            {
                AssertLocationUri(location, standaloneUri);
            }

            locations[0].GetProperty("range").GetProperty("start").GetProperty("character").GetInt32()
                .Should().Be(standaloneSource.IndexOf("Double", StringComparison.Ordinal));
            locations[1].GetProperty("range").GetProperty("start").GetProperty("character").GetInt32()
                .Should().Be(selectedOffset);

            int hiddenOffset = invalidSource.IndexOf("Hidden", StringComparison.Ordinal);
            (int hiddenLine, int hiddenCharacter) = GetSourcePositionAtOffset(invalidSource, hiddenOffset);
            JsonElement invalidProjectReferences = await RequestSourceReferences(session, invalidUri, hiddenLine, hiddenCharacter, true);
            JsonElement[] invalidProjectLocations = invalidProjectReferences.ValueKind == JsonValueKind.Array
                ? [.. invalidProjectReferences.EnumerateArray()]
                : [];
            (invalidProjectReferences.ValueKind is JsonValueKind.Null
                || invalidProjectReferences.ValueKind == JsonValueKind.Array && invalidProjectLocations.Length == 0)
                .Should()
                .BeTrue("an invalid project cannot establish speculative reference ownership");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task InitializedServerShouldValidateUnopenedBomSourcesAgainstCanonicalProjectSnapshots()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "BOM References " + Guid.NewGuid().ToString("N"));
        string sourcesDirectory = Path.Combine(directory, "Sources");
        Directory.CreateDirectory(sourcesDirectory);
        try
        {
            const string configuration = """
                name = "BomReferences"
                assembly = "BomReferences"
                language-version = "1.0"
                [build]
                default = "debug"
                sources = ["Sources/**/*.sus"]
                [build.targets.debug]
                type = "Sushi.MVP.Debug"
                """;
            string math = "package Example; namespace Example; public int32 Double(int32 value) { return value * 2; }";
            string program = "package Example; namespace Example; public int32 Calculate(int32 value) { return Double(value); }";
            string projectPath = Path.Combine(directory, "BomReferences.susproj");
            string mathPath = Path.Combine(sourcesDirectory, "Math.sus");
            string programPath = Path.Combine(sourcesDirectory, "Program.sus");
            await File.WriteAllTextAsync(projectPath, configuration);
            byte[] mathBytes = Encoding.UTF8.GetBytes(math);
            byte[] mathBytesWithBom = [0xEF, 0xBB, 0xBF, .. mathBytes];
            await File.WriteAllBytesAsync(mathPath, mathBytesWithBom);
            await File.WriteAllTextAsync(programPath, program);

            string projectUri = ExtensionUri(new Uri(projectPath));
            string mathUri = ExtensionUri(new Uri(mathPath));
            string programUri = ExtensionUri(new Uri(programPath));
            await using ServerSession session = new(directory);
            _ = await session.Request("initialize", new
            {
                processId = (int?)null,
                capabilities = new { textDocument = new { hover = new { contentFormat = hoverContentFormats } } },
                workspaceFolders = new[] { new { uri = ExtensionUri(new Uri(directory)), name = "BomReferences" } }
            });
            await session.Notify("initialized", new { });
            await session.Notify("textDocument/didOpen", new
            {
                textDocument = new { uri = programUri, languageId = "sushi", version = 1, text = program }
            });

            int callOffset = program.IndexOf("Double", StringComparison.Ordinal);
            (int line, int character) = GetSourcePositionAtOffset(program, callOffset);
            async Task<JsonElement> Request(bool includeDeclaration)
                => await RequestSourceReferences(session, programUri, line, character, includeDeclaration);

            JsonElement withDeclaration = await Request(includeDeclaration: true);
            JsonElement[] declaredLocations = [.. withDeclaration.EnumerateArray()];
            declaredLocations.Should().HaveCount(2);
            AssertLocationUri(declaredLocations[0], mathUri);
            AssertLocationUri(declaredLocations[1], programUri);
            declaredLocations[0].GetProperty("range").GetProperty("start").GetProperty("character").GetInt32()
                .Should().Be(math.IndexOf("Double", StringComparison.Ordinal), "the BOM is excluded from canonical source coordinates");

            JsonElement referencesOnly = await Request(includeDeclaration: false);
            JsonElement[] referencesWithoutDeclaration = [.. referencesOnly.EnumerateArray()];
            referencesWithoutDeclaration.Should().ContainSingle();
            AssertLocationUri(referencesWithoutDeclaration[0], programUri);
            (await Request(includeDeclaration: true)).GetRawText().Should().Be(withDeclaration.GetRawText());

            await File.WriteAllBytesAsync(mathPath, Encoding.UTF8.GetBytes(math));
            (await Request(includeDeclaration: true)).GetRawText().Should().Be(withDeclaration.GetRawText(),
                "removing only the initial BOM preserves canonical source identity");
            await File.WriteAllBytesAsync(mathPath, mathBytesWithBom);
            (await Request(includeDeclaration: true)).GetRawText().Should().Be(withDeclaration.GetRawText(),
                "restoring only the initial BOM preserves canonical source identity");

            string changedMath = math.Replace("Double", "Triple", StringComparison.Ordinal);
            await File.WriteAllBytesAsync(mathPath, Encoding.UTF8.GetBytes(changedMath));
            (await Request(includeDeclaration: true)).ValueKind.Should().Be(JsonValueKind.Null,
                "a changed unopened source makes the captured analysis stale");
            await session.Notify("workspace/didChangeWatchedFiles", new
            {
                changes = new[] { new { uri = mathUri, type = 2 } }
            });
            JsonElement changedReferences = await Request(includeDeclaration: true);
            changedReferences.ValueKind.Should().Be(JsonValueKind.Array);
            JsonElement[] changedLocations = [.. changedReferences.EnumerateArray()];
            changedLocations.Should().BeEmpty();

            await File.WriteAllBytesAsync(mathPath, mathBytesWithBom);
            await session.Notify("workspace/didChangeWatchedFiles", new
            {
                changes = new[] { new { uri = mathUri, type = 2 } }
            });
            JsonElement restoredSourceReferences = await Request(includeDeclaration: true);
            JsonElement[] restoredSourceLocations = [.. restoredSourceReferences.EnumerateArray()];
            restoredSourceLocations.Should().HaveCount(2);

            await session.Notify("textDocument/didOpen", new
            {
                textDocument = new { uri = projectUri, languageId = "susproj", version = 1, text = configuration }
            });
            string excludedMathConfiguration = configuration.Replace("Sources/**/*.sus", "Sources/Program.sus", StringComparison.Ordinal);
            await session.Notify("textDocument/didChange", new
            {
                textDocument = new { uri = projectUri, version = 2 },
                contentChanges = new[] { new { text = excludedMathConfiguration } }
            });
            JsonElement excludedMemberReferences = await Request(includeDeclaration: true);
            excludedMemberReferences.ValueKind.Should().Be(JsonValueKind.Array);
            JsonElement[] excludedMemberLocations = [.. excludedMemberReferences.EnumerateArray()];
            excludedMemberLocations.Should().BeEmpty();

            await session.Notify("textDocument/didChange", new
            {
                textDocument = new { uri = projectUri, version = 3 },
                contentChanges = new[] { new { text = configuration } }
            });
            JsonElement restoredReferences = await Request(includeDeclaration: true);
            restoredReferences.ValueKind.Should().Be(JsonValueKind.Array);
            JsonElement[] restoredLocations = [.. restoredReferences.EnumerateArray()];
            restoredLocations.Should().HaveCount(2);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task SourceHoverShouldFollowBoundLocalAndParameterIdentitiesAcrossSiblingScopes()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "Hover Local Identity " + Guid.NewGuid().ToString("N"));
        string sourcesDirectory = Path.Combine(directory, "Sources");
        Directory.CreateDirectory(sourcesDirectory);
        try
        {
            const string configuration = """
                name = "HoverLocalIdentity"
                assembly = "HoverLocalIdentity"
                language-version = "1.0"
                [build]
                default = "debug"
                sources = ["Sources/**/*.sus"]
                [build.targets.debug]
                type = "Sushi.MVP.Debug"
                """;
            string source = "package Example; namespace Example;\nprivate int32 Resolve(int32 value) { if (value > 0) { bool item = true; if (item) { return 1; } } if (value < 0) { int32 item = 2; return item; } return value; }";
            string projectPath = Path.Combine(directory, "HoverLocalIdentity.susproj");
            string sourcePath = Path.Combine(sourcesDirectory, "Resolve.sus");
            await File.WriteAllTextAsync(projectPath, configuration);
            await File.WriteAllTextAsync(sourcePath, source);
            string sourceUri = ExtensionUri(new Uri(sourcePath));
            await using ServerSession session = new(directory);
            _ = await session.Request("initialize", new
            {
                processId = (int?)null,
                capabilities = new { textDocument = new { hover = new { contentFormat = hoverContentFormats } } },
                workspaceFolders = new[] { new { uri = ExtensionUri(new Uri(directory)), name = "HoverLocalIdentity" } }
            });
            await session.Notify("initialized", new { });
            await session.Notify("textDocument/didOpen", new { textDocument = new { uri = sourceUri, languageId = "sushi", version = 1, text = source } });

            int booleanReferenceOffset = source.IndexOf("if (item)", StringComparison.Ordinal) + "if (".Length;
            (int boolLine, int boolCharacter) = GetSourcePositionAtOffset(source, booleanReferenceOffset);
            JsonElement boolHover = await RequestSourceHover(session, sourceUri, boolLine, boolCharacter);
            boolHover.GetProperty("contents").GetProperty("value").GetString()
                .Should()
                .Contain("local bool item");

            int integerReferenceOffset = source.IndexOf("return item;", StringComparison.Ordinal) + "return ".Length;
            (int intLine, int intCharacter) = GetSourcePositionAtOffset(source, integerReferenceOffset);
            JsonElement intHover = await RequestSourceHover(session, sourceUri, intLine, intCharacter);
            intHover.GetProperty("contents").GetProperty("value").GetString()
                .Should()
                .Contain("local int32 item");

            int parameterReferenceOffset = source.IndexOf("value >", StringComparison.Ordinal);
            (int parameterLine, int parameterCharacter) = GetSourcePositionAtOffset(source, parameterReferenceOffset);
            JsonElement parameterHover = await RequestSourceHover(session, sourceUri, parameterLine, parameterCharacter);
            parameterHover.GetProperty("contents").GetProperty("value").GetString()
                .Should()
                .Contain("parameter int32 value in Resolve");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static Task<JsonElement> RequestSourceHover(ServerSession session, string uri, int line, int character)
        => session.Request("textDocument/hover", new
        {
            textDocument = new { uri },
            position = new { line, character }
        });

    private static Task<JsonElement> RequestSourceDefinition(ServerSession session, string uri, int line, int character)
        => session.Request("textDocument/definition", new
        {
            textDocument = new { uri },
            position = new { line, character }
        });

    private static Task<JsonElement> RequestSourceReferences(ServerSession session, string uri, int line, int character, bool includeDeclaration)
        => session.Request("textDocument/references", new
        {
            textDocument = new { uri },
            position = new { line, character },
            context = new { includeDeclaration }
        });

    private static (int Line, int Character) GetUtf16SourcePosition(string source, string token)
    {
        int offset = source.IndexOf(token, StringComparison.Ordinal);
        offset.Should().BeGreaterThanOrEqualTo(0);
        int line = 0;
        int lineStart = 0;
        for (int index = 0; index < offset; index++)
        {
            if (source[index] == '\r')
            {
                line++;
                if (index + 1 < offset && source[index + 1] == '\n')
                {
                    index++;
                }

                lineStart = index + 1;
            }
            else if (source[index] == '\n')
            {
                line++;
                lineStart = index + 1;
            }
        }

        return (line, offset - lineStart);
    }

    private static void AssertLocationRange(JsonElement location, string source, string token)
    {
        (int line, int character) = GetUtf16SourcePosition(source, token);
        JsonElement range = location.GetProperty("range");
        range.GetProperty("start").GetProperty("line").GetInt32().Should().Be(line);
        range.GetProperty("start").GetProperty("character").GetInt32().Should().Be(character);
        range.GetProperty("end").GetProperty("line").GetInt32().Should().Be(line);
        range.GetProperty("end").GetProperty("character").GetInt32().Should().Be(character + token.Length);
    }

    private static void AssertLocationUri(JsonElement location, string expectedUri)
        => DocumentUri.Parse(location.GetProperty("uri").GetString()!).ToUri()
            .IsSamePath(DocumentUri.Parse(expectedUri).ToUri())
            .Should()
            .BeTrue();

    private static Task<JsonElement> InspectFunction(ServerSession session, string uri, string source, string functionName, string stage)
    {
        string sourceLine = source.Split('\n')[1];
        int character = sourceLine.IndexOf(functionName, StringComparison.Ordinal) + 1;
        return session.Request("sourceInspection/inspectFunction",
            new { sourceUri = uri, sourceVersion = 1, position = new { line = 1, character }, stage });
    }

    private static Task<JsonElement> NavigateToCallee(ServerSession session, JsonElement inspection)
    {
        string text = inspection.GetProperty("text").GetString()!;
        int offset = text.IndexOf("__sushi_package_function_1", StringComparison.Ordinal);
        offset
            .Should()
            .BeGreaterThanOrEqualTo(0);
        int line = text[..offset].Count(character => character == '\n');
        int character = offset - (text.LastIndexOf('\n', Math.Max(0, offset - 1)) + 1);
        return session.Request("sourceInspection/definition", new
        {
            documentUri = inspection.GetProperty("documentUri").GetString(),
            artifactId = inspection.GetProperty("artifactId").GetString(),
            position = new { line, character }
        });
    }

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

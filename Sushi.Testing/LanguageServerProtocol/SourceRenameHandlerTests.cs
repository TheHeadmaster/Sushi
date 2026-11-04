using System.Text;
using FluentAssertions;
using NUnit.Framework;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sushi.Analysis;
using Sushi.Compilation;
using Sushi.LanguageServerProtocol;
using Sushi.Source;

namespace Sushi.Testing.LanguageServerProtocol;

/// <summary>
/// Exercises source Rename through the compiler-backed handler and prospective analysis path.
/// </summary>
[TestFixture]
public sealed class SourceRenameHandlerTests
{
    [Test]
    public async Task FunctionRenameShouldReturnCompleteEditsThatRemainSemanticallyBound()
    {
        const string source = "package Example; namespace Example; /* 😀 */ public int32 Double(int32 value) { return value; } public int32 Caller() { return Double(1); }";
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "Rename " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "Program.sus");
        await File.WriteAllTextAsync(path, source);
        Uri uri = new(path);

        try
        {
            using WorkspaceOrchestrator workspace = new();
            await workspace.OpenDocument(uri, 4, source, CancellationToken.None);
            SourceRenameHandler handler = new(workspace);
            WorkspaceEdit? edit = await handler.Handle(CreateRenameRequest(path, source.IndexOf("Double", StringComparison.Ordinal), "Twice"), CancellationToken.None);

            edit.Should().NotBeNull();
            edit!.Changes.Should().NotBeNull().And.HaveCount(1);
            IEnumerable<TextEdit> fileEdits = edit.Changes!.Single().Value;
            fileEdits.Should().HaveCount(2);
            string renamed = ApplySingleLineEdits(source, fileEdits);
            renamed.Should().Contain("public int32 Twice(int32 value)")
                .And.Contain("return Twice(1);");

            IReadOnlyList<ProjectDiagnosticResult> analyses = await new ProjectDiagnosticAnalyzer().AnalyzeAsync(
                [SourceSnapshot.FromText(uri, 5, renamed)], [uri], CancellationToken.None);
            SourceAnalysisResult result = analyses.Select(item => item.Analysis).OfType<SourceAnalysisResult>().Single();
            result.SemanticOccurrences.Where(occurrence => occurrence.Symbol.Kind.ToString() == "Function")
                .Should().Contain(occurrence => occurrence.Symbol.Name == "Twice");
            result.SemanticOccurrences.Where(occurrence => occurrence.Symbol.Name == "Twice")
                .Should().HaveCount(2);
            source.Should().Contain("Double");
            (await workspace.GetSemanticSyntax(uri, CancellationToken.None))!.Snapshot.Text.Should().Be(source);

            WorkspaceEdit? fromReference = await handler.Handle(CreateRenameRequest(path, source.LastIndexOf("Double", StringComparison.Ordinal), "Twice"), CancellationToken.None);
            fromReference.Should().NotBeNull();
            fromReference!.Changes!.Single().Value.Select(item => (item.Range.Start.Character, item.Range.End.Character, item.NewText))
                .Should().BeEquivalentTo(fileEdits.Select(item => (item.Range.Start.Character, item.Range.End.Character, item.NewText)));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }


    [Test]
    public async Task FunctionRenameShouldUpdateAResolvedDocumentationLinkAndPreserveItsDisplayLabel()
    {
        const string source = "package Example; namespace Example;\n/// Café: {@link Calculate(int32) | the routine}. `Calculate` stays literal.\npublic int32 Calculate(int32 value) { return value; }\npublic int32 Caller() { return Calculate(1); }";
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "Rename " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "Program.sus");
        await File.WriteAllTextAsync(path, source);
        try
        {
            using WorkspaceOrchestrator workspace = new();
            await workspace.OpenDocument(new Uri(path), 1, source, CancellationToken.None);
            SourceRenameHandler handler = new(workspace);
            int target = source.IndexOf("Calculate", source.IndexOf("{@link", StringComparison.Ordinal), StringComparison.Ordinal);
            (int line, int character) = GetPosition(source, target);
            WorkspaceEdit? edit = await handler.Handle(new RenameParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = DocumentUri.FromFileSystemPath(path) },
                Position = new Position(line, character),
                NewName = "Compute"
            }, CancellationToken.None);

            edit.Should().NotBeNull();
            TextEdit[] edits = edit!.Changes!.Single().Value.ToArray();
            edits.Should().HaveCount(3);
            edits.Single(item => item.Range.Start.Line == 1).Range.Start.Character.Should().Be(
                source.Split('\n')[1].IndexOf("Calculate", StringComparison.Ordinal));
            string renamed = ApplyEdits(source, edits);
            renamed.Should().Contain("{@link Compute(int32) | the routine}")
                .And.Contain("`Calculate` stays literal.")
                .And.Contain("public int32 Compute(int32 value)")
                .And.Contain("return Compute(1);");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }


    [Test]
    public async Task FunctionRenameShouldUpdateDocumentationAndCodeAcrossOpenProjectFiles()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "Rename " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string projectPath = Path.Combine(directory, "Project.susproj");
        string definitionPath = Path.Combine(directory, "Math.sus");
        string callerPath = Path.Combine(directory, "Caller.sus");
        const string project = """
            name = "Rename"
            assembly = "Rename"
            language-version = "1.0"
            [build]
            default = "debug"
            sources = ["*.sus"]
            [build.targets.debug]
            type = "Sushi.MVP.Debug"
            """;
        const string definition = "package Example; namespace Math; public int32 Calculate(int32 value) { return value; }";
        const string caller = "package Example; namespace Math;\n/// {@link Calculate(int32)} is used here.\npublic int32 Caller() { return Calculate(1); }";
        try
        {
            using WorkspaceOrchestrator workspace = new();
            await workspace.OpenDocument(new Uri(projectPath), 1, project, CancellationToken.None);
            await workspace.OpenDocument(new Uri(definitionPath), 1, definition, CancellationToken.None);
            await workspace.OpenDocument(new Uri(callerPath), 1, caller, CancellationToken.None);
            SourceRenameHandler handler = new(workspace);
            WorkspaceEdit? edit = await handler.Handle(CreateRenameRequest(
                definitionPath, definition.IndexOf("Calculate", StringComparison.Ordinal), "Compute"), CancellationToken.None);

            edit.Should().NotBeNull();
            edit!.Changes.Should().NotBeNull().And.HaveCount(2);
            edit.Changes!.Values.SelectMany(value => value)
                .Should().HaveCount(3);
            edit.Changes.Values.SelectMany(value => value).Select(value => value.NewText)
                .Should().OnlyContain(value => value == "Compute");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task RenameToTheExistingNameShouldReturnAnEmptyWorkspaceEdit()
    {
        const string source = "package Example; namespace Example; public int32 Double() { return 1; }";
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "Rename " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "Program.sus");
        await File.WriteAllTextAsync(path, source);

        try
        {
            using WorkspaceOrchestrator workspace = new();
            await workspace.OpenDocument(new Uri(path), 1, source, CancellationToken.None);
            SourceRenameHandler handler = new(workspace);

            WorkspaceEdit? edit = await handler.Handle(CreateRenameRequest(path, source.IndexOf("Double", StringComparison.Ordinal), "Double"), CancellationToken.None);

            edit.Should().NotBeNull();
            edit!.Changes.Should().BeNull();
            edit.DocumentChanges.Should().BeNull();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task LocalRenameShouldUpdateItsDeclarationAndReferences()
    {
        const string source = "package Example; namespace Example; public int32 Caller(int32 input) { int32 result = input; return result; }";
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "Rename " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "Program.sus");
        await File.WriteAllTextAsync(path, source);

        try
        {
            using WorkspaceOrchestrator workspace = new();
            await workspace.OpenDocument(new Uri(path), 1, source, CancellationToken.None);
            SourceRenameHandler handler = new(workspace);

            WorkspaceEdit? edit = await handler.Handle(CreateRenameRequest(path, source.IndexOf("result", StringComparison.Ordinal), "total"), CancellationToken.None);

            edit.Should().NotBeNull();
            TextEdit[] edits = edit!.Changes!.Single().Value.ToArray();
            edits.Should().HaveCount(2);
            string renamed = ApplyEdits(source, edits);
            renamed.Should().Contain("int32 total = input;")
                .And.Contain("return total;");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task ParameterRenameShouldUpdateBoundUsesAndAttachedParamDirective()
    {
        const string source = "package Example; namespace Example;\n/// {@param value} Inline parameter reference.\n/// @param value The value.\npublic int32 Double(int32 value) { return value; } public int32 Caller(int32 input) { return Double(value: input); }";
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "Rename " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "Program.sus");
        await File.WriteAllTextAsync(path, source);
        Uri uri = new(path);

        try
        {
            using WorkspaceOrchestrator workspace = new();
            await workspace.OpenDocument(uri, 1, source, CancellationToken.None);
            SourceRenameHandler handler = new(workspace);
            int selection = source.IndexOf("value", source.IndexOf("Double", StringComparison.Ordinal), StringComparison.Ordinal);
            (int line, int character) = GetPosition(source, selection);
            WorkspaceEdit? edit = await handler.Handle(new RenameParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = DocumentUri.FromFileSystemPath(path) },
                Position = new Position(line, character),
                NewName = "number"
            }, CancellationToken.None);

            edit.Should().NotBeNull();
            TextEdit[] edits = edit!.Changes!.Single().Value.ToArray();
            edits.Should().HaveCount(5);
            string renamed = ApplyEdits(source, edits);
            renamed.Should().Contain("{@param number} Inline parameter reference.")
                .And.Contain("@param number The value.")
                .And.Contain("Double(int32 number)")
                .And.Contain("return number;")
                .And.Contain("Double(number: input)");
            renamed.Should().Contain("Double(number: input)");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task ParameterRenameShouldRejectAnExistingParameterName()
    {
        const string source = "package Example; namespace Example; public int32 Sum(int32 left, int32 right) { return left + right; }";
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "Rename " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "Program.sus");
        await File.WriteAllTextAsync(path, source);

        try
        {
            using WorkspaceOrchestrator workspace = new();
            await workspace.OpenDocument(new Uri(path), 1, source, CancellationToken.None);
            SourceRenameHandler handler = new(workspace);

            WorkspaceEdit? edit = await handler.Handle(CreateRenameRequest(path, source.IndexOf("left", StringComparison.Ordinal), "right"), CancellationToken.None);

            edit.Should().BeNull();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task RenameThatWouldCaptureAParameterShouldBeRejectedWithoutEdits()
    {
        const string source = "package Example; namespace Example; public int32 Calculate(int32 input) { int32 result = input; return result; }";
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "Rename " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "Program.sus");
        await File.WriteAllTextAsync(path, source);

        try
        {
            using WorkspaceOrchestrator workspace = new();
            await workspace.OpenDocument(new Uri(path), 1, source, CancellationToken.None);
            SourceRenameHandler handler = new(workspace);

            WorkspaceEdit? edit = await handler.Handle(CreateRenameRequest(path, source.IndexOf("result", StringComparison.Ordinal), "input"), CancellationToken.None);

            edit.Should().BeNull();
            source.Should().Contain("int32 result");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestCase("@return")]
    [TestCase("@int32")]
    public async Task FunctionRenameShouldAllowAValidEscapedReservedIdentifier(string escapedName)
    {
        const string source = "package Example; namespace Example; public int32 Double() { return 1; } public int32 Caller() { return Double(); }";
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "Rename " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "Program.sus");
        await File.WriteAllTextAsync(path, source);

        try
        {
            using WorkspaceOrchestrator workspace = new();
            await workspace.OpenDocument(new Uri(path), 1, source, CancellationToken.None);
            SourceRenameHandler handler = new(workspace);

            WorkspaceEdit? edit = await handler.Handle(CreateRenameRequest(path, source.IndexOf("Double", StringComparison.Ordinal), escapedName), CancellationToken.None);

            edit.Should().NotBeNull();
            TextEdit[] edits = edit!.Changes!.Single().Value.ToArray();
            edits.Should().HaveCount(2);
            edits.Should().OnlyContain(item => item.NewText == escapedName);
            string renamed = ApplyEdits(source, edits);
            renamed.Should().Contain($"public int32 {escapedName}()")
                .And.Contain($"return {escapedName}();");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task PrepareRenameShouldSelectTheCompleteEscapedIdentifierToken()
    {
        const string source = "package Example; namespace Example; public int32 @return() { return 1; }";
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "Rename " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "Program.sus");
        await File.WriteAllTextAsync(path, source);

        try
        {
            using WorkspaceOrchestrator workspace = new();
            await workspace.OpenDocument(new Uri(path), 1, source, CancellationToken.None);
            SourcePrepareRenameHandler handler = new(workspace);
            RangeOrPlaceholderRange? result = await handler.Handle(new PrepareRenameParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = DocumentUri.FromFileSystemPath(path) },
                Position = new Position(0, source.IndexOf("@return", StringComparison.Ordinal) + 2)
            }, CancellationToken.None);

            result.Should().NotBeNull();
            result!.PlaceholderRange.Should().NotBeNull();
            result.PlaceholderRange!.Placeholder.Should().Be("@return");
            result.PlaceholderRange.Range.Start.Character.Should().Be(source.IndexOf("@return", StringComparison.Ordinal));
            result.PlaceholderRange.Range.End.Character.Should().Be(source.IndexOf("@return", StringComparison.Ordinal) + "@return".Length);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static RenameParams CreateRenameRequest(string path, int offset, string newName)
        => new()
        {
            TextDocument = new TextDocumentIdentifier { Uri = DocumentUri.FromFileSystemPath(path) },
            Position = new Position(0, offset),
            NewName = newName
        };

    private static string ApplySingleLineEdits(string source, IEnumerable<TextEdit> edits)
        => ApplyEdits(source, [.. edits]);

    private static string ApplyEdits(string source, IReadOnlyList<TextEdit> edits)
    {
        StringBuilder builder = new(source);
        foreach (TextEdit edit in edits.OrderByDescending(item => item.Range.Start.Line).ThenByDescending(item => item.Range.Start.Character))
        {
            int start = OffsetAt(source, edit.Range.Start.Line, edit.Range.Start.Character);
            int end = OffsetAt(source, edit.Range.End.Line, edit.Range.End.Character);
            builder.Remove(start, end - start).Insert(start, edit.NewText);
        }

        return builder.ToString();
    }

    private static (int Line, int Character) GetPosition(string source, int offset)
    {
        int line = 0;
        int lineStart = 0;
        for (int index = 0; index < offset; index++)
        {
            if (source[index] == '\n')
            {
                line++;
                lineStart = index + 1;
            }
        }

        return (line, offset - lineStart);
    }

    private static int OffsetAt(string source, int line, int character)
    {
        int lineStart = 0;
        for (int currentLine = 0; currentLine < line; currentLine++)
        {
            lineStart = source.IndexOf('\n', lineStart) + 1;
        }

        return lineStart + character;
    }
}
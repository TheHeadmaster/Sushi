using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using Sushi.LanguageServerProtocol;

namespace Sushi.Testing.LanguageServerProtocol;

/// <summary>
/// Verifies Rename registration and workspace-edit routing through an initialized language server.
/// </summary>
[TestFixture]
public sealed class SourceRenameProtocolTests
{
    [Test]
    public async Task InitializedServerShouldExposePrepareRenameAndReturnVersionedEdits()
    {
        const string declaration = "package Example; namespace Example; public int32 Double(int32 value) { return value; }";
        const string source = "package Example; namespace Example; public int32 Caller() { return Double(1); }";
        string directory = Path.Combine(Path.GetTempPath(), "Sushi.Testing", "RenameProtocol " + Guid.NewGuid().ToString("N"));
        string sourceDirectory = Path.Combine(directory, "Sources");
        Directory.CreateDirectory(sourceDirectory);
        string projectPath = Path.Combine(directory, "RenameProtocol.susproj");
        await File.WriteAllTextAsync(projectPath, """
            name = "RenameProtocol"
            assembly = "RenameProtocol"
            language-version = "1.0"
            [build]
            default = "debug"
            sources = ["Sources/**/*.sus"]
            [build.targets.debug]
            type = "Sushi.MVP.Debug"
            """);
        string declarationPath = Path.Combine(sourceDirectory, "Math.sus");
        await File.WriteAllTextAsync(declarationPath, declaration);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(sourceDirectory, "Program.sus");
        await File.WriteAllTextAsync(path, source);
        string uri = new Uri(path).AbsoluteUri;

        try
        {
            await using LanguageServerSession session = new(directory);
            JsonElement initialize = await session.Request("initialize", new
            {
                processId = (int?)null,
                capabilities = new
                {
                    textDocument = new { rename = new { prepareSupport = true } },
                    workspace = new { workspaceEdit = new { documentChanges = true } }
                },
                workspaceFolders = new[] { new { uri = new Uri(directory).AbsoluteUri, name = "RenameProtocol" } }
            });
            initialize.GetProperty("capabilities").GetProperty("renameProvider").GetProperty("prepareProvider")
                .GetBoolean().Should().BeTrue();
            await session.Notify("initialized", new { });
            await session.Notify("textDocument/didOpen", new
            {
                textDocument = new { uri, languageId = "sushi", version = 7, text = source }
            });
            string declarationUri = new Uri(declarationPath).AbsoluteUri;
            await session.Notify("textDocument/didOpen", new
            {
                textDocument = new { uri = declarationUri, languageId = "sushi", version = 3, text = declaration }
            });

            int selected = source.IndexOf("Double", StringComparison.Ordinal);
            JsonElement prepared = await session.Request("textDocument/prepareRename", new
            {
                textDocument = new { uri },
                position = new { line = 0, character = selected + 1 }
            });
            prepared.GetProperty("placeholder").GetString().Should().Be("Double");
            prepared.GetProperty("range").GetProperty("start").GetProperty("character").GetInt32().Should().Be(selected);

            JsonElement edit = await session.Request("textDocument/rename", new
            {
                textDocument = new { uri },
                position = new { line = 0, character = selected + 1 },
                newName = "Twice"
            });
            JsonElement[] documentEdits = [.. edit.GetProperty("documentChanges").EnumerateArray()];
            documentEdits.Should().HaveCount(2);
            JsonElement programEdit = documentEdits.Single(item => item.GetProperty("textDocument").GetProperty("uri").GetString() == uri);
            JsonElement mathEdit = documentEdits.Single(item => item.GetProperty("textDocument").GetProperty("uri").GetString() == declarationUri);
            programEdit.GetProperty("textDocument").GetProperty("version").GetInt32().Should().Be(7);
            mathEdit.GetProperty("textDocument").GetProperty("version").GetInt32().Should().Be(3);
            programEdit.GetProperty("edits").GetArrayLength().Should().Be(1);
            mathEdit.GetProperty("edits").GetArrayLength().Should().Be(1);
            programEdit.GetProperty("edits")[0].GetProperty("newText").GetString().Should().Be("Twice");
            mathEdit.GetProperty("edits")[0].GetProperty("newText").GetString().Should().Be("Twice");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class LanguageServerSession : IAsyncDisposable
    {
        private readonly Process process;
        private readonly Stream input;
        private readonly Stream output;
        private int nextId;

        internal LanguageServerSession(string workingDirectory)
        {
            string host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")
                ?? Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..",
                    OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
            this.process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = host,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true
                }
            };
            this.process.StartInfo.ArgumentList.Add(typeof(WorkspaceOrchestrator).Assembly.Location);
            this.process.StartInfo.ArgumentList.Add("-lsp");
            this.process.StartInfo.ArgumentList.Add("-stdio");
            this.process.Start();
            this.input = this.process.StandardInput.BaseStream;
            this.output = this.process.StandardOutput.BaseStream;
        }

        internal Task Notify(string method, object parameters)
            => this.Send(new { jsonrpc = "2.0", method, @params = parameters });

        internal async Task<JsonElement> Request(string method, object parameters)
        {
            int id = ++this.nextId;
            await this.Send(new { jsonrpc = "2.0", id, method, @params = parameters });
            while (true)
            {
                JsonElement message = await this.ReadMessage();
                if (message.TryGetProperty("id", out JsonElement responseId) && responseId.GetInt32() == id)
                {
                    if (message.TryGetProperty("error", out JsonElement error))
                    {
                        throw new InvalidOperationException(error.ToString());
                    }

                    return message.GetProperty("result").Clone();
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (this.process.HasExited)
            {
                this.process.Dispose();
                return;
            }

            try
            {
                _ = await this.Request("shutdown", new { });
                await this.Notify("exit", new { });
                await this.process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally
            {
                if (!this.process.HasExited)
                {
                    this.process.Kill(entireProcessTree: true);
                }

                this.process.Dispose();
            }
        }

        private async Task Send<T>(T message)
        {
            byte[] content = JsonSerializer.SerializeToUtf8Bytes(message);
            byte[] header = Encoding.ASCII.GetBytes($"Content-Length: {content.Length}\r\n\r\n");
            await this.input.WriteAsync(header);
            await this.input.WriteAsync(content);
            await this.input.FlushAsync();
        }

        private async Task<JsonElement> ReadMessage()
        {
            StringBuilder header = new();
            int matched = 0;
            byte[] singleByte = new byte[1];
            while (matched < 4)
            {
                int read = await this.output.ReadAsync(singleByte.AsMemory());
                if (read == 0)
                {
                    throw new EndOfStreamException("The language server closed its output stream.");
                }

                char character = (char)singleByte[0];
                header.Append(character);
                matched = character switch
                {
                    '\r' when matched is 0 or 2 => matched + 1,
                    '\n' when matched is 1 or 3 => matched + 1,
                    _ => 0
                };
            }

            int contentLength = int.Parse(header.ToString().Split("Content-Length: ", StringSplitOptions.None)[1]
                .Split("\r\n", StringSplitOptions.None)[0], System.Globalization.CultureInfo.InvariantCulture);
            byte[] content = new byte[contentLength];
            int offset = 0;
            while (offset < content.Length)
            {
                int read = await this.output.ReadAsync(content.AsMemory(offset));
                if (read == 0)
                {
                    throw new EndOfStreamException("The language server closed its output stream.");
                }

                offset += read;
            }

            using JsonDocument document = JsonDocument.Parse(content);
            return document.RootElement.Clone();
        }
    }
}

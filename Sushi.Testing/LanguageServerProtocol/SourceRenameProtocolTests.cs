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
    [TestCase(true)]
    [TestCase(false)]
    public async Task InitializedServerShouldExposePrepareRenameAndReturnCapabilityCompatibleEdits(bool supportsDocumentChanges)
    {
        const string declaration = "package Example; namespace Example; public int32 Double(int32 value) { return value; }";
        const string source = "package Example; namespace Example; public int32 Caller() { return Double(1); }";
        const string unopenedSource = "package Example; namespace Example; public int32 OtherCaller() { return Double(2); }";
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
        string unopenedPath = Path.Combine(sourceDirectory, "Other.sus");
        await File.WriteAllTextAsync(unopenedPath, unopenedSource);
        string uri = new Uri(path).AbsoluteUri;
        string unopenedUri = new Uri(unopenedPath).AbsoluteUri;

        try
        {
            await using LanguageServerSession session = new(directory);
            JsonElement initialize = await session.Request("initialize", new
            {
                processId = (int?)null,
                capabilities = new
                {
                    textDocument = new { rename = new { prepareSupport = true } },
                    workspace = new { workspaceEdit = new { documentChanges = supportsDocumentChanges } }
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
            if (supportsDocumentChanges)
            {
                edit.TryGetProperty("changes", out _).Should().BeFalse();
                JsonElement[] documentEdits = [.. edit.GetProperty("documentChanges").EnumerateArray()];
                documentEdits.Should().HaveCount(3);
                JsonElement programEdit = documentEdits.Single(item => item.GetProperty("textDocument").GetProperty("uri").GetString() == uri);
                JsonElement mathEdit = documentEdits.Single(item => item.GetProperty("textDocument").GetProperty("uri").GetString() == declarationUri);
                JsonElement unopenedEdit = documentEdits.Single(item => item.GetProperty("textDocument").GetProperty("uri").GetString() == unopenedUri);
                programEdit.GetProperty("textDocument").GetProperty("version").GetInt32().Should().Be(7);
                mathEdit.GetProperty("textDocument").GetProperty("version").GetInt32().Should().Be(3);
                unopenedEdit.GetProperty("textDocument").GetProperty("version").ValueKind.Should().Be(JsonValueKind.Null);
                programEdit.GetProperty("edits").GetArrayLength().Should().Be(1);
                mathEdit.GetProperty("edits").GetArrayLength().Should().Be(1);
                unopenedEdit.GetProperty("edits").GetArrayLength().Should().Be(1);
                programEdit.GetProperty("edits")[0].GetProperty("newText").GetString().Should().Be("Twice");
                mathEdit.GetProperty("edits")[0].GetProperty("newText").GetString().Should().Be("Twice");
                unopenedEdit.GetProperty("edits")[0].GetProperty("newText").GetString().Should().Be("Twice");
            }
            else
            {
                edit.TryGetProperty("documentChanges", out _).Should().BeFalse();
                JsonElement changes = edit.GetProperty("changes");
                changes.EnumerateObject().Should().HaveCount(3);
                changes.GetProperty(uri)[0].GetProperty("newText").GetString().Should().Be("Twice");
                changes.GetProperty(declarationUri)[0].GetProperty("newText").GetString().Should().Be("Twice");
                changes.GetProperty(unopenedUri)[0].GetProperty("newText").GetString().Should().Be("Twice");
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class LanguageServerSession : IAsyncDisposable
    {
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(5);
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

        internal async Task Notify(string method, object parameters, CancellationToken cancellationToken = default)
        {
            using CancellationTokenSource timeout = CreateTimeout(RequestTimeout, cancellationToken);
            try
            {
                await this.Send(new { jsonrpc = "2.0", method, @params = parameters }, timeout.Token);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Sending language server notification '{method}' timed out after {RequestTimeout}.", exception);
            }
        }

        internal async Task<JsonElement> Request(string method, object parameters, CancellationToken cancellationToken = default)
        {
            int id = ++this.nextId;
            using CancellationTokenSource timeout = CreateTimeout(RequestTimeout, cancellationToken);
            try
            {
                await this.Send(new { jsonrpc = "2.0", id, method, @params = parameters }, timeout.Token);
                while (true)
                {
                    JsonElement message = await this.ReadMessage(method, timeout.Token);
                    if (message.TryGetProperty("id", out JsonElement responseId) && responseId.GetInt32() == id)
                    {
                        if (message.TryGetProperty("error", out JsonElement error))
                        {
                            throw new InvalidOperationException($"Language server request '{method}' failed: {error}.");
                        }

                        return message.GetProperty("result").Clone();
                    }
                }
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Language server request '{method}' timed out after {RequestTimeout}.", exception);
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (!this.process.HasExited)
                {
                    using CancellationTokenSource cleanup = new(CleanupTimeout);
                    try
                    {
                        _ = await this.Request("shutdown", new { }, cleanup.Token);
                        await this.Notify("exit", new { }, cleanup.Token);
                        await this.process.WaitForExitAsync(cleanup.Token);
                    }
                    catch (Exception exception) when (exception is OperationCanceledException or TimeoutException or IOException or InvalidOperationException)
                    {
                    }
                }
            }
            finally
            {
                if (!this.process.HasExited)
                {
                    try
                    {
                        this.process.Kill(entireProcessTree: true);
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }

                this.process.Dispose();
            }
        }

        private async Task Send<T>(T message, CancellationToken cancellationToken)
        {
            byte[] content = JsonSerializer.SerializeToUtf8Bytes(message);
            byte[] header = Encoding.ASCII.GetBytes($"Content-Length: {content.Length}\r\n\r\n");
            await this.input.WriteAsync(header, cancellationToken);
            await this.input.WriteAsync(content, cancellationToken);
            await this.input.FlushAsync(cancellationToken);
        }

        private async Task<JsonElement> ReadMessage(string method, CancellationToken cancellationToken)
        {
            StringBuilder header = new();
            int matched = 0;
            byte[] singleByte = new byte[1];
            while (matched < 4)
            {
                int read = await this.output.ReadAsync(singleByte.AsMemory(), cancellationToken);
                if (read == 0)
                {
                    string exitStatus = this.process.HasExited ? $" Exit code: {this.process.ExitCode}." : string.Empty;
                    throw new EndOfStreamException($"The language server closed its output while awaiting '{method}'.{exitStatus}");
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
                int read = await this.output.ReadAsync(content.AsMemory(offset), cancellationToken);
                if (read == 0)
                {
                    string exitStatus = this.process.HasExited ? $" Exit code: {this.process.ExitCode}." : string.Empty;
                    throw new EndOfStreamException($"The language server closed its output while awaiting '{method}'.{exitStatus}");
                }

                offset += read;
            }

            using JsonDocument document = JsonDocument.Parse(content);
            return document.RootElement.Clone();
        }

        private static CancellationTokenSource CreateTimeout(TimeSpan timeout, CancellationToken cancellationToken)
        {
            CancellationTokenSource source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            source.CancelAfter(timeout);
            return source;
        }
    }
}

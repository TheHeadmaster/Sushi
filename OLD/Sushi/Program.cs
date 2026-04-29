using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using OmniSharp.Extensions.LanguageServer.Server;
using Serilog;
using Serilog.Formatting.Compact;
using Sushi.Compilation;
using Sushi.Diagnostics;
using Sushi.LSP;
using Sushi.Parsing.Core;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi;

/// <summary>
/// Contains application metadata and houses the entry point of the application.
/// </summary>
public static class Program
{
    /// <summary>
    /// Runs the compiler.
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public static async Task Run()
    {
        Lexer lexer = new();
        Parser parser = new();
        CCompilerVisitor compiler = new();
        List<TokenFile> tokenFiles = await lexer.LexFiles(AppMeta.Options.ProjectPath);

        AbstractSyntaxTree tree = await parser.ParseSource(tokenFiles);

        foreach (CompilerMessage message in tree.Messages.OrderBy(x => x.Type))
        {
            await message.LogMessage();
        }

        List<CompiledFile> compiledFiles = await compiler.Compile(tree, parser.Reference);

        await WriteFilesToDisk(compiledFiles);

        if (!AppMeta.Options.IntermediateOnly)
        {
            await ExeCompiler.Compile("Project");
        }
    }

    public static async Task RunLanguageServer()
    {
        SushiLanguageService service = new();


        LanguageServer server = await LanguageServer.From(options => options
            .WithInput(Console.OpenStandardInput())
            .WithOutput(Console.OpenStandardOutput())
            .OnInitialize((server, request, token) => Task.Run(async () =>
            {
                await service.Initialize([.. request.WorkspaceFolders ?? []]);

                return new InitializeResult
                {
                    ServerInfo = new ServerInfo
                    {
                        Name = "Sushi",
                        Version = AppMeta.GetVersion().ToString()
                    },
                    Capabilities = new ServerCapabilities
                    {
                        HoverProvider = true,
                    }
                };
            }))
            .WithHandler<CompletionHandler>()
            .WithHandler<SemanticTokenHandler>()
        );

        await server.WaitForExit;
    }

    /// <summary>
    /// Writes the specified files to disk.
    /// </summary>
    /// <param name="compiledFiles">
    /// The <see cref="List{T}"/> of <see cref="CompiledFile" /> objects to write to disk.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    private static async Task WriteFilesToDisk(List<CompiledFile> compiledFiles)
    {
        foreach (CompiledFile compiledFile in compiledFiles)
        {
            await File.WriteAllTextAsync(compiledFile.FilePath, compiledFile.Content, Encoding.UTF8);
        }
    }
}

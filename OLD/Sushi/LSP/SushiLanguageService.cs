using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Serilog;
using Sushi.Diagnostics;
using Sushi.Parsing.Core;
using Sushi.Parsing.Nodes;
using Sushi.Tokenization;

namespace Sushi.LSP;

public sealed class SushiLanguageService
{
    public async Task PushSemanticTokens([NotNull] SemanticTokensBuilder builder, [NotNull] DocumentUri document, [NotNull] SemanticTokensLegend legend) => await new SemanticTokenVisitor(builder, document, legend).Visit(this.tree);
}

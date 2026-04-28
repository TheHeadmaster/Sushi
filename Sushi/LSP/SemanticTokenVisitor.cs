using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sushi.Parsing.Nodes;
using Sushi.Precompilation;
using Sushi.Tokenization;

namespace Sushi.LSP;

public sealed class SemanticTokenVisitor([NotNull] SemanticTokensBuilder builder, [NotNull] DocumentUri document, [NotNull] SemanticTokensLegend legend) : ASTVisitor
{
    protected override async Task VisitTree([NotNull] AbstractSyntaxTree tree)
    {
        foreach (FileNode child in tree.Children)
        {
            if (Uri.Compare(DocumentUri.FromFileSystemPath(child.FilePath).ToUri(), document.ToUri(), UriComponents.Path, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0)
            {
                await this.Visit(child);
                break;
            }
        }
    }

    protected override async Task VisitFile([NotNull] FileNode file)
    {
        foreach (StatementNode statement in file.Statements)
        {
            await this.Visit(statement);
        }
    }


    protected override async Task VisitClass([NotNull] ClassNode classNode)
    {
        await this.Visit(classNode.TypeName!);

        foreach (StatementNode node in classNode.Members)
        {
            await this.Visit(node);
        }
    }

    protected override async Task VisitMemberDeclaration([NotNull] MemberDeclarationNode member)
    {
        if (member.Type?.Name.StartsWith("T", StringComparison.Ordinal) ?? false)
        {
            await this.PushSemanticToken(member.Type);
        }
    }

    private Task PushSemanticToken(TypeNode type)
    {
        Token startToken = type.GetStartToken()!;
        builder.Push(startToken.LineNumber - 1, startToken.LinePosition, startToken.Value.Length, legend.GetTokenTypeIdentity((SemanticTokenType?)SemanticTokenType.TypeParameter), 0);

        return Task.CompletedTask;
    }
}

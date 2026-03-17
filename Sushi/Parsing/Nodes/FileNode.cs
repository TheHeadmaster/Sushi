using System.Diagnostics.CodeAnalysis;
using Sushi.Compilation;
using Sushi.Tokenization;
using Sushi.Verification;

namespace Sushi.Parsing.Nodes;

public class FileNode([NotNull] string filePath, [NotNull] string fileName, [NotNull] List<StatementNode> statements) : SyntaxNode
{
    public string FilePath { get; set; } = filePath;
    public string FileName { get; set; } = fileName;

    public List<StatementNode> Statements { get; set; } = statements;

    public override async Task Compile([NotNull] Compiler compiler)
    {
        await compiler.StartFile(this.FilePath);

        foreach (StatementNode statement in this.Statements)
        {
            await statement.Compile(compiler);
        }

        await this.CompileHeader(compiler);

        await compiler.EndFile();
    }

    public override async Task CompileHeader([NotNull] Compiler compiler)
    {
        string headerGuard = $"__H_{Path.GetFileNameWithoutExtension(this.FileName)}";
        await compiler.WriteHeaderLine($"#ifndef {headerGuard}");
        await compiler.WriteHeaderLine($"#define {headerGuard}");

        foreach (StatementNode statement in this.Statements)
        {
            await statement.CompileHeader(compiler);
        }

        await compiler.WriteHeaderLine("");
        await compiler.WriteHeaderLine("#endif");
    }

    public override Token GetStartToken() => this.Statements.First().GetStartToken();

    public override async Task Verify(VerificationContext context)
    {
        foreach (StatementNode statement in this.Statements)
        {
            await statement.Verify(context);
        }
    }
}

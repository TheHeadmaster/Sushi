using System.Diagnostics.CodeAnalysis;
using Sushi.Compilation;
using Sushi.Diagnostics;
using Sushi.Parsing.Nodes;
using Sushi.Parsing.Scope;
using Sushi.Tokenization;
using Sushi.Verification;

namespace Sushi.Parsing;

/// <summary>
/// Represents the entire syntax tree of the project.
/// </summary>
public sealed class AbstractSyntaxTree : SyntaxNode
{
    public List<FileNode> Children { get; set; } = [];

    public ReferenceScope GlobalScope { get; set; } = null!;

    /// <summary>
    /// Contains the messages emitted by the parser, such as errors and warnings.
    /// </summary>
    public List<CompilerMessage> Messages { get; set; } = [];
    public override Token GetStartToken() => this.Children.First().GetStartToken();
    public override async Task Verify(VerificationContext context)
    {
        foreach (SyntaxNode child in this.Children)
        {
            await child.Verify(context);
        }
    }

    public override async Task Compile([NotNull] Compiler compiler)
    {
        string mainFileName = "main.sus";
        while (this.Children.Any(x => x.FileName.Equals(mainFileName, StringComparison.OrdinalIgnoreCase)))
        {
            mainFileName = $"_{mainFileName}";
        }

        await compiler.StartFile(Path.Combine(AppMeta.Options.ProjectPath, mainFileName));

        await compiler.WriteLine("int main()");
        await compiler.WriteLine("{");
        await compiler.Indent();
        await compiler.WriteLine("return 0;");
        await compiler.Dedent();
        await compiler.WriteLine("}");
        await compiler.EndFile();

        foreach (FileNode child in this.Children)
        {
            await child.Compile(compiler);
        }
    }
}

using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sushi.Parsing;
using Sushi.Parsing.Nodes;

namespace Sushi.Compilation;

/// <summary>
/// Visits each node in a syntax tree and builds a C file from it.
/// </summary>
public sealed class SushiVisitor
{
    private StringBuilder sb = null!;

    int indentLevel = 0;

    private string Pad()
    {
        StringBuilder pad = new();

        for (int i = 0; i < this.indentLevel * 4; i++)
        {
            pad.Append(' ');
        }

        return pad.ToString();
    }

    private void AppendFormatted(string s) => this.sb.Append($"{this.Pad()}{s}");

    private void AppendLineFormatted(string s) => this.sb.AppendLine($"{this.Pad()}{s}");

    public async Task Visit([NotNull] SyntaxNode node)
    {
        if (node.GetType() == typeof(AbstractSyntaxTree))
        {
            await this.VisitRoot((AbstractSyntaxTree)node);
        }
        else if (node.GetType() == typeof(VariableDeclarationNode))
        {
            await this.VisitVariableDeclaration((VariableDeclarationNode)node);
        }
        else if (node.GetType() == typeof(FileNode))
        {
            await this.VisitFile((FileNode)node);
        }
        else if (node.GetType() == typeof(NumberLiteralNode))
        {
            await this.VisitNumberLiteral((NumberLiteralNode)node);
        }
    }

    private Task<string> Compile() => Task.FromResult(this.sb.ToString().Trim());

    private async Task VisitRoot(AbstractSyntaxTree node)
    {
        foreach (SyntaxNode child in node.Children)
        {
            await this.Visit(child);
        }
    }

    private async Task VisitFile(FileNode node)
    {
        this.sb = new StringBuilder();

        this.sb.AppendLine("#include <stdio.h>");
        this.sb.AppendLine("#include <stdint.h>");
        this.sb.AppendLine();

        if (node.FileName == "Program.sus")
        {
            this.sb.AppendLine("int main() {");
        }

        this.indentLevel++;

        string intermediateFolder = Path.Combine(AppMeta.Options.ProjectPath, "intermediate");

        string absoluteFilePath = Path.GetFullPath(node.FilePath);

        Uri fullUri = new(absoluteFilePath);

        string projectPath = AppMeta.Options.ProjectPath.TrimEnd('/', '\\') + Path.DirectorySeparatorChar;

        Uri baseUri = new(projectPath);

        Uri relativeUri = baseUri.MakeRelativeUri(fullUri);

        string relativeFilePath = Uri.UnescapeDataString(relativeUri.ToString()).Replace('/', Path.DirectorySeparatorChar);

        string fileDirectory = Path.GetDirectoryName(Path.Combine(intermediateFolder, relativeFilePath)) ?? string.Empty;

        if (!Directory.Exists(fileDirectory))
        {
            Directory.CreateDirectory(fileDirectory);
        }

        foreach (SyntaxNode child in node.Children)
        {
            await this.Visit(child);
        }

        this.AppendLineFormatted("printf(\"Hello World!\");");
        this.AppendLineFormatted("return 0;");
        this.sb.AppendLine("}");

        this.indentLevel--;

        await File.WriteAllTextAsync(Path.ChangeExtension(Path.Combine(intermediateFolder, relativeFilePath), ".c"), await this.Compile(), Encoding.UTF8);
    }

    private async Task VisitVariableDeclaration(VariableDeclarationNode node)
    {
        this.AppendFormatted($"{Constants.SushiToCTypes[node.Type]} {node.Name}");

        if (node.Assignment is not null)
        {
            this.sb.Append(" = ");
            await this.Visit(node.Assignment);
        }

        this.sb.Append(';');

        this.sb.AppendLine();
    }

    private Task VisitNumberLiteral(NumberLiteralNode node)
    {
        this.sb.Append(node.Value);

        return Task.CompletedTask;
    }
}

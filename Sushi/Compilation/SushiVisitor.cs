using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sushi.Parsing;
using Sushi.Parsing.Nodes;

namespace Sushi.Compilation;

/// <summary>
/// Visits each node in a syntax tree and builds a C file from it.
/// </summary>
public sealed class SushiVisitor : AbstractTreeVisitor
{
    private StringBuilder sb = null!;

    int indentLevel;

    private static readonly List<string> implicitHeaders =
    [
        "stdio",
        "stdint"
    ];

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

    public override async Task VisitParameterList([NotNull] ParameterListNode node)
    {
        this.sb.Append('(');
        this.sb.Append(')');
    }

    public override async Task VisitFunctionBody([NotNull] FunctionBodyNode node)
    {
        if (!node.Statements.Any())
        {
            this.sb.Append(" { }");
            return;
        }

        this.sb.AppendLine();
        this.AppendLineFormatted("{");

        this.indentLevel++;

        foreach (SyntaxNode statement in node.Statements)
        {
            await this.Visit(statement);
        }

        this.indentLevel--;

        this.AppendLineFormatted("}");
    }

    public override async Task VisitExpression([NotNull] ExpressionNode node) => await this.Visit(node.Body!);

    public override Task VisitType([NotNull] TypeNode node)
    {
        this.sb.Append(Constants.SushiToCTypes[node.Name!]);

        return Task.CompletedTask;
    }

    public override Task VisitIdentifier([NotNull] IdentifierNode node)
    {
        this.sb.Append(node.Name);

        return Task.CompletedTask;
    }

    private Task<string> Compile() => Task.FromResult(this.sb.ToString().Trim());

    public override async Task VisitRoot([NotNull] AbstractSyntaxTree node)
    {
        foreach (SyntaxNode child in node.Children)
        {
            await this.Visit(child);
        }
    }

    public override async Task VisitFile([NotNull] FileNode node)
    {
        this.sb = new StringBuilder();

        foreach (string header in implicitHeaders)
        {
            this.sb.AppendLine($"#include <{header}.h>");
        }

        this.sb.AppendLine();

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

        await File.WriteAllTextAsync(Path.ChangeExtension(Path.Combine(intermediateFolder, relativeFilePath), ".c"), await this.Compile(), Encoding.UTF8);
    }

    public override async Task VisitVariableDeclaration([NotNull] VariableDeclarationNode node)
    {
        this.AppendFormatted("");

        await this.Visit(node.Type!);

        this.sb.Append(' ');

        await this.Visit(node.Name!);

        if (node.Assignment is not null)
        {
            this.sb.Append(" = ");
            await this.Visit(node.Assignment);
        }

        this.sb.Append(';');

        this.sb.AppendLine();
    }

    public override Task VisitConstant([NotNull] ConstantNode node)
    {
        this.sb.Append(node.Value);

        return Task.CompletedTask;
    }

    public override async Task VisitFunctionDeclaration([NotNull] FunctionDeclarationNode node)
    {
        this.AppendFormatted("");

        TypeNode type = node.ReturnType!;
        IdentifierNode name = node.Name!;

        if (name.Name == "Main")
        {
            name.Name = "main";

            if (type.Name == "Int32")
            {
                type.Name = "__MAIN_SHADOWED_INT_SPECIAL";
            }
        }

        await this.Visit(node.ReturnType!);

        this.sb.Append(' ');

        await this.Visit(node.Name!);
        await this.Visit(node.Parameters!);
        await this.Visit(node.Body!);

        this.sb.AppendLine();
    }
}

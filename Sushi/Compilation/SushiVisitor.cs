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
        else if (node.GetType() == typeof(ConstantNode))
        {
            await this.VisitConstant((ConstantNode)node);
        }
        else if (node.GetType() == typeof(FunctionDeclarationNode))
        {
            await this.VisitFunctionDeclaration((FunctionDeclarationNode)node);
        }
        else if (node.GetType() == typeof(IdentifierNode))
        {
            await this.VisitIdentifier((IdentifierNode)node);
        }
        else if (node.GetType() == typeof(TypeNode))
        {
            await this.VisitType((TypeNode)node);
        }
        else if (node.GetType() == typeof(ExpressionNode))
        {
            await this.VisitExpression((ExpressionNode)node);
        }
        else if (node.GetType() == typeof(FunctionBodyNode))
        {
            await this.VisitFunctionBody((FunctionBodyNode)node);
        }
        else if (node.GetType() == typeof(ParameterListNode))
        {
            await this.VisitParameterList((ParameterListNode)node);
        }
    }

    private async Task VisitParameterList(ParameterListNode node)
    {
        this.sb.Append('(');
        this.sb.Append(')');
    }

    private async Task VisitFunctionBody(FunctionBodyNode node)
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

    private async Task VisitExpression(ExpressionNode node) => await this.Visit(node.Body!);

    private Task VisitType(TypeNode node)
    {
        this.sb.Append(Constants.SushiToCTypes[node.Name!]);

        return Task.CompletedTask;
    }

    private Task VisitIdentifier(IdentifierNode node)
    {
        this.sb.Append(node.Name);

        return Task.CompletedTask;
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

    private async Task VisitVariableDeclaration(VariableDeclarationNode node)
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

    private Task VisitConstant(ConstantNode node)
    {
        this.sb.Append(node.Value);

        return Task.CompletedTask;
    }

    private async Task VisitFunctionDeclaration(FunctionDeclarationNode node)
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

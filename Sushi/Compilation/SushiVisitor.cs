using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sushi.Parsing;
using Sushi.Parsing.Nodes;

namespace Sushi.Compilation;

/// <summary>
/// Visits each node in a syntax tree and builds an assembly file from it.
/// </summary>
public sealed class SushiVisitor
{
    private StringBuilder sb = null!;

    private readonly List<ASMInitializer> initializers = [];

    private ASMInitializer? currentInitializer;

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

    private Task<string> Compile()
    {
        this.sb.AppendLine("section .data");

        foreach (ASMInitializer initializer in this.initializers)
        {
            initializer.Print(this.sb);
        }

        return Task.FromResult(this.sb.ToString().Trim());
    }

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

        await File.WriteAllTextAsync(Path.ChangeExtension(Path.Combine(intermediateFolder, relativeFilePath), ".asm"), await this.Compile(), Encoding.UTF8);
    }

    private async Task VisitVariableDeclaration(VariableDeclarationNode node)
    {
        this.currentInitializer = new() { Name = node.Name, Type = node.Type };

        if (node.Assignment is not null)
        {
            await this.Visit(node.Assignment);
        }

        this.initializers.Add(this.currentInitializer);
        this.currentInitializer = null;
    }

    private async Task VisitNumberLiteral(NumberLiteralNode node)
    {
        if (this.currentInitializer is not null)
        {
            this.currentInitializer.Value = node.Value;
        }
        else
        {
            
        }
    }
}

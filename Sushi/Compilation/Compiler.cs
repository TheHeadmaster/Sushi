using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Xml.Linq;
using Sushi.Parsing;

namespace Sushi.Compilation;

public sealed class Compiler
{
    private const string GeneratedFileComment = "// Compiler-generated file, do not modify directly as it will be overwritten";
    private static readonly List<string> implicitIncludes =
    [
        "stdint"
    ];

    private StringBuilder sb = new();

    private StringBuilder headerSB = new();

    private int indentLevel;
    private int headerIndentLevel;

    private string intermediateFolder = string.Empty;

    private string absoluteFilePath = string.Empty;

    private string relativeFilePath = string.Empty;
    private bool firstOfLine = true;
    private bool headerFirstOfLine = true;

    public async Task Compile([NotNull] ICompilerNode root)
    {
        DirectoryInfo intermediate = new(Path.Combine(AppMeta.Options.ProjectPath, "intermediate"));

        if (Directory.Exists(intermediate.FullName))
        {
            Directory.Delete(intermediate.FullName, true);
        }

        Directory.CreateDirectory(intermediate.FullName);

        this.intermediateFolder = intermediate.FullName;

        await root.Compile(this);
    }

    public async Task StartFile([NotNull] string filePath)
    {
        this.sb = new StringBuilder();
        this.headerSB = new StringBuilder();
        this.indentLevel = 0;
        this.headerIndentLevel = 0;
        this.absoluteFilePath = Path.GetFullPath(filePath);

        Uri fullUri = new(this.absoluteFilePath);

        string projectPath = $"{AppMeta.Options.ProjectPath.TrimEnd('/', '\\')}{Path.DirectorySeparatorChar}";

        Uri baseUri = new(projectPath);

        Uri relativeUri = baseUri.MakeRelativeUri(fullUri);

        this.relativeFilePath = Uri.UnescapeDataString(relativeUri.ToString()).Replace('/', Path.DirectorySeparatorChar);

        string fileDirectory = Path.GetDirectoryName(Path.Combine(this.intermediateFolder, this.relativeFilePath)) ?? string.Empty;

        if (!Directory.Exists(fileDirectory))
        {
            Directory.CreateDirectory(fileDirectory);
        }
    }

    public async Task EndFile()
    {
        string sbString = this.sb.ToString().Trim();
        string headerSBString = this.headerSB.ToString().Trim();

        if (!string.IsNullOrWhiteSpace(headerSBString))
        {
            StringBuilder tempHeaderSb = new();

            tempHeaderSb.AppendLine(GeneratedFileComment);

            foreach (string include in implicitIncludes)
            {
                tempHeaderSb.AppendLine($"#include <{include}.h>");
            }

            tempHeaderSb.AppendLine();
            tempHeaderSb.Append(headerSBString);

            string fileName = Path.ChangeExtension(
                    Path.Combine(
                        this.intermediateFolder,
                        this.relativeFilePath),
                    ".h");

            await File.WriteAllTextAsync(
                fileName,
                tempHeaderSb.ToString().Trim(),
                Encoding.UTF8);

            if (!string.IsNullOrWhiteSpace(sbString))
            {
                StringBuilder tempSb = new();

                tempSb.AppendLine($"#include \"{Path.ChangeExtension(this.relativeFilePath, ".h")}\"");
                tempSb.AppendLine();
                tempSb.Append(sbString);

                sbString = tempSb.ToString().Trim();
            }
        }

        if (!string.IsNullOrWhiteSpace(sbString))
        {
            StringBuilder tempSb = new();

            tempSb.AppendLine(GeneratedFileComment);

            foreach (string include in implicitIncludes)
            {
                tempSb.AppendLine($"#include <{include}.h>");
            }

            tempSb.AppendLine();
            tempSb.Append(sbString);

            await File.WriteAllTextAsync(
                Path.ChangeExtension(
                    Path.Combine(
                        this.intermediateFolder,
                        this.relativeFilePath),
                    ".c"),
                tempSb.ToString().Trim(),
                Encoding.UTF8);
        }
    }

    private string Pad(bool header = false)
    {
        StringBuilder pad = new();

        for (int i = 0; i < (header ? this.headerIndentLevel : this.indentLevel) * 4; i++)
        {
            pad.Append(' ');
        }

        return pad.ToString();
    }

    public Task WriteLine([NotNull] string line)
    {
        this.sb.AppendLine($"{this.Pad()}{line}");
        this.firstOfLine = true;
        return Task.CompletedTask;
    }

    public Task WriteHeaderLine([NotNull] string line)
    {
        this.headerSB.AppendLine($"{this.Pad(true)}{line}");
        this.headerFirstOfLine = true;
        return Task.CompletedTask;
    }

    public Task Write([NotNull] string text)
    {
        if (this.firstOfLine)
        {
            this.sb.Append($"{this.Pad()}{text}");
        }
        else
        {
            this.sb.Append(text);
        }

        this.firstOfLine = false;

        return Task.CompletedTask;
    }

    public Task WriteHeader([NotNull] string text)
    {
        if (this.headerFirstOfLine)
        {
            this.headerSB.Append($"{this.Pad(true)}{text}");
        }
        else
        {
            this.headerSB.Append(text);
        }

        this.headerFirstOfLine = false;

        return Task.CompletedTask;
    }

    public Task EndLine()
    {
        this.sb.AppendLine();
        this.firstOfLine = true;
        return Task.CompletedTask;
    }

    public Task HeaderEndLine()
    {
        this.headerSB.AppendLine();
        this.headerFirstOfLine = true;
        return Task.CompletedTask;
    }

    public Task Indent()
    {
        this.indentLevel++;
        return Task.CompletedTask;
    }

    public Task Dedent()
    {
        this.indentLevel--;
        return Task.CompletedTask;
    }

    public Task IndentHeader()
    {
        this.headerIndentLevel++;
        return Task.CompletedTask;
    }

    public Task DedentHeader()
    {
        this.headerIndentLevel--;
        return Task.CompletedTask;
    }
}

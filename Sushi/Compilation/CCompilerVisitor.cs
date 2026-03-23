using Sushi.Parsing.Nodes;

namespace Sushi.Compilation;

/// <summary>
/// Compiles an <see cref="AbstractSyntaxTree"/> into C code.
/// </summary>
public sealed class CCompilerVisitor : CompilerVisitor
{
    /// <inheritdoc />
    protected override Task<string> WriteComment(string generatedComment) => Task.FromResult($"// {generatedComment}");

    /*
    private static readonly List<string> implicitIncludes =
    [
        "stdint"
    ];

    public async Task EndFile()
    {
        if (!string.IsNullOrWhiteSpace(headerSBString))
        {
            StringBuilder tempHeaderSb = new();

            tempHeaderSb.AppendLine(GeneratedFileComment);

            foreach (string include in implicitIncludes)
            {
                tempHeaderSb.AppendLine($"#include <{include}.h>");
            }

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

    */

}
using Sushi.Parsing.Nodes;

namespace Sushi.Compilation;

/// <summary>
/// Compiles an <see cref="AbstractSyntaxTree"/> into C code.
/// </summary>
public sealed class CCompilerVisitor : CompilerVisitor
{
    /// <inheritdoc />
    protected override Task<string> WriteComment(string generatedComment) => Task.FromResult($"// {generatedComment}");
}
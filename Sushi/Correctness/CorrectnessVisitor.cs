using System.Diagnostics.CodeAnalysis;
using Sushi.Compilation;
using Sushi.Parsing;
using Sushi.Parsing.Nodes;

namespace Sushi.Correctness;

public sealed class CorrectnessVisitor : AbstractTreeVisitor
{
    public override async Task VisitRoot([NotNull] AbstractSyntaxTree node)
    {
        foreach (SyntaxNode child in node.Children)
        {
            await this.Visit(child);
        }
    }

    public override async Task VisitFile([NotNull] FileNode node)
    {
        foreach (SyntaxNode child in node.Children)
        {
            await this.Visit(child);
        }
    }

    public override async Task VisitVariableDeclaration([NotNull] VariableDeclarationNode node) => await new AssignmentTypeChecker().Visit(node);

    public override async Task VisitFunctionDeclaration([NotNull] FunctionDeclarationNode node)
    {
        await this.Visit(node.Parameters!);
        await this.Visit(node.Body!);
    }

    public static Task<List<CompilerError>> CollectErrors() => Task.FromResult(Errors);
}

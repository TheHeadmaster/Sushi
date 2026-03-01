using System.Diagnostics.CodeAnalysis;
using System.Reflection.Metadata;
using Sushi.Compilation;
using Sushi.Parsing;
using Sushi.Parsing.Nodes;

namespace Sushi.Correctness;

public sealed class NameCollisionChecker : AbstractTreeVisitor
{
    private readonly Dictionary<string, List<SyntaxNode>> names = [];

    public override async Task VisitFunctionDeclaration([NotNull] FunctionDeclarationNode node)
    {
        await this.Visit(node.Body!);
        await this.Visit(node.Parameters!);

        foreach (KeyValuePair<string, List<SyntaxNode>> entry in this.names)
        {
            if (entry.Value.Count > 1)
            {
                foreach (SyntaxNode offendingNode in entry.Value)
                {
                    GenerateError(offendingNode, "Name collision detected.");
                }
            }
        }
    }

    public override async Task VisitFunctionBody([NotNull] FunctionBodyNode node)
    {
        foreach (SyntaxNode statement in node.Statements)
        {
            await this.Visit(statement);
        }
    }

    public override Task VisitParameterList([NotNull] ParameterListNode node)
    {
        foreach (ParameterNode parameter in node.Parameters)
        {
            if (this.names.ContainsKey(parameter.Name!.Name!))
            {
                this.names[parameter.Name.Name!].Add(parameter.Name);
            }
            else
            {

                this.names[parameter.Name!.Name!] = [parameter.Name!];
            }
        }

        return Task.CompletedTask;
    }

    public override Task VisitVariableDeclaration([NotNull] VariableDeclarationNode node)
    {
        if (this.names.ContainsKey(node.Name!.Name!))
        {
            this.names[node.Name.Name!].Add(node.Name);
        }
        else
        {

            this.names[node.Name!.Name!] = [node.Name!];
        }

        return Task.CompletedTask;
    }
}

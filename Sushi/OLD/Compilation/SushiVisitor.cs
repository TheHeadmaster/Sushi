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



    private void AppendFormatted(string s) => this.sb.Append($"{this.Pad()}{s}");

    private void AppendLineFormatted(string s) => this.sb.AppendLine($"{this.Pad()}{s}");

    public override async Task VisitParameterList([NotNull] ParameterListNode node)
    {
        this.sb.Append('(');

        bool isFirst = true;

        foreach (ParameterNode parameter in node.Parameters)
        {
            if (!isFirst)
            {
                this.sb.Append(", ");
            }

            isFirst = false;

            await this.Visit(parameter);
        }

        this.sb.Append(')');
    }

    public override async Task VisitParameter([NotNull] ParameterNode node)
    {
        await this.Visit(node.Type);
        this.sb.Append(' ');
        await this.Visit(node.Name);
    }

    public override async Task VisitBlock([NotNull] BlockNode node)
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

    public override async Task VisitAssignment(AssignmentNode node)
    {
        this.AppendFormatted("");

        IdentifierNode name = node.Identifier!;

        await this.Visit(node.Identifier!);

        this.sb.Append(" = ");

        await this.Visit(node.RightHandSide!);

        this.sb.Append(';');

        this.AppendLineFormatted("");
    }

    public override async Task VisitMethodCall(MethodCallNode node)
    {
        await this.Visit(node.Identifier);
        await this.Visit(node.Arguments);
    }

    public override async Task VisitArgumentList(ArgumentListNode node)
    {
        this.sb.Append('(');

        bool isFirst = true;

        foreach (ArgumentNode argument in node.Arguments)
        {
            if (!isFirst)
            {
                this.sb.Append(", ");
            }

            isFirst = false;

            await this.Visit(argument);
        }

        this.sb.Append(')');
    }

    public override async Task VisitArgument(ArgumentNode node)
    {
        await this.Visit(node.Argument);
    }

    public override async Task VisitIf(IfNode node)
    {
        this.AppendFormatted("if (");

        await this.Visit(node.Expression);

        this.sb.Append(')');

        await this.Visit(node.Body!);

        this.sb.AppendLine();
    }
}

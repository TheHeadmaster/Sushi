using System.Diagnostics.CodeAnalysis;
using System.Text;
using Serilog.Parsing;
using Sushi.Parsing.Core;
using Sushi.Parsing.Nodes;

namespace Sushi.Compilation;

/// <summary>
/// Compiles an <see cref="AbstractSyntaxTree"/> into C code.
/// </summary>
public sealed class CCompilerVisitor : CompilerVisitor
{
    /// <summary>
    /// Includes that are prepended to the beginning of every C file.
    /// </summary>
    private static readonly List<string> implicitIncludes =
    [
        "stdint",
        "core"
    ];

    /// <summary>
    /// The current ID used to deconflict header guards.
    /// </summary>
    private int currentHeaderGuardID;

    /// <summary>
    /// Whether the current file has a header file or not.
    /// </summary>
    private bool hasHeader;

    /// <summary>
    /// The name of the class definition currently in context. Used to prepend the name to method declarations.
    /// </summary>
    private string currentClassName = string.Empty;

    /// <inheritdoc />
    protected override Task<string> WriteComment(string generatedComment) => Task.FromResult($"// {generatedComment}");

    /// <inheritdoc />
    protected override async Task<string> WritePrepend()
    {
        StringBuilder sb = new();

        if (this.IsWritingHeader)
        {
            sb.AppendLine("");
            sb.AppendLine("// Include Guard");
            string headerGuard = $"__H_{this.currentHeaderGuardID:0000}";
            this.currentHeaderGuardID++;
            sb.AppendLine($"#ifndef {headerGuard}");
            sb.AppendLine($"#define {headerGuard}");
        }

        sb.AppendLine("");
        sb.AppendLine("// Implicit includes");
        foreach (string include in implicitIncludes)
        {
            sb.AppendLine($"#include <{include}.h>");
        }

        if (!this.IsWritingHeader && this.hasHeader)
        {
            sb.AppendLine("");
            sb.AppendLine("// Include its own header file");
            sb.AppendLine($"#include \"{Path.ChangeExtension(this.RelativeFilePath, "h")}\"");
        }

        return sb.ToString();
    }

    /// <inheritdoc />
    protected override async Task<string> WriteAppend()
    {
        StringBuilder sb = new();

        if (this.IsWritingHeader)
        {
            sb.AppendLine("");
            sb.AppendLine("// End include guard");
            sb.AppendLine("#endif");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Whether the current file being written to is a header file.
    /// </summary>
    private bool IsWritingHeader => this.CurrentFile.FileExtension == ".h";

    /// <inheritdoc />
    protected override async Task VisitTree([NotNull] AbstractSyntaxTree tree)
    {
        string mainFileName = "main.sus";
        while (tree.Children.Any(x => x.FileName.Equals(mainFileName, StringComparison.OrdinalIgnoreCase)))
        {
            mainFileName = $"_{mainFileName}";
        }

        string mainPath = await this.ConvertSourcePathToIntermediatePath(Path.Combine(AppMeta.Options.ProjectPath, mainFileName), "c");

        await this.StartFile(mainPath);

        await this.WriteLine("// This is a bootstrap entry point that calls the author's real entry point");

        await this.WriteLine("int main()");
        await this.WriteLine("{");
        await this.Indent();
        await this.WriteLine("return 0;");
        await this.Dedent();
        await this.WriteLine("}");
        
        await this.EndFile();

        foreach (FileNode child in tree.Children)
        {
            await this.Visit(child);
        }
    }

    /// <inheritdoc />
    protected override async Task VisitFile([NotNull] FileNode file)
    {

        string hPath = await this.ConvertSourcePathToIntermediatePath(file.FilePath, "h");

        await this.StartFile(hPath);

        foreach (StatementNode statement in file.Statements)
        {
            await this.Visit(statement);
        }

        this.hasHeader = await this.EndFile();

        string cPath = await this.ConvertSourcePathToIntermediatePath(file.FilePath, "c");
        await this.StartFile(cPath);

        foreach (StatementNode statement in file.Statements)
        {
            await this.Visit(statement);
        }

        await this.EndFile();
    }

    /// <inheritdoc />
    protected override async Task VisitAssignment([NotNull] AssignmentNode assignment)
    {
        if (assignment.Identifier is not null)
        {
            await this.Visit(assignment.Identifier);
        }

        await this.Write(" = ");

        if (assignment.Right is not null)
        {
            await this.Visit(assignment.Right);
        }
    }

    /// <inheritdoc />
    protected override async Task VisitClass([NotNull] ClassNode classNode)
    {
        if (classNode.TypeName is not null)
        {
            this.currentClassName = classNode.TypeName.Name;
        }

        if (this.IsWritingHeader)
        {
            await this.WriteLine("typedef struct");
            await this.WriteLine("{");

            await this.Indent();

            foreach (StatementNode node in classNode.Members)
            {
                await this.Visit(node);
            }

            await this.Dedent();

            await this.Write("} ");

            if (classNode.TypeName is not null)
            {
                await this.Visit(classNode.TypeName);
            }
        }
        else
        {
            foreach (StatementNode node in classNode.Members)
            {
                await this.Visit(node);
            }
        }
    }

    /// <inheritdoc />
    protected override async Task VisitMemberDeclaration([NotNull] MemberDeclarationNode member)
    {
        if (this.IsWritingHeader)
        {
            if (member.Type is not null)
            {
                await this.Visit(member.Type);
                await this.Write(" ");
            }

            if (member.Identifier is not null)
            {
                await this.Visit(member.Identifier);
            }

            await this.Write(";");
            await this.EndLine();
        }
    }

    /// <inheritdoc />
    protected override async Task VisitMethodDeclaration([NotNull] MethodDeclarationNode method)
    {
        if (this.IsWritingHeader)
        {
            if (method.ReturnType is not null)
            {
                await this.Visit(method.ReturnType);
            }
            else
            {
                await this.Write("void");
            }

            await this.Write(" (*");

            if (method.Name is not null)
            {
                await this.Write($"__{this.currentClassName}_");
                await this.Visit(method.Name);
            }

            await this.Write(")");

            if (method.ParameterList is not null)
            {
                await this.Visit(method.ParameterList);
            }

            await this.Write(";");
            await this.EndLine();
        }
        else
        {
            if (method.ReturnType is not null)
            {
                await this.Visit(method.ReturnType);
            }
            else
            {
                await this.Write("void");
            }

            await this.Write(" ");

            if (method.Name is not null)
            {
                await this.Write($"__{this.currentClassName}_");
                await this.Visit(method.Name);
            }

            if (method.ParameterList is not null)
            {
                await this.Visit(method.ParameterList);
            }
            else
            {
                await this.Write("()");
            }

            await this.EndLine();

            if (method.Body is not null)
            {
                await this.Visit(method.Body);
            }
        }
    }

    /// <inheritdoc />
    protected override async Task VisitType([NotNull] TypeNode type)
    {
        string resolvedName = type.ResolvedType is null ? type.Name : !type.ResolvedType.IsReferenceType() ? Constants.SushiToCConversions[type.ResolvedType.Name] : type.ResolvedType.FullName.Replace('.', '_');

        await this.Write(resolvedName);
    }

    /// <inheritdoc />
    protected override async Task VisitIdentifier([NotNull] IdentifierNode identifier) => await this.Write(identifier.Name);

    /// <inheritdoc />
    protected override async Task VisitParameterList([NotNull] ParameterListNode parameterList)
    {
        await this.Write("(");

        bool isFirst = true;

        foreach (ParameterNode parameter in parameterList.Parameters)
        {
            if (!isFirst)
            {
                await this.Write(", ");
            }
            else
            {
                isFirst = false;
            }

            await this.Visit(parameter);
        }

        await this.Write(")");
    }

    /// <inheritdoc />
    protected override async Task VisitParameter([NotNull] ParameterNode parameter)
    {
        if (parameter.Type is not null)
        {
            await this.Visit(parameter.Type);
        }

        await this.Write(" ");

        if (parameter.Name is not null)
        {
            await this.Visit(parameter.Name);
        }
    }

    /// <inheritdoc />
    protected override async Task VisitBlock([NotNull] BlockNode block)
    {
        await this.WriteLine("{");
        await this.Indent();

        foreach (StatementNode statement in block.Statements)
        {
            await this.Visit(statement);
        }

        await this.Dedent();
        await this.WriteLine("}");
    }

    /// <inheritdoc />
    protected override async Task VisitBinary([NotNull] BinaryExpressionNode binary)
    {
        await this.Write("(");
        if (binary.Left is not null)
        {
            await this.Visit(binary.Left);
        }

        string operatorString = binary.Operator switch
        {
            OperatorType.Add => "+",
            OperatorType.Subtract => "-",
            OperatorType.Multiply => "*",
            OperatorType.Divide => "/",
            _ => string.Empty
        };

        await this.Write($" {operatorString} ");

        if (binary.Right is not null)
        {
            await this.Visit(binary.Right);
        }

        await this.Write(")");
    }

    /// <inheritdoc />
    protected override async Task VisitConstant([NotNull] ConstantNode constant) => await this.Write(constant.Value);

    /// <inheritdoc />
    protected override async Task VisitDestroyerDeclaration([NotNull] DestroyerDeclarationNode destroyer)
    {
        if (this.IsWritingHeader)
        {
            await this.Write("void");

            await this.Write(" (*");

            if (destroyer.Name is not null)
            {
                await this.Write($"__{this.currentClassName}_destroyer_");
                await this.Visit(destroyer.Name);
            }

            await this.Write(")");

            if (destroyer.ParameterList is not null)
            {
                await this.Visit(destroyer.ParameterList);
            }

            await this.Write(";");
            await this.EndLine();
        }
        else
        {
            await this.Write("void");

            await this.Write(" ");

            if (destroyer.Name is not null)
            {
                await this.Write($"__{this.currentClassName}_destroyer_");
                await this.Visit(destroyer.Name);
            }

            if (destroyer.ParameterList is not null)
            {
                await this.Visit(destroyer.ParameterList);
            }
            else
            {
                await this.Write("()");
            }

            await this.EndLine();

            if (destroyer.Body is not null)
            {
                await this.Visit(destroyer.Body);
            }
        }
    }

    /// <inheritdoc />
    protected override async Task VisitDestroy([NotNull] DestroyNode destroy)
    {
        if (destroy.Object is not null)
        {
            await this.Visit(destroy.Object);
        }

        await this.Write(".");

        if (destroy.Destroyer is not null)
        {
            await this.Visit(destroy.Destroyer);
        }

        await this.Write(";");

        await this.EndLine();
    }

    /// <inheritdoc />
    protected override async Task VisitDoWhile([NotNull] DoWhileNode doWhile)
    {
        await this.WriteLine("do");
        await this.WriteLine("{");

        await this.Indent();

        if (doWhile.Body is not null)
        {
            await this.Visit(doWhile.Body);
        }

        await this.Dedent();

        await this.Write("} while (");

        if (doWhile.Condition is not null)
        {
            await this.Visit(doWhile.Condition);
        }

        await this.Write(");");

        await this.EndLine();
    }

    /// <inheritdoc />
    protected override async Task VisitExpressionStatement([NotNull] ExpressionStatementNode expression)
    {
        if (expression.Expression is not null)
        {
            await this.Visit(expression.Expression);
        }

        await this.Write(";");

        await this.EndLine();
    }

    /// <inheritdoc />
    protected override async Task VisitIf([NotNull] IfNode ifNode)
    {
        if (ifNode.Condition is not null)
        {
            await this.Write("if (");

            await this.Visit(ifNode.Condition);

            await this.Write(")");
        }

        await this.EndLine();
        await this.WriteLine("{");

        await this.Indent();

        if (ifNode.Body is not null)
        {
            await this.Visit(ifNode.Body);
        }

        await this.Dedent();

        await this.WriteLine("}");

        if (ifNode.Else is not null)
        {
            await this.Write("else ");
            await this.Visit(ifNode.Else);
        }
    }

    /// <inheritdoc />
    protected override async Task VisitMethodCall([NotNull] MethodCallNode method)
    {
        if (method.Method is not null)
        {
            await this.Visit(method.Method);
        }

        await this.Write("(");

        bool isFirst = true;

        foreach (ExpressionNode argument in method.Arguments)
        {
            if (!isFirst)
            {
                await this.Write(", ");
            }
            else
            {
                isFirst = false;
            }

            await this.Visit(argument);
        }

        await this.Write(")");
    }

    /// <inheritdoc />
    protected override Task VisitNamespaceDeclaration([NotNull] NamespaceDeclarationNode namespaceDeclaration) => Task.CompletedTask;

    /// <inheritdoc />
    protected override Task VisitNamespace([NotNull] NamespaceNode namespaceNode) => Task.CompletedTask;

    /// <inheritdoc />
    protected override async Task VisitUnary([NotNull] UnaryExpressionNode unary)
    {
        string operatorString = unary.Operator switch
        {
            OperatorType.Negative => "-",
            _ => string.Empty
        };

        if (unary.IsPrefix)
        {
            await this.Write(operatorString);
        }

        if (unary.Operand is not null)
        {
            await this.Visit(unary.Operand);
        }

        if (!unary.IsPrefix)
        {
            await this.Write(operatorString);
        }
    }

    /// <inheritdoc />
    protected override async Task VisitUsing([NotNull] UsingNode usingNode)
    {
        await this.WriteLine("// Expanded using statements");
        foreach (string namespaceString in usingNode.ResolvedNamespaces)
        {
            foreach (string path in await this.Reference.GetNamespaceFilePaths(namespaceString))
            {
                await this.WriteLine($"#include \"{Path.ChangeExtension(path, "h")}\"");
            }
        }

        await this.WriteLine("");
    }

    /// <inheritdoc />
    protected override async Task VisitVariableDeclaration([NotNull] VariableDeclarationNode variable)
    {
        if (variable.Type is not null)
        {
            await this.Visit(variable.Type);
        }    

        if (variable.Assignment is not null)
        {
            await this.Write(" = ");
            await this.Visit(variable.Assignment);
        }

        await this.Write(";");
        await this.EndLine();
    }

    /// <inheritdoc />
    protected override async Task VisitWhile([NotNull] WhileNode whileNode)
    {
        await this.Write("while (");

        if (whileNode.Condition is not null)
        {
            await this.Visit(whileNode.Condition);
        }

        await this.Write(")");
        await this.EndLine();

        await this.WriteLine("{");

        await this.Indent();

        if (whileNode.Body is not null)
        {
            await this.Visit(whileNode.Body);
        }

        await this.Dedent();

        await this.WriteLine("}");
    }

    /// <inheritdoc />
    protected override async Task VisitCreate([NotNull] CreateNode create)
    {
        await this.Write("malloc(sizeof(struct ");
        
        if (create.Type is not null)
        {
            await this.Visit(create.Type);
        }

        await this.Write("))");
    }

    /// <inheritdoc />
    protected override async Task VisitCreatorDeclaration([NotNull] CreatorDeclarationNode creator)
    {
        if (this.IsWritingHeader)
        {
            await this.Write("void");

            await this.Write(" (*");

            await this.Write($"__{this.currentClassName}_creator");

            await this.Write(")");

            if (creator.ParameterList is not null)
            {
                await this.Visit(creator.ParameterList);
            }

            await this.Write(";");
            await this.EndLine();
        }
        else
        {
            await this.Write("void");

            await this.Write(" ");

            await this.Write($"__{this.currentClassName}_creator");

            if (creator.ParameterList is not null)
            {
                await this.Visit(creator.ParameterList);
            }
            else
            {
                await this.Write("()");
            }

            await this.EndLine();

            if (creator.Body is not null)
            {
                await this.Visit(creator.Body);
            }
        }
    }
}
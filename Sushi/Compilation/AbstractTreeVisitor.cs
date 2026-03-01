using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sushi.Lexing.Tokenization;
using Sushi.Parsing;
using Sushi.Parsing.Nodes;

namespace Sushi.Compilation;

public abstract class AbstractTreeVisitor
{
    public static List<CompilerError> Errors { get; } = [];

    protected static void GenerateError([NotNull] SyntaxNode node, string message)
    {
        Errors.Add(new CompilerError(new Token()
        {
            LineNumber = node.LineNumber,
            LinePosition = node.LinePosition,
            CurrentLine = node.CurrentLine,
            Type = TokenType.Unknown,
            Value = "".PadLeft(node.Length)
        })
        {
            ErrorReason = message
        });
    }

    public virtual async Task Visit([NotNull] SyntaxNode node)
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
        else if (node.GetType() == typeof(ParameterNode))
        {
            await this.VisitParameter((ParameterNode)node);
        }
    }

    public virtual Task VisitParameter([NotNull] ParameterNode node) => Task.CompletedTask;
    public virtual Task VisitParameterList([NotNull] ParameterListNode node) => Task.CompletedTask;

    public virtual Task VisitFunctionBody([NotNull] FunctionBodyNode node) => Task.CompletedTask;

    public virtual Task VisitExpression([NotNull] ExpressionNode node) => Task.CompletedTask;

    public virtual Task VisitType([NotNull] TypeNode node) => Task.CompletedTask;

    public virtual Task VisitIdentifier([NotNull] IdentifierNode node) => Task.CompletedTask;

    public virtual Task VisitRoot([NotNull] AbstractSyntaxTree node) => Task.CompletedTask;
    public virtual Task VisitFile([NotNull] FileNode node) => Task.CompletedTask;

    public virtual Task VisitVariableDeclaration([NotNull] VariableDeclarationNode node) => Task.CompletedTask;

    public virtual Task VisitConstant([NotNull] ConstantNode node) => Task.CompletedTask;

    public virtual Task VisitFunctionDeclaration([NotNull] FunctionDeclarationNode node) => Task.CompletedTask;
}

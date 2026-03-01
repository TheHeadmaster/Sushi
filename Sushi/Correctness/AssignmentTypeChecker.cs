using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Sushi.Compilation;
using Sushi.Parsing.Nodes;

namespace Sushi.Correctness;

public sealed class AssignmentTypeChecker : AbstractTreeVisitor
{
    public override Task VisitVariableDeclaration([NotNull] VariableDeclarationNode node)
    {
        TypeNode type = node.Assignment!.EvaluateType();

        if (node.Type!.Name != type.Name)
        {
            GenerateError(type, "Type mismatch.");
        }

        return Task.CompletedTask;
    }
}

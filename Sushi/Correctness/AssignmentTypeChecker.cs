using System.Diagnostics.CodeAnalysis;
using Sushi.Compilation;
using Sushi.Parsing;
using Sushi.Parsing.Nodes;

namespace Sushi.Correctness;

public sealed class AssignmentTypeChecker : AbstractTreeVisitor
{
    public override Task VisitVariableDeclaration([NotNull] VariableDeclarationNode node)
    {
        if (node.Assignment is null)
        {
            return Task.CompletedTask;
        }

        SushiType? assignmentType = node.Assignment.EvaluateType();
        SushiType? declarationType = node.Type!.EvaluateType();

        if (assignmentType is null)
        {
            GenerateError(node.Assignment, "Could not resolve type in assignment declaration.");

            return Task.CompletedTask;
        }

        if (declarationType is null)
        {
            GenerateError(node.Type, "Could not resolve type in assignment declaration.");

            return Task.CompletedTask;
        }

        if (!declarationType.IsValidAssignment(assignmentType))
        {
            GenerateError(node.Assignment, "Type mismatch.");
        }

        return Task.CompletedTask;
    }

    public override Task VisitAssignment(AssignmentNode node)
    {
        SushiType? identifierType = node.EvaluateType();
        SushiType? rightHandSideType = node.RightHandSide!.EvaluateType();

        if (identifierType is null || rightHandSideType is null)
        {
            GenerateError(node, "Could not resolve type in assignment.");

            return Task.CompletedTask;
        }

        if (!identifierType.IsValidAssignment(rightHandSideType))
        {
            GenerateError(node.RightHandSide, "Type mismatch.");
        }

        return Task.CompletedTask;
    }
}

using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents a variable declaration.
/// </summary>
/// <param name="type">
/// The type of the variable.
/// </param>
/// <param name="assignment">
/// The assignment expression of the variable.
/// </param>
public sealed class VariableDeclarationNode(TypeNode? type, AssignmentNode? assignment) : StatementNode
{
    /// <summary>
    /// The type of the variable.
    /// </summary>
    public TypeNode? Type { get; set; } = type;

    /// <summary>
    /// The assignment expression of the variable.
    /// </summary>
    public AssignmentNode? Assignment { get; set; } = assignment;

    /// <inheritdoc />
    public override Token? GetStartToken() => this.Type?.GetStartToken();
}

using System.Diagnostics.CodeAnalysis;
using Sushi.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents a method declaration.
/// </summary>
/// <param name="token">
/// The start token of the method declaration.
/// </param>
/// <param name="returnType">
/// The return type of the method declaration.
/// </param>
/// <param name="name">
/// The name of the method.
/// </param>
/// <param name="parameterList">
/// The list of parameters defined on the method.
/// </param>
/// <param name="body">
/// The body of the method, which contains its statements.
/// </param>
public class MethodDeclarationNode([NotNull] Token token, TypeNode? returnType, IdentifierNode? name, ParameterListNode? parameterList, BlockNode? body) : StatementNode
{
    /// <summary>
    /// The return type of the method declaration.
    /// </summary>
    public TypeNode? ReturnType { get; set; } = returnType;

    /// <summary>
    /// The name of the method.
    /// </summary>
    public IdentifierNode? Name { get; set; } = name;

    /// <summary>
    /// The list of parameters defined on the method.
    /// </summary>
    public ParameterListNode? ParameterList { get; set; } = parameterList;

    /// <summary>
    /// The body of the method, which contains its statements.
    /// </summary>
    public BlockNode? Body { get; set; } = body;

    /// <inheritdoc />
    public override Token GetStartToken() => token;
}

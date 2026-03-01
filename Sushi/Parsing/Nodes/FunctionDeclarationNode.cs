using System.Diagnostics.CodeAnalysis;
using Sushi.Lexing.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents a function declaration.
/// </summary>
/// <param name="startToken">
/// The <see cref="Token"/> used to mark the start of the node.
/// </param>
/// <param name="scope">
/// The scope that the node exists in.
/// </param>
public sealed class FunctionDeclarationNode(Token startToken, ReferenceScope scope) : SyntaxNode(startToken, scope)
{
    /// <summary>
    /// The return type of the function.
    /// </summary>
    public TypeNode? ReturnType { get; set; }

    /// <summary>
    /// The name of the function.
    /// </summary>
    public IdentifierNode? Name { get; set; }

    /// <summary>
    /// The statements inside of the body of the function.
    /// </summary>
    public FunctionBodyNode? Body { get; set; }

    /// <summary>
    /// The parameters of the function.
    /// </summary>
    public ParameterListNode? Parameters { get; set; }

    private readonly ReferenceScope functionScope = new(scope);

    /// <inheritdoc />
    public override async Task<bool> Visit([NotNull] ParsingContext context)
    {
        Token token = context.Peek()!;

        if (token is null)
        {
            context.Errors.Add(new CompilerError(context.EndOfFileToken())
            {
                ErrorReason = "Unexpected end of file."
            });

            return false;
        }

        if (this.ReturnType is null)
        {
            TypeNode type = new(token, this.Scope);
            if (!await type.Visit(context))
            {
                return false;
            }

            this.ReturnType = type;

            return await this.Visit(context);
        }

        if (this.Name is null)
        {
            IdentifierNode name = new(token, this.Scope);
            if (!await name.Visit(context))
            {
                return false;
            }

            this.Name = name;

            return await this.Visit(context);
        }

        if (this.Parameters is null)
        {
            ParameterListNode parameters = new(token, this.functionScope);

            if (!await parameters.Visit(context))
            {
                return false;
            }

            this.Parameters = parameters;

            return await this.Visit(context);
        }

        if (this.Body is null)
        {
            FunctionBodyNode body = new(token, this.functionScope);

            if (!await body.Visit(context))
            {
                return false;
            }

            this.Body = body;

            return await this.Visit(context);
        }

        return true;
    }
}
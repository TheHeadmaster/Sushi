using System.Diagnostics.CodeAnalysis;
using Sushi.Lexing.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents a constant literal value, such as <see cref="true"/> or <c>1.05f</c>.
/// </summary>
/// <param name="startToken">
/// The <see cref="Token"/> used to mark the start of the node.
/// </param>
/// <param name="scope">
/// The scope that the node exists in.
/// </param>
public sealed class ConstantNode(Token startToken, ReferenceScope scope) : ExpressionableNode(startToken, scope)
{
    /// <summary>
    /// The type of the constant.
    /// </summary>
    public TypeNode? Type { get; set; }

    /// <summary>
    /// The value of the constant.
    /// </summary>
    public string? Value { get; set; }

    /// <inheritdoc />
    public override SushiType? EvaluateType() => this.Scope.ResolveType(this.Type!.Name!);

    /// <inheritdoc />
    public override Task<bool> VisitConstant([NotNull] ParsingContext context)
    {
        Token token = context.Peek()!;

        if (Constants.BooleanLiterals.Contains(token.Value))
        {
            this.Type = new TypeNode(token, this.Scope)
            {
                Name = "Boolean"
            };

            this.Value = token.Value;

            context.Pop();

            return Task.FromResult(true);
        }

        if (int.TryParse(token.Value, out _))
        {
            this.Type = new TypeNode(token, this.Scope)
            {
                Name = "Int32"
            };

            this.Value = token.Value;

            context.Pop();

            return Task.FromResult(true);
        }
        else if (float.TryParse(token.Value, out _))
        {
            this.Type = new TypeNode(token, this.Scope)
            {
                Name = "Float32"
            };

            this.Value = token.Value;

            context.Pop();

            return Task.FromResult(true);
        }

        context.Errors.Add(new CompilerError(token)
        {
            ErrorReason = "Invalid constant."
        });

        return Task.FromResult(false);
    }
}

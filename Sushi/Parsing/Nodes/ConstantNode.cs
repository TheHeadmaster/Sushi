using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Newtonsoft.Json.Linq;
using Sushi.Lexing.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents a constant literal value, such as <see cref="true"/> or <c>1.05f</c>.
/// </summary>
public sealed class ConstantNode(Token startToken) : ExpressionableNode(startToken)
{
    /// <summary>
    /// The type of the constant.
    /// </summary>
    public TypeNode? Type { get; set; }

    /// <summary>
    /// The value of the constant.
    /// </summary>
    public string? Value { get; set; }

    public override TypeNode EvaluateType() => this.Type!;

    /// <inheritdoc />
    public override Task<bool> VisitConstant([NotNull] ParsingContext context)
    {
        Token token = context.Peek()!;

        if (int.TryParse(this.Value, out _))
        {
            this.Type = new TypeNode(token)
            {
                Name = "Int32"
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

    /// <inheritdoc />
    public override async Task<bool> VisitKeyword([NotNull] ParsingContext context)
    {
        Token token = context.Peek()!;

        if (Constants.BooleanLiterals.Contains(token.Value))
        {
            this.Type = new TypeNode(token)
            {
                Name = "Boolean"
            };
            this.Value = token.Value;
        }

        context.Errors.Add(new CompilerError(token)
        {
            ErrorReason = "Unexpected keyword in variable assignment."
        });

        return false;
    }
}

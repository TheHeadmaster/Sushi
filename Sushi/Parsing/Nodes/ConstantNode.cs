using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json.Linq;
using Sushi.Lexing.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents a constant literal value, such as <see cref="true"/> or <c>1.05f</c>.
/// </summary>
public sealed class ConstantNode(Token startToken) : SyntaxNode(startToken)
{
    /// <summary>
    /// The value of the constant.
    /// </summary>
    public string? Value { get; set; }

    /// <inheritdoc />
    public override Task<bool> VisitConstant([NotNull] ParsingContext context)
    {
        this.Value = context.Peek()!.Value;

        context.Pop();

        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public override async Task<bool> VisitKeyword([NotNull] ParsingContext context)
    {
        Token token = context.Peek()!;

        if (Constants.BooleanLiterals.Contains(token.Value))
        {
            this.Value = token.Value;
        }

        context.Errors.Add(new CompilerError(token)
        {
            ErrorReason = "Unexpected keyword in variable assignment."
        });

        return false;
    }
}

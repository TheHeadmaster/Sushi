using System.Diagnostics.CodeAnalysis;
using Sushi.Lexing.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents an identifier that is defined somewhere else in the code.
/// </summary>
/// <param name="startToken">
/// The <see cref="Token"/> used to mark the start of the node.
/// </param>
/// <param name="scope">
/// The scope that the node exists in.
/// </param>
public sealed class IdentifierNode(Token startToken, ReferenceScope scope, bool isFunction) : ExpressionableNode(startToken, scope)
{
    public TypeNode? Type { get; private set; }

    /// <summary>
    /// The name of the identifier.
    /// </summary>
    public string? Name { get; set; }

    public bool IsFunction { get; set; } = isFunction;

    /// <inheritdoc />
    public override Task<bool> VisitIdentifier([NotNull] ParsingContext context)
    {
        Token token = context.Peek()!;

        this.Name = token.Value;

        this.LineNumber = token.LineNumber;
        this.LinePosition = token.LinePosition;
        this.CurrentLine = token.CurrentLine;
        this.Length = token.Value.Length;

        context.Pop();

        return Task.FromResult(true);
    }

    public bool AssignType([NotNull] ParsingContext context, TypeNode type)
    {
        this.Type = type;

        if (!this.Scope.TryAddIdentifier(this.Name!, this.Type!.Name!, this.IsFunction))
        {
            context.Errors.Add(new CompilerError(startToken)
            {
                ErrorReason = "Name collision."
            });

            return false;
        }

        return true;
    }

    /// <inheritdoc />
    public override SushiType? EvaluateType()
    {
        if (this.Type is not null)
        {
            return this.Type.EvaluateType();
        }

        SushiIdentifier? identifier = this.Scope.ResolveIdentifier(this.Name!, this.IsFunction);

        return identifier is null ? null : this.Scope.ResolveType(identifier!.Type);
    }
}

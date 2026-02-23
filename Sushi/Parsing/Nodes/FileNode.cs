using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json.Linq;
using Sushi.Lexing.Tokenization;

namespace Sushi.Parsing.Nodes;

/// <summary>
/// Represents a source file that contains <see cref="SyntaxNode"/> objects.
/// </summary>
public sealed class FileNode() : SyntaxNode(null)
{
    /// <summary>
    /// The child nodes in the file.
    /// </summary>
    public List<SyntaxNode> Children { get; } = [];

    /// <summary>
    /// The list of <see cref="Token"/> objects waiting to be processed.
    /// </summary>
    public required List<Token> Tokens { get; init; }

    /// <summary>
    /// The path of the file.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// The name of the file with extension.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// The raw source code of the file, or the contents.
    /// </summary>
    public required string RawSourceCode { get; init; }

    /// <inheritdoc />
    public override async Task<bool> Visit([NotNull] ParsingContext context)
    {
        while (!context.IsAtEnd())
        {
            bool handled = await base.Visit(context);

            if (!handled)
            {
                return false;
            }
        }

        return true;
    }
    /// <inheritdoc />
    public override async Task<bool> VisitKeyword([NotNull] ParsingContext context)
    {
        Token token = context.Peek()!;

        if (Constants.PrimitiveTypes.ContainsKey(token.Value))
        {
            VariableDeclarationNode varDeclarationNode = new(token);
            this.Children.Add(varDeclarationNode);

            await varDeclarationNode.Visit(context);

            return true;
        }

        context.Errors.Add(new CompilerError(token)
        {
            ErrorReason = $"Unexpected keyword in top-level statement ({token.Value})."
        });

        return false;
    }
}

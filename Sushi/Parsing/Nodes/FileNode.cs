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
            // Look ahead to see if this is the start of a variable or function declaration

            if (this.IsVariableDeclaration(context))
            {
                VariableDeclarationNode varDeclarationNode = new(token);
                this.Children.Add(varDeclarationNode);

                return await varDeclarationNode.Visit(context);
            }

            FunctionDeclarationNode funcDeclarationNode = new(token);
            this.Children.Add(funcDeclarationNode);

            return await funcDeclarationNode.Visit(context);
        }

        context.Errors.Add(new CompilerError(token)
        {
            ErrorReason = $"Unexpected keyword in top-level statement ({token.Value})."
        });

        return false;
    }

    private bool IsVariableDeclaration([NotNull] ParsingContext context)
    {
        Token? name = context.Peek(2);
        Token? assignment = context.Peek(3);

        if (assignment?.Type is TokenType.Whitespace)
        {
            assignment = context.Peek(4);
        }
        
        if (name?.Type is TokenType.Identifier && assignment?.Type is TokenType.AssignmentOperator)
        {
            return true;
        }

        if (name?.Type is TokenType.Identifier && assignment?.Type is TokenType.Terminator)
        {
            return true;
        }

        return false;
    }
}

using Sushi.Lexing;
using Sushi.Source;

namespace Sushi.Parsing.Syntax;

/// <summary>
/// Represents parsed Sushi syntax together with the complete
/// lexical source model from which it was produced.
/// </summary>
public sealed class ConcreteSyntaxTree
{
    /// <summary>
    /// Gets the complete lexical result underlying this syntax tree.
    /// </summary>
    public LexerResult LexerResult { get; }

    /// <summary>
    /// Gets the structured root of the syntax tree.
    /// </summary>
    public SyntaxNode Root { get; }

    /// <summary>
    /// Gets the authoritative source snapshot represented by the tree.
    /// </summary>
    public SourceSnapshot Snapshot => this.LexerResult.Snapshot;

    /// <summary>
    /// Creates a concrete syntax tree over an existing lexical result.
    /// </summary>
    /// <param name="lexerResult">
    /// The complete lexical result, including source-backed trivia and recovery elements.
    /// </param>
    /// <param name="root">
    /// The structured syntax root produced by the parser.
    /// </param>
    public ConcreteSyntaxTree(LexerResult lexerResult, SyntaxNode root)
    {
        ArgumentNullException.ThrowIfNull(lexerResult);
        ArgumentNullException.ThrowIfNull(root);

        if (!ReferenceEquals(lexerResult.Snapshot, root.Span.Snapshot))
        {
            throw new ArgumentException("The syntax root must belong to the lexical result's source snapshot.", nameof(root));
        }

        this.LexerResult = lexerResult;
        this.Root = root;
    }
}
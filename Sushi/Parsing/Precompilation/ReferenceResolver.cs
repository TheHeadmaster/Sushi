using System.Diagnostics.CodeAnalysis;
using Sushi.Diagnostics;
using Sushi.Diagnostics.Errors;
using Sushi.Parsing.Nodes;
using Sushi.Parsing.Nodes.Expressions.Core;
using Sushi.Parsing.Nodes.Expressions.Prefixes;
using Sushi.Parsing.Nodes.TopLevelStatements;
using Sushi.Tokenization;

namespace Sushi.Parsing.Precompilation;

/// <summary>
/// Handles the resolution and tracking of references, such as types, namespaces, and identifiers.
/// </summary>
public sealed partial class ReferenceResolver : ASTVisitor
{
    /// <summary>
    /// The <see cref="FileNode"/> currently being visited.
    /// </summary>
    private FileNode currentFile = null!;

    /// <summary>
    /// The currently included namespaces in the resolving scope.
    /// </summary>
    private readonly List<string> includedNamespaces = [];

    /// <summary>
    /// The list of messages emitted by this visitor.
    /// </summary>
    private readonly List<CompilerMessage> messages = [];

    /// <inheritdoc />
    protected override async Task VisitFile([NotNull] FileNode file)
    {
        this.currentFile = file;

        foreach (StatementNode statement in file.Statements)
        {
            await this.Visit(statement);
        }
    }

    /// <inheritdoc />
    protected override async Task VisitNamespaceDeclaration([NotNull] NamespaceDeclarationNode namespaceDeclaration)
    {
        if (namespaceDeclaration.Expression is null)
        {
            return;
        }

        if (namespaceDeclaration.Expression is not IdentifierNode and not NamespaceNode)
        {
            List<Token> tokens = await namespaceDeclaration.Expression.GetTokens().ToListAsync();

            this.messages.Add(new InvalidNamespaceError(tokens.First(), tokens.Last(), this.currentFile.FilePath));
            return;
        }

        List<string> namespaceChain = await namespaceDeclaration.BuildNamespace();

        this.includedNamespaces.Add(string.Join('.', namespaceChain));
    }

    /// <inheritdoc />
    protected override async Task VisitTree([NotNull] AbstractSyntaxTree tree)
    {
        //this.types.AddRange(Constants.PrimitiveResolvedTypes);

        foreach (FileNode child in tree.Children)
        {
            await this.Visit(child);
        }
    }
}

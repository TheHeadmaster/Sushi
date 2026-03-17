using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Sushi.Parsing;
using Sushi.Parsing.Nodes;
using Sushi.Parsing.Scope;

namespace Sushi.Precompilation;

/// <summary>
/// Handles the resolution and tracking of references, such as types, namespaces, and identifiers.
/// </summary>
public sealed partial class ReferenceResolver : ASTVisitor
{
    /// <summary>
    /// The list of currently tracked namespaces.
    /// </summary>
    private readonly List<string> namespaces = [];

    /// <summary>
    /// The namespace currently in-scope.
    /// </summary>
    private string? currentNamespace;

    /// <summary>
    /// The file path currently in-scope.
    /// </summary>
    private string? currentFilePath;

    /// <summary>
    /// The list of types registered in the resolver.
    /// </summary>
    private readonly List<SushiType> types = [];

    /// <summary>
    /// The currently included namespaces in the resolving scope.
    /// </summary>
    private readonly List<string> includedNamespaces = [];

    /// <summary>
    /// Starts a new namespace scope. All type declarations inside of this scope will be a part of this namespace.
    /// </summary>
    /// <param name="node">
    /// The node to use as a namespace scope.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/>.
    /// </returns>
    public async Task StartNamespace([NotNull] NamespaceDeclarationNode node)
    {
        ExpressionNode? currentNode = node.Body;

        List<string> namespaceChain = [];

        while (true)
        {
            if (currentNode is null)
            {
                break;
            }

            currentNode = await this.ConsumeNamespaceOrIdentifier(currentNode, namespaceChain);
        }

        this.currentNamespace = string.Join('.', namespaceChain);
    }

    /// <summary>
    /// Consumes a namespace or identifier node and adds the namespace part to the namespaces list.
    /// </summary>
    /// <param name="node">
    /// The node to consume.
    /// </param>
    /// <param name="namespaceChain">
    /// The namespace chain to modify.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/> that returns the next node in the chain.
    /// </returns>
    private Task<ExpressionNode?> ConsumeNamespaceOrIdentifier([NotNull] ExpressionNode node, [NotNull] List<string> namespaceChain)
    {
        ExpressionNode? nextNode = null;

        if (node is NamespaceNode namespaceNode && namespaceNode.Name is not null)
        {
            namespaceChain.Add(namespaceNode.Name.Name);
            nextNode = namespaceNode.Right;
        }
        else if (node is IdentifierNode identifier)
        {
            namespaceChain.Add(identifier.Name);
            nextNode = null;
        }

        string ns = string.Join('.', namespaceChain);

        if (!this.namespaces.Contains(ns))
        {
            this.namespaces.Add(ns);
        }

        return Task.FromResult(nextNode);
    }

    /// <summary>
    /// Tries to add a type node, and returns false if there is a naming conflict within the scope.
    /// </summary>
    /// <param name="node">
    /// The type node.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/> that returns whether the operation succeeded.
    /// </returns>
    public async Task<bool> TryAddType([NotNull] TypeNode node)
    {
        SushiType? existing = this.types.FirstOrDefault(x => x.Name == node.Name && x.Namespace == this.currentNamespace);

        if (existing is not null)
        {
            return false;
        }
        else
        {
            existing = new SushiType() { Name = node.Name, Namespace = this.currentNamespace!, FilePath = this.currentFilePath };
            this.types.Add(existing);
            node.ResolvedType = existing;
            return true;
        }
    }

    /// <summary>
    /// Resolves the type based on the current scope of the resolver.
    /// </summary>
    /// <param name="name">
    /// The type name to resolve.
    /// </param>
    /// <returns>
    /// An awaitable <see cref="Task"/> that returns the <see cref="SushiType"/> or null if it was not resolved.
    /// </returns>
    public async Task<SushiType?> ResolveType(string name) => this.types.FirstOrDefault(x => x.Name == name && this.includedNamespaces.Contains(x.Namespace));

    public async Task StartFile(string filePath) => this.currentFilePath = filePath;

    public async Task<List<string>> GetNamespaceFilePaths(string namespaceString)
    {
        List<string> namespaceFilePaths = [];

        foreach (SushiType type in this.types.Where(x => x.Namespace == namespaceString))
        {
            Uri fullUri = new(type.FilePath);

            string projectPath = $"{AppMeta.Options.ProjectPath.TrimEnd('/', '\\')}{Path.DirectorySeparatorChar}";

            Uri baseUri = new(projectPath);

            Uri relativeUri = baseUri.MakeRelativeUri(fullUri);

            string relativeFilePath = Uri.UnescapeDataString(relativeUri.ToString()).Replace('/', Path.DirectorySeparatorChar);

            namespaceFilePaths.Add(relativeFilePath);
        }

        return [..namespaceFilePaths.Distinct()];
    }

    /// <inheritdoc />
    protected override async Task VisitTree(AbstractSyntaxTree tree)
    {
        foreach (FileNode child in tree.Children)
        {
            await this.Visit(child);
        }
    }

    /// <inheritdoc />
    protected override async Task VisitFile(FileNode file)
    {
        foreach (StatementNode statement in file.Statements)
        {
            this.includedNamespaces.Clear();
            this.currentFilePath = file.FilePath;
            await this.Visit(statement);
        }
    }

    /// <inheritdoc />
    protected override async Task VisitUsing(UsingNode usingNode)
    {
        List<string> namespaceChain = await usingNode.BuildNamespace();

        Match match = NamespaceChain().Match(string.Join('.', namespaceChain));

        string root = string.Empty;

        bool hasWildcard = false;

        if (match.Success && match.Groups.TryGetValue("chain", out Group? chain))
        {
            root = chain.Value;
        }

        if (match.Success && match.Groups.ContainsKey("wildcard"))
        {
            hasWildcard = true;
        }

        if (hasWildcard)
        {
            IEnumerable<string> addNamespaces = this.namespaces.Where(x => x.StartsWith(root, StringComparison.Ordinal));
            this.includedNamespaces.AddRange(addNamespaces);
            usingNode.ResolvedNamespaces.AddRange(addNamespaces);
        }
        else
        {
            this.includedNamespaces.Add(root);
            usingNode.ResolvedNamespaces.Add(root);
        }
    }

    /// <inheritdoc />
    protected override async Task VisitNamespaceDeclaration(NamespaceDeclarationNode namespaceDeclaration)
    {
        List<string> namespaceChain = await namespaceDeclaration.BuildNamespace();

        this.includedNamespaces.Add(string.Join('.', namespaceChain));
    }

    /// <summary>
    /// Matches valid identifier strings.
    /// </summary>
    /// <returns>
    /// The <see cref="Regex"/>.
    /// </returns>
    [GeneratedRegex(@"^(?<chain>(?:@?[a-zA-Z][a-zA-Z0-9]*)(?:\.@?[a-zA-Z][a-zA-Z0-9]*)*)(?<wildcard>(?:\.\*)?)")]
    private static partial Regex NamespaceChain();
}

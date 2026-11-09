using System.Text;
using Sushi.Analysis;
using Sushi.Diagnostics;
using Sushi.Parsing.Syntax;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Finds compiler-validated parameters and locals visible at a source position.
/// Lexical scope is determined from the parsed function structure.
/// </summary>
internal static class SourceCompletionScopeResolver
{
    /// <summary>
    /// Collects local bindings and parameters that may be referenced at the cursor.
    /// </summary>
    /// <param name="analysis">
    /// The project-aware analysis containing the callable catalog.
    /// </param>
    /// <param name="owner">
    /// The source function enclosing the cursor.
    /// </param>
    /// <param name="offset">
    /// The canonical UTF-8 cursor offset.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels binding and lexical-scope traversal.
    /// </param>
    /// <returns>
    /// Visible, compiler-validated bindings ordered by semantic name.
    /// </returns>
    internal static IReadOnlyList<SourceCompletionBinding> Resolve(
        SourceAnalysisResult analysis, FunctionDeclarationSyntax owner, int offset, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(owner);
        cancellationToken.ThrowIfCancellationRequested();

        PackageCallable? callable = analysis.VisibleCallables.FirstOrDefault(candidate =>
            ReferenceEquals(candidate.Declaration, owner));

        if (callable is null)
        {
            return [];
        }

        SortedDictionary<string, SourceCompletionBinding> visible = new(StringComparer.Ordinal);

        for (int index = 0; index < callable.Function.Parameters.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            BoundParameter parameter = callable.Function.Parameters[index];
            if (parameter.Name is "_" || index >= owner.Parameters.Count)
            {
                continue;
            }

            SyntaxToken nameToken = owner.Parameters[index].NameToken;
            if (!nameToken.IsSourceBacked || !StringComparer.Ordinal.Equals(IdentifierBinder.Bind(nameToken), parameter.Name))
            {
                continue;
            }

            visible.TryAdd(parameter.Name, new SourceCompletionBinding(
                parameter.Name, GetSpelling(nameToken.Span), parameter.Type, SourceCompletionBindingKind.Parameter));
        }

        LocalBindingContext context = new(callable.Function.Parameters, owner, callable.Function, callable.Documentation);

        _ = FunctionDeclarationBinder.BindBody(owner.Body, callable.Function.ReturnType, callable.Function.Parameters,
            new DiagnosticReporter(), cancellationToken, analysis.VisibleCallables, context);

        Dictionary<SourceSpan, SourceCompletionBinding> validatedLocals = [];

        foreach (SourceSemanticOccurrence occurrence in context.GetOccurrences())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!occurrence.IsDeclaration
                || occurrence.Symbol.Kind is not SourceSemanticSymbolKind.Local
                || !ReferenceEquals(occurrence.Symbol.Declaration, owner)
                || occurrence.Symbol.Type is not BoundType type
                || occurrence.Symbol.Name is "_")
            {
                continue;
            }

            validatedLocals[occurrence.Span] = new SourceCompletionBinding(
                occurrence.Symbol.Name, GetSpelling(occurrence.Span), type, SourceCompletionBindingKind.Local);
        }

        CollectBlock(owner.Body, offset, validatedLocals, visible, cancellationToken);

        return [.. visible.Values];
    }

    private static void CollectBlock(
        BlockSyntax block, int offset,
        IReadOnlyDictionary<SourceSpan, SourceCompletionBinding> validatedLocals,
        SortedDictionary<string, SourceCompletionBinding> visible,
        CancellationToken cancellationToken)
    {
        if (!ContainsCursor(block, offset))
        {
            return;
        }

        foreach (StatementSyntax statement in block.Statements)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (statement.Span.Start > offset)
            {
                break;
            }

            if (statement is LocalDeclarationStatementSyntax declaration)
            {
                if (declaration.SemicolonToken.IsSourceBacked && declaration.SemicolonToken.Span.End <= offset)
                {
                    AddValidatedLocal(declaration.NameToken.Span, validatedLocals, visible);
                }

                continue;
            }

            if (statement.Span.End < offset)
            {
                continue;
            }

            switch (statement)
            {
                case IfStatementSyntax conditional:
                    CollectConditional(conditional, offset, validatedLocals, visible, cancellationToken);
                    break;

                case WhileStatementSyntax loop:
                    CollectBlock(loop.Body, offset, validatedLocals, visible, cancellationToken);
                    break;

                case DoWhileStatementSyntax loop:
                    CollectBlock(loop.Body, offset, validatedLocals, visible, cancellationToken);
                    break;

                case ForStatementSyntax loop:
                    CollectFor(loop, offset, validatedLocals, visible, cancellationToken);
                    break;
            }

            break;
        }
    }

    private static void CollectConditional(
        IfStatementSyntax conditional, int offset,
        IReadOnlyDictionary<SourceSpan, SourceCompletionBinding> validatedLocals,
        SortedDictionary<string, SourceCompletionBinding> visible,
        CancellationToken cancellationToken)
    {
        if (ContainsCursor(conditional.ThenBlock, offset))
        {
            CollectBlock(conditional.ThenBlock, offset, validatedLocals, visible, cancellationToken);
        }
        else if (conditional.ElseBlock is BlockSyntax alternative && ContainsCursor(alternative, offset))
        {
            CollectBlock(alternative, offset, validatedLocals, visible, cancellationToken);
        }
        else if (conditional.ElseIf is IfStatementSyntax elseIf
            && elseIf.Span.Start <= offset && offset <= elseIf.Span.End)
        {
            CollectConditional(elseIf, offset, validatedLocals, visible, cancellationToken);
        }
    }

    private static void CollectFor(
        ForStatementSyntax loop, int offset,
        IReadOnlyDictionary<SourceSpan, SourceCompletionBinding> validatedLocals,
        SortedDictionary<string, SourceCompletionBinding> visible,
        CancellationToken cancellationToken)
    {
        foreach (ForHeaderOperationSyntax operation in loop.Initialization)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (operation.Span.End > offset)
            {
                break;
            }

            if (operation.IsDeclaration && operation.TypeToken is not null)
            {
                AddValidatedLocal(operation.NameToken.Span, validatedLocals, visible);
            }
        }

        CollectBlock(loop.Body, offset, validatedLocals, visible, cancellationToken);
    }

    private static bool ContainsCursor(BlockSyntax block, int offset)
        => block.OpenBraceToken.IsSourceBacked
            && block.OpenBraceToken.Span.End <= offset
            && (block.CloseBraceToken.IsSourceBacked
                ? offset <= block.CloseBraceToken.Span.Start
                : offset <= block.Span.End);

    private static void AddValidatedLocal(
        SourceSpan nameSpan,
        IReadOnlyDictionary<SourceSpan, SourceCompletionBinding> validatedLocals,
        SortedDictionary<string, SourceCompletionBinding> visible)
    {
        if (validatedLocals.TryGetValue(nameSpan, out SourceCompletionBinding? binding))
        {
            visible.TryAdd(binding.Name, binding);
        }
    }

    private static string GetSpelling(SourceSpan span)
        => Encoding.UTF8.GetString(span.Snapshot.Bytes.Span[span.Start..span.End]);
}

/// <summary>
/// Identifies the compiler binding category displayed by completion.
/// </summary>
internal enum SourceCompletionBindingKind
{
    Parameter,
    Local
}

/// <summary>
/// Describes a validated local or parameter and the exact spelling needed
/// to reference it from Sushi source.
/// </summary>
/// <param name="Name">
/// The compiler-decoded semantic identifier.
/// </param>
/// <param name="Spelling">
/// The source spelling, including an identifier escape when required.
/// </param>
/// <param name="Type">
/// The concrete type established by binding.
/// </param>
/// <param name="Kind">
/// Whether the symbol is a function parameter or initialized local.
/// </param>
internal sealed record SourceCompletionBinding(string Name, string Spelling, BoundType Type, SourceCompletionBindingKind Kind);
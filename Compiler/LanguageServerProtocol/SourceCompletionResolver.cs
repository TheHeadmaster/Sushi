using System.Text;
using Sushi.Analysis;
using Sushi.Lexing.Tokenization;
using Sushi.Parsing.Syntax;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Discovers compiler-visible symbols matching an identifier being edited
/// inside a package-level Sushi function body.
/// </summary>
internal static class SourceCompletionResolver
{
    /// <summary>
    /// Resolves function and local-binding completion using the current
    /// source snapshot and the compiler's visible callable catalog.
    /// </summary>
    /// <param name="analysis">
    /// The project-aware analysis of the current source snapshot.
    /// </param>
    /// <param name="offset">
    /// The cursor position in canonical UTF-8 byte coordinates.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels source inspection and candidate selection.
    /// </param>
    /// <returns>
    /// The identifier replacement span and matching candidates, or null
    /// when completion is unsupported at this position.
    /// </returns>
    internal static SourceCompletionResult? Resolve(SourceAnalysisResult analysis, int offset, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        cancellationToken.ThrowIfCancellationRequested();

        SourceSnapshot snapshot = analysis.Snapshot;
        if (!snapshot.IsValidUtf8 || offset < 0 || offset > snapshot.SourceLength
            || analysis.SyntaxTree.Root is not SourceFileSyntax source)
        {
            return null;
        }

        FunctionDeclarationSyntax? owner = source.FunctionDeclarations.FirstOrDefault(function =>
            function.Body.OpenBraceToken.IsSourceBacked
            && function.Body.OpenBraceToken.Span.End <= offset
            && (function.Body.CloseBraceToken.IsSourceBacked
                ? offset <= function.Body.CloseBraceToken.Span.Start
                : offset <= function.Body.Span.End));

        if (owner is null)
        {
            return null;
        }

        IReadOnlyList<LexToken> tokens = analysis.SyntaxTree.LexerResult.Tokens;
        if (!TryFindIdentifier(tokens, snapshot, offset, cancellationToken, out SourceSpan replacement, out string prefix))
        {
            return null;
        }

        SortedDictionary<string, PackageCallable?> candidates = new(StringComparer.Ordinal);

        foreach (PackageCallable callable in analysis.VisibleCallables)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!callable.Name.StartsWith(prefix, StringComparison.Ordinal)
                || !HasReliableDeclaration(callable))
            {
                continue;
            }

            if (!candidates.TryAdd(callable.Name, callable))
            {
                candidates[callable.Name] = null;
            }
        }

        SourceCompletionBinding[] bindings =
        [
            .. SourceCompletionScopeResolver.Resolve(analysis, owner, offset, cancellationToken)
                .Where(binding => binding.Name.StartsWith(prefix, StringComparison.Ordinal))
        ];

        PackageCallable[] selectedFunctions = [.. candidates.Values.OfType<PackageCallable>()];

        return selectedFunctions.Length == 0 && bindings.Length == 0
            ? null
            : new SourceCompletionResult(replacement, selectedFunctions, bindings);
    }

    private static bool TryFindIdentifier(
        IReadOnlyList<LexToken> tokens, SourceSnapshot snapshot, int offset,
        CancellationToken cancellationToken, out SourceSpan replacement, out string prefix)
    {
        replacement = default;
        prefix = string.Empty;

        ReadOnlySpan<byte> bytes = snapshot.Bytes.Span;

        for (int index = 0; index < tokens.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            LexToken token = tokens[index];
            if (token.Span.Start >= offset)
            {
                break;
            }

            if (token.Span.End < offset)
            {
                continue;
            }

            if (token.Type is not LexTokenType.Identifier and not LexTokenType.EscapedIdentifier)
            {
                return false;
            }

            int prefixStart = token.Span.Start + (token.Type is LexTokenType.EscapedIdentifier ? 1 : 0);
            if (offset < prefixStart)
            {
                return false;
            }

            int previous = PreviousSignificant(tokens, index - 1);
            int next = NextSignificant(tokens, index + 1);

            if (previous >= 0 && IsPunctuation(tokens[previous], bytes, (byte)'.')
                || next >= 0 && IsPunctuation(tokens[next], bytes, (byte)':')
                || previous >= 0 && IsTypeDeclarationPrefix(tokens[previous], bytes))
            {
                return false;
            }

            prefix = Encoding.UTF8.GetString(bytes[prefixStart..offset]);
            replacement = token.Span;
            return true;
        }

        return false;
    }

    private static bool HasReliableDeclaration(PackageCallable callable)
    {
        SyntaxToken name = callable.Declaration.Name;

        return name.IsSourceBacked
            && StringComparer.Ordinal.Equals(IdentifierBinder.Bind(name), callable.Name);
    }

    private static bool IsTypeDeclarationPrefix(LexToken token, ReadOnlySpan<byte> bytes)
    {
        if (token.Type is not (LexTokenType.Keyword or LexTokenType.Identifier))
        {
            return false;
        }

        ReadOnlySpan<byte> spelling = bytes[token.Span.Start..token.Span.End];

        return spelling.SequenceEqual("int32"u8)
            || spelling.SequenceEqual("bool"u8)
            || spelling.SequenceEqual("void"u8);
    }

    private static bool IsPunctuation(LexToken token, ReadOnlySpan<byte> bytes, byte punctuation)
        => token.Type is LexTokenType.Punctuation
            && token.Span.Length == 1
            && bytes[token.Span.Start] == punctuation;

    private static int PreviousSignificant(IReadOnlyList<LexToken> tokens, int index)
    {
        for (; index >= 0; index--)
        {
            if (!IsTrivia(tokens[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private static int NextSignificant(IReadOnlyList<LexToken> tokens, int index)
    {
        for (; index < tokens.Count; index++)
        {
            if (!IsTrivia(tokens[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsTrivia(LexToken token)
        => token.Type is LexTokenType.Whitespace
            or LexTokenType.LineTerminator
            or LexTokenType.LineComment
            or LexTokenType.DocumentationLineComment
            or LexTokenType.BlockComment;
}

/// <summary>
/// Contains a source-backed completion replacement range and the matching
/// functions and local bindings established for the editor position.
/// </summary>
/// <param name="Replacement">
/// The complete identifier being edited.
/// </param>
/// <param name="Candidates">
/// Unambiguous compiler-visible package functions.
/// </param>
/// <param name="Bindings">
/// Visible function parameters and initialized locals.
/// </param>
internal sealed record SourceCompletionResult(
    SourceSpan Replacement,
    IReadOnlyList<PackageCallable> Candidates,
    IReadOnlyList<SourceCompletionBinding> Bindings);
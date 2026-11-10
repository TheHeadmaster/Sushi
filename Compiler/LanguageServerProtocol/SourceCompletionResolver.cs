using System.Text;
using Sushi.Analysis;
using Sushi.Lexing.Tokenization;
using Sushi.Parsing.Syntax;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Discovers compiler-visible symbols matching a typed identifier or an
/// empty, type-constrained expression position within a package function.
/// </summary>
internal static class SourceCompletionResolver
{
    /// <summary>
    /// Resolves compiler-backed function, parameter, and local completion
    /// using the current source snapshot and expression context.
    /// </summary>
    /// <param name="analysis">The current project-aware source analysis.</param>
    /// <param name="offset">The cursor's canonical UTF-8 byte position.</param>
    /// <param name="cancellationToken">Cancels source inspection and binding.</param>
    /// <returns>
    /// Matching candidates and their replacement range, or null when
    /// completion is unsupported at the requested position.
    /// </returns>
    internal static SourceCompletionResult? Resolve(
        SourceAnalysisResult analysis, int offset, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        cancellationToken.ThrowIfCancellationRequested();

        SourceSnapshot snapshot = analysis.Snapshot;
        if (!snapshot.IsValidUtf8 || offset < 0 || offset > snapshot.SourceLength
            || analysis.SyntaxTree.Root is not SourceFileSyntax source)
        {
            return null;
        }

        IReadOnlyList<LexToken> tokens = analysis.SyntaxTree.LexerResult.Tokens;
        bool hasIdentifier = TryFindIdentifier(tokens, snapshot, offset, cancellationToken,
            out SourceSpan replacement, out string prefix);
        bool hasPotentialExpressionContext = false;

        if (!hasIdentifier)
        {
            replacement = new SourceSpan(snapshot, offset, offset);
            prefix = string.Empty;
            hasPotentialExpressionContext = HasPotentialEmptyExpressionContext(tokens, snapshot, offset, cancellationToken);
        }

        FunctionDeclarationSyntax? owner = FindEnclosingFunction(
            source, replacement, offset, cancellationToken);
        if (owner is null)
        {
            return null;
        }

        int scopeOffset = owner.Body.CloseBraceToken.IsMissing
            ? Math.Min(offset, owner.Body.CloseBraceToken.Span.Start)
            : offset;
        IReadOnlyList<SourceCompletionBinding> visibleBindings =
            SourceCompletionScopeResolver.Resolve(analysis, owner, scopeOffset, cancellationToken);
        SourceCompletionArgumentLabelResolution? labelResolution =
            SourceCompletionArgumentLabelResolver.Resolve(analysis, owner, offset, cancellationToken);

        if (!hasIdentifier && !hasPotentialExpressionContext && labelResolution is null)
        {
            return null;
        }

        SourceCompletionExpressionContext nestedContext = SourceCompletionNestedExpressionContextResolver.Resolve(
            analysis, owner, replacement.Start, visibleBindings, cancellationToken);
        BoundType? expectedType = nestedContext.IsRecognized
            ? nestedContext.ExpectedType
            : SourceCompletionExpressionContextResolver.Resolve(
                analysis, owner, replacement.Start, visibleBindings, cancellationToken);
        bool allowIntegerBooleanStarter = nestedContext.IsRecognized
            && nestedContext.AllowsIntegerBooleanExpressionStarter;

        bool includeValues = labelResolution?.SuppressValueCompletions is not true
            && (hasIdentifier || expectedType is not null);
        SortedDictionary<string, PackageCallable?> candidates = new(StringComparer.Ordinal);

        if (includeValues)
        {
            foreach (PackageCallable callable in analysis.VisibleCallables)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!callable.Name.StartsWith(prefix, StringComparison.Ordinal)
                    || !HasReliableDeclaration(callable)
                    || !IsCandidateTypeEligible(callable.Function.ReturnType, expectedType, allowIntegerBooleanStarter))
                {
                    continue;
                }

                if (!candidates.TryAdd(callable.Name, callable))
                {
                    candidates[callable.Name] = null;
                }
            }
        }

        SourceCompletionBinding[] bindings = includeValues
            ? [.. visibleBindings.Where(binding =>
                binding.Name.StartsWith(prefix, StringComparison.Ordinal)
                && IsCandidateTypeEligible(binding.Type, expectedType, allowIntegerBooleanStarter))]
            : [];

        PackageCallable[] selectedFunctions = [.. candidates.Values.OfType<PackageCallable>()];
        IReadOnlyList<SourceCompletionArgumentLabelCandidate> labels =
            labelResolution?.Candidates ?? Array.Empty<SourceCompletionArgumentLabelCandidate>();

        return selectedFunctions.Length == 0 && bindings.Length == 0 && labels.Count == 0
            ? null
            : new SourceCompletionResult(replacement, selectedFunctions, bindings)
            {
                ArgumentLabels = labels,
                SuppressValueCandidates = labelResolution?.SuppressValueCompletions is true,
                PrioritizeBooleanExpressionStarters = allowIntegerBooleanStarter
            };
    }

    private static bool HasPotentialEmptyExpressionContext(
        IReadOnlyList<LexToken> tokens, SourceSnapshot snapshot,
        int offset, CancellationToken cancellationToken)
    {
        ReadOnlySpan<byte> bytes = snapshot.Bytes.Span;

        foreach (LexToken token in tokens)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (token.Span.Start > offset)
            {
                break;
            }

            if (token.Span.Start <= offset && offset < token.Span.End
                && token.Type is (LexTokenType.LineComment or LexTokenType.DocumentationLineComment
                    or LexTokenType.BlockComment or LexTokenType.Unknown))
            {
                return false;
            }

            if (token.Span.Start < offset && offset < token.Span.End && !IsTrivia(token))
            {
                return false;
            }
        }

        for (int index = tokens.Count - 1; index >= 0; index--)
        {
            cancellationToken.ThrowIfCancellationRequested();

            LexToken token = tokens[index];
            if (token.Span.End > offset || IsTrivia(token))
            {
                continue;
            }

            if (token.Type is LexTokenType.Keyword)
            {
                if (offset <= token.Span.End)
                {
                    return false;
                }

                ReadOnlySpan<byte> keyword = bytes[token.Span.Start..token.Span.End];
                return keyword.SequenceEqual("return"u8)
                    || keyword.SequenceEqual("not"u8)
                    || keyword.SequenceEqual("and"u8)
                    || keyword.SequenceEqual("or"u8);
            }

            if (token.Type is LexTokenType.Operator)
            {
                return true;
            }

            return token.Type is LexTokenType.Punctuation
                && token.Span.Length == 1
                && bytes[token.Span.Start] is (byte)'(' or (byte)',' or (byte)':' or (byte)';';
        }

        return false;
    }

    private static FunctionDeclarationSyntax? FindEnclosingFunction(
        SourceFileSyntax source, SourceSpan identifier, int offset, CancellationToken cancellationToken)
    {
        foreach (FunctionDeclarationSyntax function in source.FunctionDeclarations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            BlockSyntax body = function.Body;
            if (!body.OpenBraceToken.IsSourceBacked || body.OpenBraceToken.Span.End > identifier.Start)
            {
                continue;
            }

            if (body.CloseBraceToken.IsSourceBacked)
            {
                if (offset <= body.CloseBraceToken.Span.Start)
                {
                    return function;
                }

                continue;
            }

            if (offset <= body.CloseBraceToken.Span.Start)
            {
                return function;
            }

            // The parser leaves unsupported statement starts unconsumed.
            // Completion can recover at that exact boundary without changing
            // the grammar or claiming later source belongs to this function.
            if (body.CloseBraceToken.IsMissing
                && body.CloseBraceToken.Span.Start == identifier.Start
                && source.UnparsedContentSpan is SourceSpan unparsed
                && unparsed.Start == identifier.Start)
            {
                return function;
            }
        }

        return null;
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

    private static bool IsCandidateTypeEligible(
        BoundType candidateType, BoundType? expectedType, bool allowIntegerBooleanStarter)
        => expectedType is null
            || candidateType == expectedType.Value
            || allowIntegerBooleanStarter
                && expectedType is BoundType.Bool
                && candidateType is BoundType.Int32;

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
/// Contains an identifier or insertion-point replacement span and the
/// compiler-visible functions and bindings available for completion.
/// </summary>
/// <param name="Replacement">The source span replaced when completion is accepted.</param>
/// <param name="Candidates">Unambiguous matching package functions.</param>
/// <param name="Bindings">Matching parameters and initialized locals.</param>
internal sealed record SourceCompletionResult(
    SourceSpan Replacement,
    IReadOnlyList<PackageCallable> Candidates,
    IReadOnlyList<SourceCompletionBinding> Bindings)
{
    /// <summary>
    /// Gets named-argument label candidates available at the current invocation slot.
    /// </summary>
    public IReadOnlyList<SourceCompletionArgumentLabelCandidate> ArgumentLabels { get; init; } = [];

    /// <summary>
    /// Gets whether the invocation requires named arguments at the current slot.
    /// </summary>
    public bool SuppressValueCandidates { get; init; }

    /// <summary>
    /// Gets whether direct Boolean candidates should rank before integer expression starters.
    /// </summary>
    public bool PrioritizeBooleanExpressionStarters { get; init; }
}
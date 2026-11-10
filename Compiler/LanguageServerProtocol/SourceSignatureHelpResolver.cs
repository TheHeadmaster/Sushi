using Sushi.Analysis;
using Sushi.Lexing.Tokenization;
using Sushi.Parsing.Syntax;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Resolves signature help from source tokens and compiler-visible callable
/// signatures, including calls whose arguments cannot yet be bound.
/// </summary>
internal static class SourceSignatureHelpResolver
{
    /// <summary>
    /// Finds the innermost active invocation and its reliably identifiable
    /// parameter without attempting to resolve an overload or bind expressions.
    /// </summary>
    /// <param name="analysis">
    /// The current analyzed source snapshot.
    /// </param>
    /// <param name="offset">
    /// The cursor's canonical UTF-8 byte offset.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels source inspection.
    /// </param>
    /// <returns>
    /// Signature information, or null when no unique callable is established.
    /// </returns>
    internal static SourceSignatureHelpResult? Resolve(SourceAnalysisResult analysis, int offset, CancellationToken cancellationToken)
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
            function.Body.Span.Start <= offset && offset <= function.Body.Span.End);

        if (owner is null)
        {
            return null;
        }

        IReadOnlyList<LexToken> tokens = analysis.SyntaxTree.LexerResult.Tokens;
        if (IsInsideComment(tokens, offset))
        {
            return null;
        }

        List<OpenDelimiter> delimiters = FindOpenDelimiters(tokens, snapshot.Bytes.Span, offset, cancellationToken);

        for (int index = delimiters.Count - 1; index >= 0; index--)
        {
            cancellationToken.ThrowIfCancellationRequested();

            OpenDelimiter delimiter = delimiters[index];
            if (delimiter.Character != (byte)'(')
            {
                continue;
            }

            int targetIndex = PreviousSignificant(tokens, delimiter.TokenIndex - 1);
            if (targetIndex < 0 || !IsIdentifier(tokens[targetIndex]))
            {
                continue;
            }

            LexToken target = tokens[targetIndex];
            if (target.Span.Start < owner.Body.OpenBraceToken.Span.End)
            {
                continue;
            }

            int previousIndex = PreviousSignificant(tokens, targetIndex - 1);
            if (previousIndex >= 0 && IsPunctuation(tokens[previousIndex], snapshot.Bytes.Span, (byte)'.'))
            {
                return null;
            }

            string? name = GetIdentifierName(target);
            if (name is null)
            {
                return null;
            }

            PackageCallable[] candidates = [.. analysis.VisibleCallables.Where(callable =>
                StringComparer.Ordinal.Equals(callable.Name, name))];

            if (candidates.Length != 1)
            {
                return null;
            }

            PackageCallable callable = candidates[0];
            int? activeParameter = FindActiveParameter(tokens, delimiter.TokenIndex, offset, callable.Function.Parameters, cancellationToken);

            return new SourceSignatureHelpResult(callable, activeParameter);
        }

        return null;
    }

    /// <summary>
    /// Resolves the callable and active parameter for a specific invocation node,
    /// even when the cursor is nested inside one of its argument expressions.
    /// </summary>
    /// <param name="analysis">The current analyzed source snapshot.</param>
    /// <param name="invocation">The parsed invocation whose parameter context is required.</param>
    /// <param name="offset">The cursor's canonical UTF-8 byte offset.</param>
    /// <param name="cancellationToken">Cancels source inspection.</param>
    /// <returns>The selected callable and active parameter, or null when resolution is ambiguous.</returns>
    internal static SourceSignatureHelpResult? ResolveInvocation(
        SourceAnalysisResult analysis, InvocationExpressionSyntax invocation, int offset,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(invocation);
        cancellationToken.ThrowIfCancellationRequested();

        if (invocation.Target is not IdentifierExpressionSyntax target)
        {
            return null;
        }

        string? name = IdentifierBinder.Bind(target.IdentifierToken);
        if (name is null)
        {
            return null;
        }

        PackageCallable[] candidates = [.. analysis.VisibleCallables.Where(callable =>
            StringComparer.Ordinal.Equals(callable.Name, name))];
        if (candidates.Length != 1)
        {
            return null;
        }

        IReadOnlyList<LexToken> tokens = analysis.SyntaxTree.LexerResult.Tokens;
        int openingIndex = -1;
        for (int index = 0; index < tokens.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (tokens[index].Span.Start == invocation.OpenParenthesisToken.Span.Start
                && tokens[index].Span.End == invocation.OpenParenthesisToken.Span.End)
            {
                openingIndex = index;
                break;
            }
        }

        if (openingIndex < 0)
        {
            return null;
        }

        PackageCallable callable = candidates[0];
        int? activeParameter = FindActiveParameter(
            tokens, openingIndex, offset, callable.Function.Parameters, cancellationToken);
        return new SourceSignatureHelpResult(callable, activeParameter);
    }

    private static List<OpenDelimiter> FindOpenDelimiters(
        IReadOnlyList<LexToken> tokens, ReadOnlySpan<byte> bytes, int offset, CancellationToken cancellationToken)
    {
        List<OpenDelimiter> delimiters = [];

        for (int index = 0; index < tokens.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            LexToken token = tokens[index];
            if (token.Span.End > offset)
            {
                break;
            }

            if (token.Type is not LexTokenType.Punctuation || token.Span.Length != 1)
            {
                continue;
            }

            byte character = bytes[token.Span.Start];

            switch (character)
            {
                case (byte)'(' or (byte)'[' or (byte)'{':
                    delimiters.Add(new OpenDelimiter(character, index));
                    break;

                case (byte)')' or (byte)']' or (byte)'}':
                    CloseDelimiter(delimiters, character);
                    break;

                case (byte)';':
                    DiscardInterruptedGroups(delimiters);
                    break;
            }
        }

        return delimiters;
    }

    private static void CloseDelimiter(List<OpenDelimiter> delimiters, byte closing)
    {
        byte opening = closing switch
        {
            (byte)')' => (byte)'(',
            (byte)']' => (byte)'[',
            (byte)'}' => (byte)'{',
            _ => throw new ArgumentOutOfRangeException(nameof(closing))
        };

        int matchingIndex = delimiters.FindLastIndex(item => item.Character == opening);
        if (matchingIndex < 0)
        {
            delimiters.Clear();
            return;
        }

        delimiters.RemoveRange(matchingIndex, delimiters.Count - matchingIndex);
    }

    private static void DiscardInterruptedGroups(List<OpenDelimiter> delimiters)
    {
        int containingBlock = delimiters.FindLastIndex(item => item.Character == (byte)'{');
        int start = containingBlock + 1;

        if (start < delimiters.Count)
        {
            delimiters.RemoveRange(start, delimiters.Count - start);
        }
    }

    private static int? FindActiveParameter(
        IReadOnlyList<LexToken> tokens, int openingIndex, int offset,
        IReadOnlyList<BoundParameter> parameters, CancellationToken cancellationToken)
    {
        ReadOnlySpan<byte> bytes = tokens[openingIndex].Span.Snapshot.Bytes.Span;
        HashSet<int> supplied = [];
        List<byte> nestedDelimiters = [];

        int nextPositional = 0;
        int argumentStart = openingIndex + 1;
        bool namedSeen = false;

        int end = argumentStart;
        while (end < tokens.Count && tokens[end].Span.End <= offset)
        {
            end++;
        }

        for (int index = argumentStart; index < end; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            LexToken token = tokens[index];
            if (token.Type is not LexTokenType.Punctuation || token.Span.Length != 1)
            {
                continue;
            }

            byte character = bytes[token.Span.Start];

            switch (character)
            {
                case (byte)'(' or (byte)'[' or (byte)'{':
                    nestedDelimiters.Add(character);
                    break;

                case (byte)')' or (byte)']' or (byte)'}':
                    if (nestedDelimiters.Count == 0 || !DelimitersMatch(nestedDelimiters[^1], character))
                    {
                        return null;
                    }

                    nestedDelimiters.RemoveAt(nestedDelimiters.Count - 1);
                    break;

                case (byte)';' when nestedDelimiters.Count == 0:
                    return null;

                case (byte)',' when nestedDelimiters.Count == 0:
                    if (!TryCommitArgument(tokens, argumentStart, index, bytes, parameters,
                        supplied, ref nextPositional, ref namedSeen))
                    {
                        return null;
                    }

                    argumentStart = index + 1;
                    break;
            }
        }

        string? currentName = GetNamedArgument(tokens, argumentStart, end, bytes);
        if (currentName is not null)
        {
            int parameterIndex = FindParameterIndex(parameters, currentName);
            return parameterIndex >= 0 && !supplied.Contains(parameterIndex) ? parameterIndex : null;
        }

        if (!namedSeen)
        {
            return nextPositional < parameters.Count && !supplied.Contains(nextPositional)
                ? nextPositional
                : null;
        }

        int? remainingParameter = FindSoleUnsuppliedParameter(parameters, supplied);
        if (remainingParameter is null)
        {
            return null;
        }

        int first = NextSignificant(tokens, argumentStart, end);
        if (first < 0)
        {
            return remainingParameter;
        }

        if (!IsIdentifier(tokens[first]) || NextSignificant(tokens, first + 1, end) >= 0)
        {
            return null;
        }

        string? identifier = GetIdentifierName(tokens[first]);
        return identifier is not null
            && parameters[remainingParameter.Value].Name.StartsWith(identifier, StringComparison.Ordinal)
                ? remainingParameter
                : null;
    }

    private static bool TryCommitArgument(
        IReadOnlyList<LexToken> tokens, int start, int end, ReadOnlySpan<byte> bytes,
        IReadOnlyList<BoundParameter> parameters, HashSet<int> supplied,
        ref int nextPositional, ref bool namedSeen)
    {
        if (NextSignificant(tokens, start, end) < 0)
        {
            return false;
        }

        string? name = GetNamedArgument(tokens, start, end, bytes);
        if (name is not null)
        {
            namedSeen = true;
            int parameterIndex = FindParameterIndex(parameters, name);
            return parameterIndex >= 0 && supplied.Add(parameterIndex);
        }

        if (namedSeen || nextPositional >= parameters.Count)
        {
            return false;
        }

        return supplied.Add(nextPositional++);
    }

    private static int? FindSoleUnsuppliedParameter(IReadOnlyList<BoundParameter> parameters, HashSet<int> supplied)
    {
        int? remaining = null;

        for (int index = 0; index < parameters.Count; index++)
        {
            if (supplied.Contains(index))
            {
                continue;
            }

            if (remaining is not null)
            {
                return null;
            }

            remaining = index;
        }

        return remaining;
    }

    private static int FindParameterIndex(IReadOnlyList<BoundParameter> parameters, string name)
    {
        for (int index = 0; index < parameters.Count; index++)
        {
            if (StringComparer.Ordinal.Equals(parameters[index].Name, name))
            {
                return index;
            }
        }

        return -1;
    }

    private static string? GetNamedArgument(
        IReadOnlyList<LexToken> tokens, int start, int end, ReadOnlySpan<byte> bytes)
    {
        int first = NextSignificant(tokens, start, end);
        if (first < 0 || !IsIdentifier(tokens[first]))
        {
            return null;
        }

        int second = NextSignificant(tokens, first + 1, end);
        return second >= 0 && IsPunctuation(tokens[second], bytes, (byte)':')
            ? GetIdentifierName(tokens[first])
            : null;
    }

    private static bool DelimitersMatch(byte opening, byte closing)
        => opening switch
        {
            (byte)'(' => closing == (byte)')',
            (byte)'[' => closing == (byte)']',
            (byte)'{' => closing == (byte)'}',
            _ => false
        };

    private static string? GetIdentifierName(LexToken token)
    {
        SyntaxType type = token.Type is LexTokenType.EscapedIdentifier
            ? SyntaxType.EscapedIdentifierToken
            : SyntaxType.IdentifierToken;

        return IdentifierBinder.Bind(new SyntaxToken(type, token));
    }

    private static bool IsIdentifier(LexToken token)
        => token.Type is LexTokenType.Identifier or LexTokenType.EscapedIdentifier;

    private static bool IsPunctuation(LexToken token, ReadOnlySpan<byte> bytes, byte punctuation)
        => token.Type is LexTokenType.Punctuation && token.Span.Length == 1 && bytes[token.Span.Start] == punctuation;

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

    private static int NextSignificant(IReadOnlyList<LexToken> tokens, int start, int end)
    {
        for (int index = start; index < end; index++)
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

    private static bool IsInsideComment(IReadOnlyList<LexToken> tokens, int offset)
        => tokens.Any(token =>
            token.Span.Start < offset && offset < token.Span.End
            && token.Type is LexTokenType.LineComment or LexTokenType.DocumentationLineComment or LexTokenType.BlockComment);

    private readonly record struct OpenDelimiter(byte Character, int TokenIndex);
}

/// <summary>
/// Represents a uniquely selected compiler-visible signature and the parameter
/// reliably identified at the current editor position.
/// </summary>
/// <param name="Callable">
/// The selected function declaration.
/// </param>
/// <param name="ActiveParameter">
/// The declared parameter index, or null when no mapping is established.
/// </param>
internal sealed record SourceSignatureHelpResult(PackageCallable Callable, int? ActiveParameter);
using Sushi.Analysis;
using Sushi.Lexing.Tokenization;
using Sushi.Parsing.Syntax;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Locates an invocation at an editor position using source tokens and the
/// compiler-established set of visible callable signatures.
/// </summary>
internal static class SourceSignatureHelpResolver
{
    /// <summary>
    /// Resolves the innermost active invocation without requiring its
    /// arguments or enclosing function body to bind successfully.
    /// </summary>
    /// <param name="analysis">Analysis of the authoritative source snapshot.</param>
    /// <param name="offset">The cursor's UTF-8 byte offset.</param>
    /// <param name="cancellationToken">Cancels lexical position inspection.</param>
    /// <returns>The unique visible callable and active parameter, if established.</returns>
    internal static SourceSignatureHelpResult? Resolve(SourceAnalysisResult analysis, int offset, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        cancellationToken.ThrowIfCancellationRequested();

        SourceSnapshot snapshot = analysis.Snapshot;
        if (!snapshot.IsValidUtf8 || offset < 0 || offset > snapshot.SourceLength
            || analysis.SyntaxTree.Root is not SourceFileSyntax source
            || !source.FunctionDeclarations.Any(function =>
                function.Body.Span.Start <= offset && offset <= function.Body.Span.End))
        {
            return null;
        }

        IReadOnlyList<LexToken> tokens = analysis.SyntaxTree.LexerResult.Tokens;
        if (IsInsideComment(tokens, offset))
        {
            return null;
        }

        List<int> openParentheses = [];
        ReadOnlySpan<byte> bytes = snapshot.Bytes.Span;

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

            switch (bytes[token.Span.Start])
            {
                case (byte)'(':
                    openParentheses.Add(index);
                    break;
                case (byte)')' when openParentheses.Count > 0:
                    openParentheses.RemoveAt(openParentheses.Count - 1);
                    break;
            }
        }

        for (int index = openParentheses.Count - 1; index >= 0; index--)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int openingIndex = openParentheses[index];
            int targetIndex = PreviousSignificant(tokens, openingIndex - 1);
            if (targetIndex < 0 || !IsIdentifier(tokens[targetIndex]))
            {
                continue;
            }

            LexToken target = tokens[targetIndex];
            int beforeTarget = PreviousSignificant(tokens, targetIndex - 1);

            if (beforeTarget >= 0 && IsPunctuation(tokens[beforeTarget], bytes, (byte)'.'))
            {
                return null;
            }

            if (source.FunctionDeclarations.Any(declaration => declaration.Name.Span.Start == target.Span.Start))
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
            int? activeParameter = FindActiveParameter(tokens, openingIndex, offset, callable.Function.Parameters, cancellationToken);
            return new SourceSignatureHelpResult(callable, activeParameter);
        }

        return null;
    }

    private static int? FindActiveParameter(
        IReadOnlyList<LexToken> tokens,
        int openingIndex,
        int offset,
        IReadOnlyList<BoundParameter> parameters,
        CancellationToken cancellationToken)
    {
        ReadOnlySpan<byte> bytes = tokens[openingIndex].Span.Snapshot.Bytes.Span;
        int segmentStart = openingIndex + 1;
        int nesting = 0;
        int positionalIndex = 0;
        bool namedSeen = false;

        for (int index = segmentStart; index < tokens.Count; index++)
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

            byte punctuation = bytes[token.Span.Start];

            if (punctuation is (byte)'(' or (byte)'[' or (byte)'{')
            {
                nesting++;
            }
            else if (punctuation is (byte)')' or (byte)']' or (byte)'}')
            {
                if (nesting > 0)
                {
                    nesting--;
                }
            }
            else if (punctuation == (byte)',' && nesting == 0)
            {
                string? name = GetNamedArgument(tokens, segmentStart, index, bytes);

                if (name is not null)
                {
                    namedSeen = true;
                    if (!parameters.Any(parameter => StringComparer.Ordinal.Equals(parameter.Name, name)))
                    {
                        return null;
                    }
                }
                else
                {
                    if (namedSeen)
                    {
                        return null;
                    }

                    positionalIndex++;
                }

                segmentStart = index + 1;
            }
        }

        int segmentEnd = segmentStart;
        while (segmentEnd < tokens.Count && tokens[segmentEnd].Span.End <= offset)
        {
            segmentEnd++;
        }

        string? currentName = GetNamedArgument(tokens, segmentStart, segmentEnd, bytes);

        if (currentName is not null)
        {
            BoundParameter? namedParameter = parameters.FirstOrDefault(parameter =>
                StringComparer.Ordinal.Equals(parameter.Name, currentName));

            return namedParameter?.Index;
        }

        if (namedSeen)
        {
            return null;
        }

        return positionalIndex < parameters.Count ? positionalIndex : null;
    }

    private static string? GetNamedArgument(IReadOnlyList<LexToken> tokens, int start, int end, ReadOnlySpan<byte> bytes)
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
}

/// <summary>
/// Represents a uniquely selected signature and the declared parameter
/// corresponding to the argument currently being edited.
/// </summary>
/// <param name="Callable">The compiler-visible function declaration.</param>
/// <param name="ActiveParameter">The declared parameter index, if identifiable.</param>
internal sealed record SourceSignatureHelpResult(PackageCallable Callable, int? ActiveParameter);
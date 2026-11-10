using System.Text;
using Sushi.Analysis;
using Sushi.Lexing.Tokenization;
using Sushi.Parsing.Syntax;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Finds named-argument labels that remain valid at the current invocation argument position.
/// </summary>
internal static class SourceCompletionArgumentLabelResolver
{
    /// <summary>
    /// Resolves labels for a uniquely visible direct package-function invocation.
    /// </summary>
    /// <param name="analysis">The current project-aware analysis.</param>
    /// <param name="owner">The function containing the completion position.</param>
    /// <param name="position">The cursor's canonical UTF-8 byte offset.</param>
    /// <param name="cancellationToken">Cancels source traversal.</param>
    /// <returns>Label candidates and value-suppression state, or null outside a reliable label position.</returns>
    internal static SourceCompletionArgumentLabelResolution? Resolve(
        SourceAnalysisResult analysis, FunctionDeclarationSyntax owner, int position,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(owner);
        cancellationToken.ThrowIfCancellationRequested();

        SourceSnapshot snapshot = analysis.Snapshot;
        IReadOnlyList<LexToken> tokens = analysis.SyntaxTree.LexerResult.Tokens;
        if (!snapshot.IsValidUtf8 || position < owner.Body.OpenBraceToken.Span.End
            || position > owner.Body.Span.End || IsInsideComment(tokens, position))
        {
            return null;
        }

        (int Opening, int Target)? activeTokens =
            FindInnermostInvocation(tokens, snapshot, owner, position, cancellationToken);
        if (activeTokens is null)
        {
            return null;
        }

        (int opening, int target) = activeTokens.Value;

        int targetStart = tokens[target].Span.Start;
        if (targetStart < owner.Body.OpenBraceToken.Span.End)
        {
            return null;
        }

        string? targetName = BindIdentifier(tokens[target]);
        if (targetName is null)
        {
            return null;
        }

        int previousTarget = PreviousSignificant(tokens, target - 1);
        if (previousTarget >= 0 && IsPunctuation(tokens[previousTarget], snapshot.Bytes.Span, (byte)'.'))
        {
            return null;
        }

        SourceSignatureHelpResult? signature =
            SourceSignatureHelpResolver.Resolve(analysis, position, cancellationToken);
        if (signature is null || !StringComparer.Ordinal.Equals(signature.Callable.Name, targetName)
            || !HasReliableParameters(signature.Callable))
        {
            return null;
        }

        PackageCallable callable = signature.Callable;
        List<(int Start, int End)> completedArguments = [];
        int currentStart = opening + 1;
        List<Delimiter> nested = [];

        for (int index = currentStart; index < tokens.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LexToken token = tokens[index];
            if (token.Span.End > position)
            {
                break;
            }

            if (IsTrivia(token))
            {
                continue;
            }

            if (TryGetDelimiter(token, snapshot.Bytes.Span, out byte character))
            {
                if (character is (byte)'(' or (byte)'[' or (byte)'{')
                {
                    nested.Add(new Delimiter(character, index));
                    continue;
                }

                if (nested.Count > 0)
                {
                    if (!Matches(nested[^1].Character, character))
                    {
                        return null;
                    }

                    nested.RemoveAt(nested.Count - 1);
                    continue;
                }

                if (character is (byte)')' or (byte)'}' or (byte)']')
                {
                    return null;
                }

                if (character == (byte)';')
                {
                    return null;
                }
            }

            if (nested.Count == 0 && IsPunctuation(token, snapshot.Bytes.Span, (byte)','))
            {
                completedArguments.Add((currentStart, index));
                currentStart = index + 1;
            }
        }

        bool[] supplied = new bool[callable.Function.Parameters.Count];
        int nextPositional = 0;
        bool namedSeen = false;

        foreach ((int start, int end) in completedArguments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryMapCompletedArgument(tokens, snapshot, start, end, callable.Function.Parameters,
                supplied, ref nextPositional, ref namedSeen))
            {
                return null;
            }
        }

        int currentEnd = FindCurrentArgumentEnd(tokens, snapshot, currentStart, cancellationToken);
        LabelPosition? labelPosition = FindLabelPosition(tokens, snapshot, currentStart, currentEnd, position);
        if (labelPosition is null)
        {
            return null;
        }

        SourceCompletionArgumentLabelCandidate[] candidates =
        [
            .. callable.Function.Parameters
                .Where((_, index) => !supplied[index])
                .Where(parameter => parameter.Name.StartsWith(labelPosition.Value.Prefix, StringComparison.Ordinal))
                .Select(parameter =>
                {
                    ParameterSyntax declaration = callable.Declaration.Parameters[parameter.Index];
                    string spelling = GetIdentifierSpelling(declaration.NameToken, parameter.Name);
                    return new SourceCompletionArgumentLabelCandidate(
                        parameter.Name, spelling, parameter.Type, labelPosition.Value.Replacement);
                })
        ];

        return new SourceCompletionArgumentLabelResolution(
            candidates, namedSeen || labelPosition.Value.HasExistingColon);
    }

    private static (int Opening, int Target)? FindInnermostInvocation(
        IReadOnlyList<LexToken> tokens, SourceSnapshot snapshot, FunctionDeclarationSyntax owner,
        int position, CancellationToken cancellationToken)
    {
        int bodyOpenIndex = FindTokenIndex(tokens, owner.Body.OpenBraceToken);
        if (bodyOpenIndex < 0)
        {
            return null;
        }

        List<Delimiter> openDelimiters = [];
        for (int index = bodyOpenIndex + 1; index < tokens.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LexToken token = tokens[index];
            if (token.Span.End > position)
            {
                break;
            }

            if (IsTrivia(token) || !TryGetDelimiter(token, snapshot.Bytes.Span, out byte character))
            {
                continue;
            }

            if (character is (byte)'(' or (byte)'[' or (byte)'{')
            {
                openDelimiters.Add(new Delimiter(character, index));
            }
            else if (openDelimiters.Count > 0 && Matches(openDelimiters[^1].Character, character))
            {
                openDelimiters.RemoveAt(openDelimiters.Count - 1);
            }
            else if (character is (byte)')' or (byte)']' or (byte)'}')
            {
                return null;
            }
            else if (character is (byte)';')
            {
                int forHeader = openDelimiters.FindLastIndex(delimiter =>
                    delimiter.Character == (byte)'(' && IsForHeaderOpening(tokens, snapshot, delimiter.TokenIndex));
                int resetAfter = forHeader >= 0
                    ? forHeader
                    : openDelimiters.FindLastIndex(delimiter => delimiter.Character == (byte)'{');

                if (resetAfter + 1 < openDelimiters.Count)
                {
                    openDelimiters.RemoveRange(resetAfter + 1, openDelimiters.Count - resetAfter - 1);
                }
            }
        }

        if (openDelimiters.Count == 0 || openDelimiters[^1].Character != (byte)'(')
        {
            return null;
        }

        int opening = openDelimiters[^1].TokenIndex;
        int target = PreviousSignificant(tokens, opening - 1);
        if (target < 0 || !IsIdentifier(tokens[target]))
        {
            return null;
        }

        return (opening, target);
    }

    private static bool TryMapCompletedArgument(
        IReadOnlyList<LexToken> tokens, SourceSnapshot snapshot, int start, int end,
        IReadOnlyList<BoundParameter> parameters, bool[] supplied,
        ref int nextPositional, ref bool namedSeen)
    {
        int first = NextSignificant(tokens, start, end);
        if (first < 0)
        {
            return false;
        }

        int second = NextSignificant(tokens, first + 1, end);
        if (IsIdentifier(tokens[first]) && second >= 0
            && IsPunctuation(tokens[second], snapshot.Bytes.Span, (byte)':'))
        {
            string? name = BindIdentifier(tokens[first]);
            int parameterIndex = name is null ? -1 : FindParameter(parameters, name);
            if (parameterIndex < 0 || supplied[parameterIndex])
            {
                return false;
            }

            namedSeen = true;
            supplied[parameterIndex] = true;
            return true;
        }

        if (namedSeen || nextPositional >= parameters.Count || supplied[nextPositional])
        {
            return false;
        }

        supplied[nextPositional] = true;
        nextPositional++;
        return true;
    }

    private static LabelPosition? FindLabelPosition(
        IReadOnlyList<LexToken> tokens, SourceSnapshot snapshot, int start, int end, int position)
    {
        int first = NextSignificant(tokens, start, end);
        if (first < 0)
        {
            return new LabelPosition(string.Empty, new SourceSpan(snapshot, position, position), false);
        }

        if (!IsIdentifier(tokens[first]))
        {
            return null;
        }

        int second = NextSignificant(tokens, first + 1, end);
        bool hasColon = second >= 0 && IsPunctuation(tokens[second], snapshot.Bytes.Span, (byte)':');
        LexToken label = tokens[first];
        if (hasColon && position > tokens[second].Span.Start)
        {
            return null;
        }

        if (!hasColon && second >= 0)
        {
            return null;
        }

        if (position < label.Span.Start || position > label.Span.End)
        {
            return null;
        }

        int prefixStart = label.Type is LexTokenType.EscapedIdentifier
            ? label.Span.Start + 1
            : label.Span.Start;
        int prefixEnd = Math.Clamp(position, prefixStart, label.Span.End);
        string prefix = Encoding.UTF8.GetString(snapshot.Bytes.Span[prefixStart..prefixEnd]);
        return new LabelPosition(prefix, label.Span, hasColon);
    }

    private static int FindCurrentArgumentEnd(
        IReadOnlyList<LexToken> tokens, SourceSnapshot snapshot, int start,
        CancellationToken cancellationToken)
    {
        List<byte> nested = [];
        for (int index = start; index < tokens.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LexToken token = tokens[index];
            if (IsTrivia(token) || !TryGetDelimiter(token, snapshot.Bytes.Span, out byte character))
            {
                if (nested.Count == 0
                    && (IsPunctuation(token, snapshot.Bytes.Span, (byte)',')
                        || IsPunctuation(token, snapshot.Bytes.Span, (byte)';')))
                {
                    return index;
                }

                continue;
            }

            if (character is (byte)'(' or (byte)'[' or (byte)'{')
            {
                nested.Add(character);
                continue;
            }

            if (nested.Count == 0)
            {
                if (character is (byte)')' or (byte)']' or (byte)'}' or (byte)';')
                {
                    return index;
                }

                continue;
            }

            if (!Matches(nested[^1], character))
            {
                return index;
            }

            nested.RemoveAt(nested.Count - 1);
        }

        return tokens.Count;
    }

    private static bool IsForHeaderOpening(
        IReadOnlyList<LexToken> tokens, SourceSnapshot snapshot, int openingIndex)
    {
        int previous = PreviousSignificant(tokens, openingIndex - 1);
        if (previous < 0 || tokens[previous].Type is not LexTokenType.Keyword)
        {
            return false;
        }

        ReadOnlySpan<byte> spelling = snapshot.Bytes.Span[tokens[previous].Span.Start..tokens[previous].Span.End];
        return spelling.SequenceEqual("for"u8);
    }

    private static bool HasReliableParameters(PackageCallable callable)
    {
        if (!callable.Declaration.Name.IsSourceBacked
            || !StringComparer.Ordinal.Equals(IdentifierBinder.Bind(callable.Declaration.Name), callable.Name)
            || callable.Declaration.Parameters.Count != callable.Function.Parameters.Count)
        {
            return false;
        }

        HashSet<string> names = new(StringComparer.Ordinal);
        for (int index = 0; index < callable.Function.Parameters.Count; index++)
        {
            ParameterSyntax declaration = callable.Declaration.Parameters[index];
            BoundParameter parameter = callable.Function.Parameters[index];
            if (!declaration.NameToken.IsSourceBacked
                || !StringComparer.Ordinal.Equals(IdentifierBinder.Bind(declaration.NameToken), parameter.Name)
                || parameter.Index != index
                || !names.Add(parameter.Name))
            {
                return false;
            }
        }

        return true;
    }

    private static string GetIdentifierSpelling(SyntaxToken token, string semanticName)
        => token.Type is SyntaxType.EscapedIdentifierToken ? "@" + semanticName : semanticName;

    private static string? BindIdentifier(LexToken token)
        => IdentifierBinder.Bind(new SyntaxToken(
            token.Type is LexTokenType.EscapedIdentifier ? SyntaxType.EscapedIdentifierToken : SyntaxType.IdentifierToken,
            token));

    private static int FindParameter(IReadOnlyList<BoundParameter> parameters, string name)
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

    private static int FindTokenIndex(IReadOnlyList<LexToken> tokens, SyntaxToken syntaxToken)
    {
        for (int index = 0; index < tokens.Count; index++)
        {
            if (tokens[index].Span.Start == syntaxToken.Span.Start
                && tokens[index].Span.End == syntaxToken.Span.End)
            {
                return index;
            }
        }

        return -1;
    }

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

    private static int NextSignificant(IReadOnlyList<LexToken> tokens, int index, int end)
    {
        for (; index < end; index++)
        {
            if (!IsTrivia(tokens[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsIdentifier(LexToken token)
        => token.Type is LexTokenType.Identifier or LexTokenType.EscapedIdentifier;

    private static bool IsTrivia(LexToken token)
        => token.Type is LexTokenType.Whitespace or LexTokenType.LineTerminator
            or LexTokenType.LineComment or LexTokenType.DocumentationLineComment or LexTokenType.BlockComment;

    private static bool IsInsideComment(IReadOnlyList<LexToken> tokens, int offset)
        => tokens.Any(token => token.Span.Start <= offset && offset < token.Span.End
            && token.Type is LexTokenType.LineComment or LexTokenType.DocumentationLineComment or LexTokenType.BlockComment);

    private static bool TryGetDelimiter(LexToken token, ReadOnlySpan<byte> bytes, out byte character)
    {
        character = 0;
        if (token.Type is not LexTokenType.Punctuation || token.Span.Length != 1)
        {
            return false;
        }

        character = bytes[token.Span.Start];
        return character is (byte)'(' or (byte)')' or (byte)'[' or (byte)']' or (byte)'{' or (byte)'}' or (byte)';';
    }

    private static bool IsPunctuation(LexToken token, ReadOnlySpan<byte> bytes, byte punctuation)
        => token.Type is LexTokenType.Punctuation && token.Span.Length == 1 && bytes[token.Span.Start] == punctuation;

    private static bool Matches(byte opening, byte closing)
        => opening switch
        {
            (byte)'(' => closing == (byte)')',
            (byte)'[' => closing == (byte)']',
            (byte)'{' => closing == (byte)'}',
            _ => false
        };

    private readonly record struct Delimiter(byte Character, int TokenIndex);
    private readonly record struct LabelPosition(string Prefix, SourceSpan Replacement, bool HasExistingColon);
}

/// <summary>
/// A parameter label presented as a completion candidate, separate from value bindings.
/// </summary>
/// <param name="Name">The compiler-decoded parameter identity.</param>
/// <param name="Spelling">The valid source spelling to insert.</param>
/// <param name="Type">The declared parameter type.</param>
/// <param name="Replacement">The source span replaced when accepted.</param>
internal sealed record SourceCompletionArgumentLabelCandidate(
    string Name, string Spelling, BoundType Type, SourceSpan Replacement);

/// <summary>
/// Label candidates at one invocation argument start and whether named arguments prohibit positional values.
/// </summary>
/// <param name="Candidates">Valid unsupplied parameter labels.</param>
/// <param name="SuppressValueCompletions">Whether remaining arguments must be named.</param>
internal sealed record SourceCompletionArgumentLabelResolution(
    IReadOnlyList<SourceCompletionArgumentLabelCandidate> Candidates, bool SuppressValueCompletions);

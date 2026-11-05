using Sushi.Analysis;
using Sushi.Parsing.Syntax;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.Documentation;

/// <summary>
/// Describes the declaration metadata needed to author a documentation scaffold.
/// </summary>
/// <param name="ParameterNames">The named parameters that require descriptions, in declaration order.</param>
/// <param name="HasReturnDescription">Whether the declaration has a supported non-void return value.</param>
public sealed record DocumentationScaffold(IReadOnlyList<string> ParameterNames, bool HasReturnDescription);

/// <summary>
/// Resolves an attached, newly typed documentation line to a complete supported declaration.
/// </summary>
public static class DocumentationScaffoldResolver
{
    /// <summary>
    /// Finds a complete declaration attached to a single triple-slash line at the requested position.
    /// </summary>
    /// <param name="analysis">Compiler analysis for the current source snapshot.</param>
    /// <param name="line">The zero-based line containing the typed documentation introducer.</param>
    /// <param name="character">The zero-based UTF-16 position immediately after the third slash.</param>
    /// <param name="cancellationToken">Cancels analysis traversal.</param>
    /// <returns>Scaffold metadata, or null when the line is detached, already part of documentation, or incompatible.</returns>
    public static DocumentationScaffold? Resolve(SourceAnalysisResult analysis, int line, int character, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        cancellationToken.ThrowIfCancellationRequested();

        SourceSnapshot snapshot = analysis.Snapshot;
        if (line < 0 || line >= snapshot.LineStarts.Count)
        {
            return null;
        }

        int lineStart = snapshot.LineStarts[line];
        int lineEnd = line + 1 < snapshot.LineStarts.Count ? snapshot.LineStarts[line + 1] : snapshot.SourceLength;
        ReadOnlySpan<byte> bytes = snapshot.Bytes.Span;
        if (lineEnd > lineStart && bytes[lineEnd - 1] == (byte)'\n')
        {
            lineEnd--;
        }

        if (lineEnd > lineStart && bytes[lineEnd - 1] == (byte)'\r')
        {
            lineEnd--;
        }

        int cursor;
        try
        {
            cursor = snapshot.GetByteOffset(line, character);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        int slashStart = cursor - 3;
        if (cursor != lineEnd || slashStart < lineStart
            || !bytes.Slice(slashStart, 3).SequenceEqual("///"u8)
            || !IsIndentation(bytes[lineStart..slashStart]))
        {
            return null;
        }

        DocumentationBlock? triggerBlock = analysis.DocumentationBlocks.FirstOrDefault(block =>
            block.LineSpans.Count == 1 && block.LineSpans[0].Start == slashStart && block.IsAttached);
        if (triggerBlock?.Declaration is not SyntaxNode declaration
            || analysis.DocumentationBlocks.Any(block => !ReferenceEquals(block, triggerBlock)
                && ReferenceEquals(block.Declaration, declaration)))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return declaration switch
        {
            FunctionDeclarationSyntax function => ResolveCallable(function.AccessModifier, function.ReturnType, function.Name,
                function.OpenParenthesisToken, function.CloseParenthesisToken, function.ParameterSeparators, function.Parameters, function.Body, cancellationToken),
            ActivityMethodDeclarationSyntax method => ResolveCallable(method.AccessModifier, method.ReturnType, method.Name,
                method.OpenParenthesisToken, method.CloseParenthesisToken, method.ParameterSeparators, method.Parameters, method.Body, cancellationToken),
            ActivityDeclarationSyntax activity when IsCompleteActivity(activity) => new DocumentationScaffold([], false),
            _ => null
        };
    }

    private static DocumentationScaffold? ResolveCallable(
        SyntaxToken accessModifier,
        SyntaxToken returnType,
        SyntaxToken name,
        SyntaxToken openParenthesis,
        SyntaxToken closeParenthesis,
        IReadOnlyList<SyntaxToken> parameterSeparators,
        IReadOnlyList<ParameterSyntax> parameters,
        BlockSyntax body,
        CancellationToken cancellationToken)
    {
        if (!accessModifier.IsSourceBacked || !returnType.IsSourceBacked || !name.IsSourceBacked
            || !openParenthesis.IsSourceBacked || !closeParenthesis.IsSourceBacked
            || parameterSeparators.Any(separator => !separator.IsSourceBacked)
            || !body.OpenBraceToken.IsSourceBacked || !body.CloseBraceToken.IsSourceBacked
            || !IsSupportedType(returnType) || !IsIdentifier(name))
        {
            return null;
        }

        List<string> parameterNames = [];
        foreach (ParameterSyntax parameter in parameters)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!parameter.TypeToken.IsSourceBacked || !IsSupportedType(parameter.TypeToken))
            {
                return null;
            }

            if (IsDiscardParameter(parameter.NameToken))
            {
                continue;
            }

            if (!parameter.NameToken.IsSourceBacked || !IsIdentifier(parameter.NameToken))
            {
                return null;
            }

            string? parameterName = IdentifierBinder.Bind(parameter.NameToken);
            if (parameterName is null)
            {
                return null;
            }

            parameterNames.Add(parameterName);
        }

        return new DocumentationScaffold(Array.AsReadOnly(parameterNames.ToArray()), returnType.Type is not SyntaxType.VoidKeyword);
    }

    private static bool IsDiscardParameter(SyntaxToken name)
        => name.IsSourceBacked
            && name.Span.Snapshot.Bytes.Span[name.Span.Start..name.Span.End].SequenceEqual("_"u8);

    private static bool IsCompleteActivity(ActivityDeclarationSyntax activity)
        => activity.AccessModifier.IsSourceBacked
            && activity.Name.IsSourceBacked
            && activity.ActivityKeyword.IsSourceBacked
            && activity.ColonToken.IsSourceBacked
            && activity.LifetimeKeyword.IsSourceBacked
            && activity.OpenBraceToken.IsSourceBacked
            && activity.CloseBraceToken.IsSourceBacked
            && (activity.RootKeyword is null || activity.RootKeyword.Value.IsSourceBacked);

    private static bool IsSupportedType(SyntaxToken token)
        => token.Type is SyntaxType.VoidKeyword
            || IdentifierBinder.Bind(token) is "int32" or "bool";

    private static bool IsIdentifier(SyntaxToken token)
        => token.Type is (SyntaxType.IdentifierToken or SyntaxType.EscapedIdentifierToken)
            && IdentifierBinder.Bind(token) is not null;

    private static bool IsIndentation(ReadOnlySpan<byte> prefix)
    {
        foreach (byte value in prefix)
        {
            if (value is not (byte)' ' and not (byte)'\t')
            {
                return false;
            }
        }

        return true;
    }
}
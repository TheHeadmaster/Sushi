using Sushi.Analysis;
using Sushi.Lexing.Tokenization;
using Sushi.Parsing.Syntax;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Determines the required value type at supported expression-completion
/// positions without treating unfinished source as a valid expression.
/// </summary>
internal static class SourceCompletionExpressionContextResolver
{
    /// <summary>
    /// Resolves the required type following a return keyword, assignment
    /// operator, or invocation argument separator.
    /// </summary>
    /// <param name="analysis">The current project-aware source analysis.</param>
    /// <param name="owner">The enclosing package-level function.</param>
    /// <param name="position">The UTF-8 start of the completion replacement.</param>
    /// <param name="bindings">Compiler-validated bindings visible at the cursor.</param>
    /// <param name="cancellationToken">Cancels context inspection.</param>
    /// <returns>The required concrete type, or null when it cannot be established.</returns>
    internal static BoundType? Resolve(
        SourceAnalysisResult analysis, FunctionDeclarationSyntax owner, int position,
        IReadOnlyList<SourceCompletionBinding> bindings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(bindings);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<LexToken> tokens = analysis.SyntaxTree.LexerResult.Tokens;
        if (IsInsideNonExpressionToken(tokens, position))
        {
            return null;
        }

        int previous = PreviousSignificant(tokens, position, cancellationToken);
        if (previous < 0)
        {
            return null;
        }

        LexToken token = tokens[previous];
        ReadOnlySpan<byte> spelling = analysis.Snapshot.Bytes.Span[token.Span.Start..token.Span.End];

        if (token.Type is LexTokenType.Keyword && spelling.SequenceEqual("return"u8))
        {
            if (position == token.Span.End)
            {
                return null;
            }

            PackageCallable? callable = analysis.VisibleCallables.FirstOrDefault(candidate =>
                ReferenceEquals(candidate.Declaration, owner));

            return callable is null || callable.Function.ReturnType is BoundType.Void
                ? null
                : callable.Function.ReturnType;
        }

        if (token.Type is LexTokenType.Operator && IsAssignmentOperator(spelling))
        {
            return FindAssignmentType(owner.Body, token.Span, bindings, cancellationToken);
        }

        if (token.Type is LexTokenType.Punctuation
            && spelling.Length == 1
            && spelling[0] is (byte)'(' or (byte)',' or (byte)':')
        {
            SourceSignatureHelpResult? signature = SourceSignatureHelpResolver.Resolve(
                analysis, position, cancellationToken);

            if (signature?.ActiveParameter is not int parameterIndex
                || parameterIndex < 0
                || parameterIndex >= signature.Callable.Function.Parameters.Count)
            {
                return null;
            }

            BoundType type = signature.Callable.Function.Parameters[parameterIndex].Type;
            return type is BoundType.Void ? null : type;
        }

        return null;
    }

    private static BoundType? FindAssignmentType(
        BlockSyntax block, SourceSpan operatorSpan,
        IReadOnlyList<SourceCompletionBinding> bindings, CancellationToken cancellationToken)
    {
        foreach (StatementSyntax statement in block.Statements)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (statement.Span.Start > operatorSpan.Start || statement.Span.End < operatorSpan.End)
            {
                continue;
            }

            switch (statement)
            {
                case LocalDeclarationStatementSyntax declaration
                    when IsSameToken(declaration.EqualsToken, operatorSpan):
                    return GetDeclaredType(declaration.TypeToken);

                case LocalAssignmentStatementSyntax assignment
                    when IsSameToken(assignment.OperatorToken, operatorSpan):
                    return GetWritableLocalType(assignment.NameToken, bindings);

                case IfStatementSyntax conditional:
                    return FindAssignmentType(conditional, operatorSpan, bindings, cancellationToken);

                case WhileStatementSyntax loop:
                    return FindAssignmentType(loop.Body, operatorSpan, bindings, cancellationToken);

                case DoWhileStatementSyntax loop:
                    return FindAssignmentType(loop.Body, operatorSpan, bindings, cancellationToken);

                case ForStatementSyntax loop:
                    return FindAssignmentType(loop, operatorSpan, bindings, cancellationToken);
            }
        }

        return null;
    }

    private static BoundType? FindAssignmentType(
        IfStatementSyntax conditional, SourceSpan operatorSpan,
        IReadOnlyList<SourceCompletionBinding> bindings, CancellationToken cancellationToken)
    {
        BoundType? result = FindAssignmentType(conditional.ThenBlock, operatorSpan, bindings, cancellationToken);

        if (result is not null)
        {
            return result;
        }

        if (conditional.ElseBlock is BlockSyntax elseBlock)
        {
            return FindAssignmentType(elseBlock, operatorSpan, bindings, cancellationToken);
        }

        return conditional.ElseIf is IfStatementSyntax elseIf
            ? FindAssignmentType(elseIf, operatorSpan, bindings, cancellationToken)
            : null;
    }

    private static BoundType? FindAssignmentType(
        ForStatementSyntax loop, SourceSpan operatorSpan,
        IReadOnlyList<SourceCompletionBinding> bindings, CancellationToken cancellationToken)
    {
        foreach (ForHeaderOperationSyntax operation in loop.Initialization.Concat(loop.Iteration))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsSameToken(operation.OperatorToken, operatorSpan))
            {
                continue;
            }

            return operation.TypeToken is SyntaxToken declaredType
                ? GetDeclaredType(declaredType)
                : GetWritableLocalType(operation.NameToken, bindings);
        }

        return FindAssignmentType(loop.Body, operatorSpan, bindings, cancellationToken);
    }

    private static BoundType? GetDeclaredType(SyntaxToken token)
        => IdentifierBinder.Bind(token) switch
        {
            "int32" => BoundType.Int32,
            "bool" => BoundType.Bool,
            _ => null
        };

    private static BoundType? GetWritableLocalType(
        SyntaxToken nameToken, IReadOnlyList<SourceCompletionBinding> bindings)
    {
        string? name = IdentifierBinder.Bind(nameToken);
        if (name is null)
        {
            return null;
        }

        return bindings.FirstOrDefault(binding =>
            binding.Kind is SourceCompletionBindingKind.Local
            && StringComparer.Ordinal.Equals(binding.Name, name))?.Type;
    }

    private static bool IsSameToken(SyntaxToken token, SourceSpan span)
        => token.IsSourceBacked
            && ReferenceEquals(token.Span.Snapshot, span.Snapshot)
            && token.Span.Start == span.Start
            && token.Span.End == span.End;

    private static bool IsAssignmentOperator(ReadOnlySpan<byte> spelling)
        => spelling.Length switch
        {
            1 => spelling[0] == (byte)'=',
            2 => spelling[1] == (byte)'='
                && spelling[0] is (byte)'+' or (byte)'-' or (byte)'*' or (byte)'/'
                    or (byte)'%' or (byte)'&' or (byte)'^' or (byte)'|',
            3 => spelling[2] == (byte)'='
                && spelling[0] is (byte)'<' or (byte)'>'
                && spelling[1] == spelling[0],
            _ => false
        };

    private static int PreviousSignificant(
        IReadOnlyList<LexToken> tokens, int position, CancellationToken cancellationToken)
    {
        for (int index = tokens.Count - 1; index >= 0; index--)
        {
            cancellationToken.ThrowIfCancellationRequested();

            LexToken token = tokens[index];
            if (token.Span.End <= position && !IsTrivia(token))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsInsideNonExpressionToken(IReadOnlyList<LexToken> tokens, int position)
    {
        foreach (LexToken token in tokens)
        {
            if (token.Span.Start > position)
            {
                break;
            }

            if (token.Span.Start <= position && position < token.Span.End
                && token.Type is (LexTokenType.LineComment or LexTokenType.DocumentationLineComment
                    or LexTokenType.BlockComment or LexTokenType.Unknown))
            {
                return true;
            }

            if (token.Span.Start < position && position < token.Span.End
                && !IsTrivia(token))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTrivia(LexToken token)
        => token.Type is LexTokenType.Whitespace
            or LexTokenType.LineTerminator
            or LexTokenType.LineComment
            or LexTokenType.DocumentationLineComment
            or LexTokenType.BlockComment;
}
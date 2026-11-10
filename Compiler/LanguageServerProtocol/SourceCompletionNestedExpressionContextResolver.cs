using Sushi.Analysis;
using Sushi.Lexing.Tokenization;
using Sushi.Parsing.Syntax;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Propagates compiler-established expected types from statement contexts into
/// nested expression operands without interpreting unrelated source as syntax.
/// </summary>
internal static class SourceCompletionNestedExpressionContextResolver
{
    /// <summary>
    /// Finds the innermost supported expression position containing the cursor.
    /// </summary>
    /// <param name="analysis">The current project-aware source analysis.</param>
    /// <param name="owner">The parsed function containing the cursor.</param>
    /// <param name="position">The cursor's canonical UTF-8 byte offset.</param>
    /// <param name="bindings">Compiler-validated bindings visible at the cursor.</param>
    /// <param name="cancellationToken">Cancels syntax traversal and resolution.</param>
    /// <returns>A recognized context, including one whose expected type is unknown.</returns>
    internal static SourceCompletionExpressionContext Resolve(
        SourceAnalysisResult analysis, FunctionDeclarationSyntax owner, int position,
        IReadOnlyList<SourceCompletionBinding> bindings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(bindings);
        cancellationToken.ThrowIfCancellationRequested();

        PackageCallable? callable = analysis.VisibleCallables.FirstOrDefault(candidate =>
            ReferenceEquals(candidate.Declaration, owner));

        if (callable is null)
        {
            return default;
        }

        return FindInBlock(analysis, owner.Body, callable.Function.ReturnType, position, bindings, cancellationToken)
            ?? default;
    }

    private static SourceCompletionExpressionContext? FindInBlock(
        SourceAnalysisResult analysis, BlockSyntax block, BoundType returnType, int position,
        IReadOnlyList<SourceCompletionBinding> bindings, CancellationToken cancellationToken)
    {
        if (!Contains(block.Span, position))
        {
            return null;
        }

        foreach (StatementSyntax statement in block.Statements)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Contains(statement.Span, position))
            {
                continue;
            }

            SourceCompletionExpressionContext? result = FindInStatement(
                analysis, statement, returnType, position, bindings, cancellationToken);
            if (result is SourceCompletionExpressionContext context)
            {
                return context;
            }
        }

        return null;
    }

    private static SourceCompletionExpressionContext? FindInStatement(
        SourceAnalysisResult analysis, StatementSyntax statement, BoundType returnType, int position,
        IReadOnlyList<SourceCompletionBinding> bindings, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (statement)
        {
            case ReturnStatementSyntax returned:
                return FindReturnExpression(analysis, returned, returnType, position, bindings, cancellationToken);

            case LocalDeclarationStatementSyntax declaration:
                return FindDeclaredValue(analysis, declaration.Initializer, declaration.TypeToken,
                    declaration.EqualsToken, declaration.SemicolonToken, position, bindings, cancellationToken);

            case LocalAssignmentStatementSyntax assignment:
                return FindAssignedValue(analysis, assignment.Value, assignment.NameToken,
                    assignment.OperatorToken, assignment.SemicolonToken, position, bindings, cancellationToken);

            case ExpressionStatementSyntax expressionStatement:
                return ResolveExpression(analysis, expressionStatement.Expression, null, position, bindings, cancellationToken);

            case IfStatementSyntax conditional:
                return FindInConditional(analysis, conditional, returnType, position, bindings, cancellationToken);

            case WhileStatementSyntax loop:
                return FindCondition(analysis, loop.Condition, loop.OpenParenthesisToken,
                    loop.CloseParenthesisToken, BoundType.Bool, position, bindings, cancellationToken)
                    ?? FindInBlock(analysis, loop.Body, returnType, position, bindings, cancellationToken);

            case DoWhileStatementSyntax loop:
                return FindCondition(analysis, loop.Condition, loop.OpenParenthesisToken,
                    loop.CloseParenthesisToken, BoundType.Bool, position, bindings, cancellationToken)
                    ?? FindInBlock(analysis, loop.Body, returnType, position, bindings, cancellationToken);

            case ForStatementSyntax loop:
                return FindInFor(analysis, loop, returnType, position, bindings, cancellationToken);

            default:
                return null;
        }
    }

    private static SourceCompletionExpressionContext? FindReturnExpression(
        SourceAnalysisResult analysis, ReturnStatementSyntax returned, BoundType returnType, int position,
        IReadOnlyList<SourceCompletionBinding> bindings, CancellationToken cancellationToken)
    {
        if (position <= returned.ReturnKeyword.Span.End || position > returned.SemicolonToken.Span.Start)
        {
            return null;
        }

        if (returned.Expression is not ExpressionSyntax expression)
        {
            return RecognizedResult(returnType is BoundType.Void ? null : returnType);
        }

        if (!Contains(expression.Span, position) && !ContainsMissingSyntax(expression))
        {
            return null;
        }

        BoundType? expected = returnType is BoundType.Void ? null : returnType;
        return ResolveExpression(analysis, expression, expected, position, bindings, cancellationToken,
            allowIntegerBooleanStarter: expected is BoundType.Bool)
            ?? RecognizedResult(expected);
    }

    private static SourceCompletionExpressionContext? FindDeclaredValue(
        SourceAnalysisResult analysis, ExpressionSyntax expression, SyntaxToken typeToken,
        SyntaxToken equalsToken, SyntaxToken semicolonToken, int position,
        IReadOnlyList<SourceCompletionBinding> bindings, CancellationToken cancellationToken)
    {
        if (position < equalsToken.Span.End || position > semicolonToken.Span.Start)
        {
            return null;
        }

        BoundType? expected = GetDeclaredType(typeToken);
        if (Contains(expression.Span, position))
        {
            return ResolveExpression(analysis, expression, expected, position, bindings, cancellationToken,
                allowIntegerBooleanStarter: expected is BoundType.Bool);
        }

        return ContainsMissingSyntax(expression) ? RecognizedResult(expected) : null;
    }

    private static SourceCompletionExpressionContext? FindAssignedValue(
        SourceAnalysisResult analysis, ExpressionSyntax expression, SyntaxToken nameToken,
        SyntaxToken operatorToken, SyntaxToken semicolonToken, int position,
        IReadOnlyList<SourceCompletionBinding> bindings, CancellationToken cancellationToken)
    {
        if (position < operatorToken.Span.End || position > semicolonToken.Span.Start)
        {
            return null;
        }

        BoundType? expected = GetWritableBindingType(nameToken, bindings);
        if (Contains(expression.Span, position))
        {
            return ResolveExpression(analysis, expression, expected, position, bindings, cancellationToken,
                allowIntegerBooleanStarter: expected is BoundType.Bool);
        }

        return ContainsMissingSyntax(expression) ? RecognizedResult(expected) : null;
    }

    private static SourceCompletionExpressionContext? FindInConditional(
        SourceAnalysisResult analysis, IfStatementSyntax conditional, BoundType returnType, int position,
        IReadOnlyList<SourceCompletionBinding> bindings, CancellationToken cancellationToken)
    {
        SourceCompletionExpressionContext? result = FindCondition(
            analysis, conditional.Condition, conditional.OpenParenthesisToken,
            conditional.CloseParenthesisToken, BoundType.Bool, position, bindings, cancellationToken);

        if (result is not null)
        {
            return result;
        }

        result = FindInBlock(analysis, conditional.ThenBlock, returnType, position, bindings, cancellationToken);
        if (result is not null)
        {
            return result;
        }

        if (conditional.ElseBlock is BlockSyntax elseBlock)
        {
            result = FindInBlock(analysis, elseBlock, returnType, position, bindings, cancellationToken);
        }
        else if (conditional.ElseIf is IfStatementSyntax elseIf)
        {
            result = FindInConditional(analysis, elseIf, returnType, position, bindings, cancellationToken);
        }

        return result;
    }

    private static SourceCompletionExpressionContext? FindCondition(
        SourceAnalysisResult analysis, ExpressionSyntax expression, SyntaxToken openParenthesis,
        SyntaxToken closeParenthesis, BoundType expected, int position,
        IReadOnlyList<SourceCompletionBinding> bindings, CancellationToken cancellationToken)
    {
        if (position < openParenthesis.Span.End || position > closeParenthesis.Span.Start)
        {
            return null;
        }

        if (Contains(expression.Span, position))
        {
            return ResolveExpression(analysis, expression, expected, position, bindings, cancellationToken,
                allowIntegerBooleanStarter: expected is BoundType.Bool)
                ?? (ContainsMissingSyntax(expression) ? RecognizedResult(expected) : Recognized(null));
        }

        return ContainsMissingSyntax(expression) ? RecognizedResult(expected) : Recognized(null);
    }

    private static SourceCompletionExpressionContext? FindInFor(
        SourceAnalysisResult analysis, ForStatementSyntax loop, BoundType returnType, int position,
        IReadOnlyList<SourceCompletionBinding> bindings, CancellationToken cancellationToken)
    {
        foreach (ForHeaderOperationSyntax operation in loop.Initialization.Concat(loop.Iteration))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (operation.Value is not ExpressionSyntax value
                || position < operation.OperatorToken.Span.End
                || position > operation.Span.End)
            {
                continue;
            }

            BoundType? expected = operation.TypeToken is SyntaxToken typeToken
                ? GetDeclaredType(typeToken)
                : GetWritableBindingType(operation.NameToken, bindings);

            if (Contains(value.Span, position))
            {
                return ResolveExpression(analysis, value, expected, position, bindings, cancellationToken,
                    allowIntegerBooleanStarter: expected is BoundType.Bool);
            }

            if (ContainsMissingSyntax(value))
            {
                return RecognizedResult(expected);
            }
        }

        int conditionStart = loop.InitializationSemicolon.Span.End;
        int conditionEnd = loop.ConditionSemicolon.Span.Start;
        if (position >= conditionStart && position <= conditionEnd)
        {
            if (loop.Condition is ExpressionSyntax condition)
            {
                if (Contains(condition.Span, position))
                {
                    return ResolveExpression(analysis, condition, BoundType.Bool, position, bindings, cancellationToken,
                        allowIntegerBooleanStarter: true)
                        ?? (ContainsMissingSyntax(condition) ? RecognizedResult(BoundType.Bool) : Recognized(null));
                }

                return ContainsMissingSyntax(condition) ? RecognizedResult(BoundType.Bool) : Recognized(null);
            }

            return IsTriviaOnly(analysis, conditionStart, position)
                && IsTriviaOnly(analysis, position, conditionEnd)
                    ? RecognizedResult(BoundType.Bool)
                    : Recognized(null);
        }

        return FindInBlock(analysis, loop.Body, returnType, position, bindings, cancellationToken);
    }

    private static SourceCompletionExpressionContext? ResolveExpression(
        SourceAnalysisResult analysis, ExpressionSyntax expression, BoundType? expected,
        int position, IReadOnlyList<SourceCompletionBinding> bindings,
        CancellationToken cancellationToken, bool allowIntegerBooleanStarter = false)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Contains(expression.Span, position))
        {
            return null;
        }

        switch (expression)
        {
            case InvalidComparisonChainSyntax:
                return Recognized(null);

            case ParenthesizedExpressionSyntax grouped:
                if (position < grouped.OpenParenthesisToken.Span.End
                    || position > grouped.CloseParenthesisToken.Span.Start)
                {
                    return Recognized(null);
                }

                return ResolveExpression(analysis, grouped.Expression, expected, position, bindings, cancellationToken,
                    allowIntegerBooleanStarter: expected is BoundType.Bool)
                    ?? (ContainsMissingSyntax(grouped.Expression) ? RecognizedResult(expected) : Recognized(null));

            case UnaryExpressionSyntax unary:
                if (position < unary.OperatorToken.Span.End)
                {
                    return Recognized(null);
                }

                BoundType operandType = unary.OperatorToken.Type is SyntaxType.NotKeyword
                    ? BoundType.Bool
                    : BoundType.Int32;
                return ResolveExpression(analysis, unary.Operand, operandType, position, bindings, cancellationToken)
                    ?? (ContainsMissingSyntax(unary.Operand) ? Recognized(operandType) : Recognized(null));

            case BinaryExpressionSyntax binary:
                return ResolveBinaryOperand(analysis, binary, expected, position, bindings, cancellationToken);

            case InvocationExpressionSyntax invocation:
                return ResolveInvocationArgument(analysis, invocation, position, bindings, cancellationToken);

            case IntegerLiteralExpressionSyntax:
            case BooleanLiteralExpressionSyntax:
            case IdentifierExpressionSyntax:
            case IncrementDecrementExpressionSyntax:
                return Recognized(expected, allowIntegerBooleanStarter);

            default:
                return Recognized(null);
        }
    }

    private static SourceCompletionExpressionContext ResolveBinaryOperand(
        SourceAnalysisResult analysis, BinaryExpressionSyntax binary, BoundType? expected,
        int position, IReadOnlyList<SourceCompletionBinding> bindings,
        CancellationToken cancellationToken)
    {
        SourceSpan operatorSpan = binary.OperatorToken.Span;
        if (position > operatorSpan.Start && position < operatorSpan.End)
        {
            return Recognized(null);
        }

        bool useRight = position >= operatorSpan.End;
        ExpressionSyntax operand = useRight ? binary.Right : binary.Left;
        BoundType? operandType = GetOperandType(analysis, binary, expected, bindings, cancellationToken);

        bool allowIntegerStarter = binary.OperatorToken.Type is SyntaxType.AndKeyword or SyntaxType.OrKeyword;
        if (Contains(operand.Span, position))
        {
            return ResolveExpression(analysis, operand, operandType, position, bindings, cancellationToken,
                allowIntegerBooleanStarter: allowIntegerStarter)
                ?? Recognized(operandType, allowIntegerStarter);
        }

        return ContainsMissingSyntax(operand)
            ? Recognized(operandType, allowIntegerStarter)
            : Recognized(null);
    }

    private static BoundType? GetOperandType(
        SourceAnalysisResult analysis, BinaryExpressionSyntax binary, BoundType? expected,
        IReadOnlyList<SourceCompletionBinding> bindings, CancellationToken cancellationToken)
    {
        SyntaxToken operatorToken = binary.OperatorToken;
        if (operatorToken.Type is SyntaxType.AndKeyword or SyntaxType.OrKeyword)
        {
            return BoundType.Bool;
        }

        ReadOnlySpan<byte> spelling = operatorToken.Span.Snapshot.Bytes.Span[
            operatorToken.Span.Start..operatorToken.Span.End];

        if (spelling.SequenceEqual("+"u8) || spelling.SequenceEqual("-"u8)
            || spelling.SequenceEqual("*"u8) || spelling.SequenceEqual("/"u8)
            || spelling.SequenceEqual("%"u8) || spelling.SequenceEqual("<<"u8)
            || spelling.SequenceEqual(">>"u8)
            || spelling.SequenceEqual("<"u8) || spelling.SequenceEqual(">"u8)
            || spelling.SequenceEqual("<="u8) || spelling.SequenceEqual(">="u8)
            || spelling.SequenceEqual("=="u8) || spelling.SequenceEqual("!="u8))
        {
            return BoundType.Int32;
        }

        if (!spelling.SequenceEqual("&"u8) && !spelling.SequenceEqual("|"u8) && !spelling.SequenceEqual("^"u8))
        {
            return null;
        }

        BoundType? leftType = InferExpressionType(analysis, binary.Left, null, bindings, cancellationToken);
        BoundType? rightType = InferExpressionType(analysis, binary.Right, null, bindings, cancellationToken);
        if (leftType is BoundType left && rightType is BoundType right && left != right)
        {
            return null;
        }

        BoundType? established = leftType ?? rightType;
        if (expected is BoundType expectedType && established is BoundType establishedType && expectedType != establishedType)
        {
            return null;
        }

        return expected ?? established;
    }

    private static BoundType? InferExpressionType(
        SourceAnalysisResult analysis, ExpressionSyntax expression, BoundType? expected,
        IReadOnlyList<SourceCompletionBinding> bindings, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return expression switch
        {
            BooleanLiteralExpressionSyntax => BoundType.Bool,
            IntegerLiteralExpressionSyntax { LiteralToken.IsMissing: false } => BoundType.Int32,
            IdentifierExpressionSyntax identifier => GetBindingType(identifier.IdentifierToken, bindings),
            ParenthesizedExpressionSyntax grouped when !ContainsMissingSyntax(grouped.Expression) =>
                InferExpressionType(analysis, grouped.Expression, expected, bindings, cancellationToken),
            UnaryExpressionSyntax unary => unary.OperatorToken.Type is SyntaxType.NotKeyword
                ? BoundType.Bool
                : BoundType.Int32,
            InvocationExpressionSyntax invocation => InferInvocationType(analysis, invocation),
            BinaryExpressionSyntax binary => InferBinaryResultType(analysis, binary, expected, bindings, cancellationToken),
            _ => null
        };
    }

    private static BoundType? InferInvocationType(SourceAnalysisResult analysis, InvocationExpressionSyntax invocation)
    {
        if (invocation.Target is not IdentifierExpressionSyntax target
            || IdentifierBinder.Bind(target.IdentifierToken) is not string name)
        {
            return null;
        }

        PackageCallable[] callables = [.. analysis.VisibleCallables.Where(candidate =>
            StringComparer.Ordinal.Equals(candidate.Name, name))];

        return callables.Length == 1 && callables[0].Function.ReturnType is not BoundType.Void
            ? callables[0].Function.ReturnType
            : null;
    }

    private static BoundType? InferBinaryResultType(
        SourceAnalysisResult analysis, BinaryExpressionSyntax binary, BoundType? expected,
        IReadOnlyList<SourceCompletionBinding> bindings, CancellationToken cancellationToken)
    {
        if (binary.OperatorToken.Type is SyntaxType.AndKeyword or SyntaxType.OrKeyword)
        {
            return BoundType.Bool;
        }

        ReadOnlySpan<byte> spelling = binary.OperatorToken.Span.Snapshot.Bytes.Span[
            binary.OperatorToken.Span.Start..binary.OperatorToken.Span.End];

        if (spelling.SequenceEqual("<"u8) || spelling.SequenceEqual(">"u8)
            || spelling.SequenceEqual("<="u8) || spelling.SequenceEqual(">="u8)
            || spelling.SequenceEqual("=="u8) || spelling.SequenceEqual("!="u8))
        {
            return BoundType.Bool;
        }

        if (spelling.SequenceEqual("&"u8) || spelling.SequenceEqual("|"u8) || spelling.SequenceEqual("^"u8))
        {
            return GetOperandType(analysis, binary, expected, bindings, cancellationToken);
        }

        return BoundType.Int32;
    }

    private static SourceCompletionExpressionContext? ResolveInvocationArgument(
        SourceAnalysisResult analysis, InvocationExpressionSyntax invocation, int position,
        IReadOnlyList<SourceCompletionBinding> bindings, CancellationToken cancellationToken)
    {
        if (position < invocation.OpenParenthesisToken.Span.End
            || position > invocation.CloseParenthesisToken.Span.Start)
        {
            return null;
        }

        SourceSignatureHelpResult? signature = SourceSignatureHelpResolver.ResolveInvocation(
            analysis, invocation, position, cancellationToken);

        if (signature?.ActiveParameter is not int parameterIndex
            || parameterIndex < 0
            || parameterIndex >= signature.Callable.Function.Parameters.Count)
        {
            return Recognized(null);
        }

        BoundType expected = signature.Callable.Function.Parameters[parameterIndex].Type;
        if (expected is BoundType.Void)
        {
            return Recognized(null);
        }

        int argumentIndex = invocation.ArgumentSeparators.Count(separator => separator.Span.End <= position);
        if (argumentIndex < invocation.Arguments.Count)
        {
            CallArgumentSyntax argument = invocation.Arguments[argumentIndex];
            if (Contains(argument.Expression.Span, position))
            {
                return ResolveExpression(analysis, argument.Expression, expected, position, bindings, cancellationToken,
                    allowIntegerBooleanStarter: expected is BoundType.Bool)
                    ?? (ContainsMissingSyntax(argument.Expression) ? RecognizedResult(expected) : Recognized(null));
            }

            if (Contains(argument.Span, position) || ContainsMissingSyntax(argument.Expression))
            {
                return ContainsMissingSyntax(argument.Expression) ? RecognizedResult(expected) : Recognized(null);
            }
        }
        else if (invocation.Arguments.Count == 0)
        {
            return RecognizedResult(expected);
        }

        return Recognized(null);
    }

    private static BoundType? GetBindingType(SyntaxToken identifier, IReadOnlyList<SourceCompletionBinding> bindings)
    {
        string? name = IdentifierBinder.Bind(identifier);
        return name is null
            ? null
            : bindings.FirstOrDefault(binding => StringComparer.Ordinal.Equals(binding.Name, name))?.Type;
    }

    private static BoundType? GetWritableBindingType(
        SyntaxToken identifier, IReadOnlyList<SourceCompletionBinding> bindings)
    {
        string? name = IdentifierBinder.Bind(identifier);
        return name is null
            ? null
            : bindings.FirstOrDefault(binding =>
                binding.Kind is SourceCompletionBindingKind.Local
                && StringComparer.Ordinal.Equals(binding.Name, name))?.Type;
    }

    private static BoundType? GetDeclaredType(SyntaxToken token)
        => IdentifierBinder.Bind(token) switch
        {
            "int32" => BoundType.Int32,
            "bool" => BoundType.Bool,
            _ => null
        };

    private static bool ContainsMissingSyntax(ExpressionSyntax expression)
        => expression switch
        {
            IntegerLiteralExpressionSyntax integer => integer.LiteralToken.IsMissing,
            ParenthesizedExpressionSyntax grouped =>
                grouped.CloseParenthesisToken.IsMissing || ContainsMissingSyntax(grouped.Expression),
            UnaryExpressionSyntax unary => ContainsMissingSyntax(unary.Operand),
            BinaryExpressionSyntax binary =>
                ContainsMissingSyntax(binary.Left) || ContainsMissingSyntax(binary.Right),
            InvalidComparisonChainSyntax => true,
            InvocationExpressionSyntax invocation =>
                invocation.CloseParenthesisToken.IsMissing
                || invocation.Arguments.Any(argument => ContainsMissingSyntax(argument.Expression)),
            IncrementDecrementExpressionSyntax update => ContainsMissingSyntax(update.Operand),
            _ => false
        };

    private static bool IsTriviaOnly(SourceAnalysisResult analysis, int start, int end)
    {
        foreach (LexToken token in analysis.SyntaxTree.LexerResult.Tokens)
        {
            if (token.Span.Start < start || token.Span.End > end)
            {
                continue;
            }

            if (token.Type is not (LexTokenType.Whitespace
                or LexTokenType.LineTerminator
                or LexTokenType.LineComment
                or LexTokenType.BlockComment))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Contains(SourceSpan span, int position)
        => span.Start <= position && position <= span.End;

    private static SourceCompletionExpressionContext Recognized(BoundType? expectedType, bool allowIntegerBooleanStarter = false)
        => new(true, expectedType, allowIntegerBooleanStarter);

    private static SourceCompletionExpressionContext RecognizedResult(BoundType? expectedType)
        => Recognized(expectedType, expectedType is BoundType.Bool);
}

/// <summary>
/// Represents whether a source position belongs to a supported expression
/// context separately from whether that context has a known expected type.
/// </summary>
/// <param name="IsRecognized">Whether the position is in a supported expression context.</param>
/// <param name="ExpectedType">The required expression result type, or null when unknown.</param>
/// <param name="AllowsIntegerBooleanExpressionStarter">
/// Whether an integer value may begin a comparison that completes a Boolean expression.
/// </param>
internal readonly record struct SourceCompletionExpressionContext(
    bool IsRecognized, BoundType? ExpectedType, bool AllowsIntegerBooleanExpressionStarter = false);

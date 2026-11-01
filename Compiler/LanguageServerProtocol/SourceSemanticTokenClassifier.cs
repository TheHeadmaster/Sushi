
using System.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.LanguageServerProtocol;

/// <summary>
/// Describes a semantic highlighting span independently of LSP wire encoding.
/// </summary>
/// <param name="Span">
/// The source-backed span of one identifier or type token.
/// </param>
/// <param name="TokenType">
/// The LSP classification assigned to the token.
/// </param>
/// <param name="IsDeclaration">
/// Whether the token introduces a declaration rather than a reference.
/// </param>
internal sealed record SourceSemanticToken(SourceSpan Span, SemanticTokenType TokenType, bool IsDeclaration);

/// <summary>
/// Classifies supported Sushi syntax using explicit declaration and expression
/// roles. It does not perform cross-file name resolution or replace binding.
/// </summary>
internal static class SourceSemanticTokenClassifier
{
    /// <summary>
    /// Classifies semantic tokens within one parsed Sushi source file.
    /// </summary>
    /// <param name="source">
    /// The parsed source file and its authoritative source snapshot.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels classification of a potentially large syntax tree.
    /// </param>
    /// <returns>
    /// Nonoverlapping, source-ordered semantic tokens suitable for LSP encoding.
    /// </returns>
    public static IReadOnlyList<SourceSemanticToken> Classify(SourceFileSyntax source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();

        Collector collector = new(source.Snapshot, cancellationToken);
        return collector.Collect(source);
    }

    private sealed class Collector(SourceSnapshot snapshot, CancellationToken cancellationToken)
    {
        private readonly SourceSnapshot snapshot = snapshot;
        private readonly CancellationToken cancellationToken = cancellationToken;
        private readonly Dictionary<int, SourceSemanticToken> tokens = [];
        private readonly HashSet<string> callableNames = [with(StringComparer.Ordinal)];

        public IReadOnlyList<SourceSemanticToken> Collect(SourceFileSyntax source)
        {
            foreach (FunctionDeclarationSyntax function in source.FunctionDeclarations)
            {
                this.callableNames.Add(this.GetName(function.Name));
            }

            foreach (PackageDeclarationSyntax declaration in source.PackageDeclarations)
            {
                foreach (SyntaxToken segment in declaration.Name.Segments)
                {
                    this.Add(segment, SemanticTokenType.Namespace, declaration: true);
                }
            }

            foreach (NamespaceDeclarationSyntax declaration in source.NamespaceDeclarations)
            {
                foreach (SyntaxToken segment in declaration.Name.Segments)
                {
                    this.Add(segment, SemanticTokenType.Namespace, declaration: true);
                }
            }

            foreach (SyntaxNode declaration in source.Declarations)
            {
                this.cancellationToken.ThrowIfCancellationRequested();

                switch (declaration)
                {
                    case FunctionDeclarationSyntax function:
                        this.VisitFunction(function);
                        break;
                    case ActivityDeclarationSyntax activity:
                        this.VisitActivity(activity);
                        break;
                }
            }

            return [.. this.tokens.Values.OrderBy(token => token.Span.Start)];
        }

        private void VisitFunction(FunctionDeclarationSyntax function)
        {
            this.Add(function.Name, SemanticTokenType.Function, declaration: true);
            this.Add(function.ReturnType, SemanticTokenType.Type);

            HashSet<string> parameters = this.VisitParameters(function.Parameters);
            this.VisitBlock(function.Body, [], parameters);
        }

        private void VisitActivity(ActivityDeclarationSyntax activity)
        {
            this.Add(activity.Name, SemanticTokenType.Class, declaration: true);

            foreach (ActivityMethodDeclarationSyntax method in activity.Members.OfType<ActivityMethodDeclarationSyntax>())
            {
                this.cancellationToken.ThrowIfCancellationRequested();
                this.Add(method.Name, SemanticTokenType.Method, declaration: true);
                this.Add(method.ReturnType, SemanticTokenType.Type);

                HashSet<string> parameters = this.VisitParameters(method.Parameters);
                this.VisitBlock(method.Body, [], parameters);
            }
        }

        private HashSet<string> VisitParameters(IReadOnlyList<ParameterSyntax> declarations)
        {
            HashSet<string> parameters = new(StringComparer.Ordinal);

            foreach (ParameterSyntax parameter in declarations)
            {
                this.Add(parameter.TypeToken, SemanticTokenType.Type);
                this.Add(parameter.NameToken, SemanticTokenType.Parameter, declaration: true);
                parameters.Add(this.GetName(parameter.NameToken));
            }

            return parameters;
        }

        private void VisitBlock(BlockSyntax block, HashSet<string> inheritedLocals, HashSet<string> parameters)
        {
            HashSet<string> locals = new(inheritedLocals, StringComparer.Ordinal);

            foreach (StatementSyntax statement in block.Statements)
            {
                this.cancellationToken.ThrowIfCancellationRequested();
                this.VisitStatement(statement, locals, parameters);
            }
        }

        private void VisitStatement(StatementSyntax statement, HashSet<string> locals, HashSet<string> parameters)
        {
            switch (statement)
            {
                case LocalDeclarationStatementSyntax declaration:
                    this.Add(declaration.TypeToken, SemanticTokenType.Type);
                    this.VisitExpression(declaration.Initializer, locals, parameters);
                    this.Add(declaration.NameToken, SemanticTokenType.Variable, declaration: true);
                    locals.Add(this.GetName(declaration.NameToken));
                    break;

                case LocalAssignmentStatementSyntax assignment:
                    this.AddLocalReference(assignment.NameToken, locals, parameters);
                    this.VisitExpression(assignment.Value, locals, parameters);
                    break;

                case ExpressionStatementSyntax expression:
                    this.VisitExpression(expression.Expression, locals, parameters);
                    break;

                case ReturnStatementSyntax returning when returning.Expression is not null:
                    this.VisitExpression(returning.Expression, locals, parameters);
                    break;

                case IfStatementSyntax conditional:
                    this.VisitExpression(conditional.Condition, locals, parameters);
                    this.VisitBlock(conditional.ThenBlock, locals, parameters);

                    if (conditional.ElseBlock is not null)
                    {
                        this.VisitBlock(conditional.ElseBlock, locals, parameters);
                    }

                    if (conditional.ElseIf is not null)
                    {
                        this.VisitStatement(conditional.ElseIf, locals, parameters);
                    }

                    break;

                case WhileStatementSyntax loop:
                    this.VisitExpression(loop.Condition, locals, parameters);
                    this.VisitBlock(loop.Body, locals, parameters);
                    break;

                case DoWhileStatementSyntax loop:
                    this.VisitBlock(loop.Body, locals, parameters);
                    this.VisitExpression(loop.Condition, locals, parameters);
                    break;

                case ForStatementSyntax loop:
                    this.VisitFor(loop, locals, parameters);
                    break;
            }
        }

        private void VisitFor(ForStatementSyntax loop, HashSet<string> inheritedLocals, HashSet<string> parameters)
        {
            HashSet<string> locals = new(inheritedLocals, StringComparer.Ordinal);

            foreach (ForHeaderOperationSyntax operation in loop.Initialization)
            {
                this.VisitHeaderOperation(operation, locals, parameters);
            }

            if (loop.Condition is not null)
            {
                this.VisitExpression(loop.Condition, locals, parameters);
            }

            foreach (ForHeaderOperationSyntax operation in loop.Iteration)
            {
                this.VisitHeaderOperation(operation, locals, parameters);
            }

            this.VisitBlock(loop.Body, locals, parameters);
        }

        private void VisitHeaderOperation(ForHeaderOperationSyntax operation, HashSet<string> locals, HashSet<string> parameters)
        {
            if (operation.TypeToken is SyntaxToken declaredType)
            {
                this.Add(declaredType, SemanticTokenType.Type);

                if (operation.Value is not null)
                {
                    this.VisitExpression(operation.Value, locals, parameters);
                }

                this.Add(operation.NameToken, SemanticTokenType.Variable, declaration: true);
                locals.Add(this.GetName(operation.NameToken));
                return;
            }

            this.AddLocalReference(operation.NameToken, locals, parameters);

            if (operation.Value is not null)
            {
                this.VisitExpression(operation.Value, locals, parameters);
            }
        }

        private void VisitExpression(ExpressionSyntax expression, HashSet<string> locals, HashSet<string> parameters)
        {
            this.cancellationToken.ThrowIfCancellationRequested();

            switch (expression)
            {
                case IdentifierExpressionSyntax identifier:
                    this.AddLocalReference(identifier.IdentifierToken, locals, parameters);
                    break;

                case InvocationExpressionSyntax invocation:
                    this.VisitInvocation(invocation, locals, parameters);
                    break;

                case ParenthesizedExpressionSyntax grouped:
                    this.VisitExpression(grouped.Expression, locals, parameters);
                    break;

                case UnaryExpressionSyntax unary:
                    this.VisitExpression(unary.Operand, locals, parameters);
                    break;

                case IncrementDecrementExpressionSyntax update:
                    this.VisitExpression(update.Operand, locals, parameters);
                    break;

                case BinaryExpressionSyntax binary:
                    this.VisitExpression(binary.Left, locals, parameters);
                    this.VisitExpression(binary.Right, locals, parameters);
                    break;

                case InvalidComparisonChainSyntax chain:
                    this.VisitExpression(chain.Left, locals, parameters);
                    this.VisitExpression(chain.Right, locals, parameters);
                    break;
            }
        }

        private void VisitInvocation(InvocationExpressionSyntax invocation, HashSet<string> locals, HashSet<string> parameters)
        {
            bool recognizedCallable = invocation.Target is IdentifierExpressionSyntax target
                && this.callableNames.Contains(this.GetName(target.IdentifierToken));

            if (recognizedCallable && invocation.Target is IdentifierExpressionSyntax identifier)
            {
                this.Add(identifier.IdentifierToken, SemanticTokenType.Function);
            }
            else if (invocation.Target is not IdentifierExpressionSyntax)
            {
                this.VisitExpression(invocation.Target, locals, parameters);
            }

            foreach (CallArgumentSyntax argument in invocation.Arguments)
            {
                if (recognizedCallable && argument.Name is SyntaxToken name)
                {
                    this.Add(name, SemanticTokenType.Parameter);
                }

                this.VisitExpression(argument.Expression, locals, parameters);
            }
        }

        private void AddLocalReference(SyntaxToken token, HashSet<string> locals, HashSet<string> parameters)
        {
            string name = this.GetName(token);

            if (locals.Contains(name))
            {
                this.Add(token, SemanticTokenType.Variable);
            }
            else if (parameters.Contains(name))
            {
                this.Add(token, SemanticTokenType.Parameter);
            }
        }

        private string GetName(SyntaxToken token)
        {
            if (token.IsMissing || token.Span.Length == 0)
            {
                return string.Empty;
            }

            ReadOnlySpan<byte> bytes = this.snapshot.Bytes.Span[token.Span.Start..token.Span.End];

            if (bytes[0] == (byte)'@')
            {
                bytes = bytes[1..];
            }

            return Encoding.UTF8.GetString(bytes);
        }

        private void Add(SyntaxToken token, SemanticTokenType type, bool declaration = false)
        {
            SourceSpan span = token.Span;

            if (token.IsMissing || span.Length == 0 || !ReferenceEquals(span.Snapshot, this.snapshot))
            {
                return;
            }

            this.tokens.TryAdd(span.Start, new SourceSemanticToken(span, type, declaration));
        }
    }
}
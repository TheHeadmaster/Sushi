using Sushi.Diagnostics;
using Sushi.Lexing;
using Sushi.Lexing.Tokenization;
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.Documentation;

/// <summary>
/// Groups source-backed documentation trivia and attaches each block to an immediately following supported declaration.
/// </summary>
public static class DocumentationProcessor
{
    /// <summary>
    /// Processes documentation comments retained by the lexer and reports detached blocks.
    /// </summary>
    /// <param name="lexerResult">
    /// The complete lexical result for the source file.
    /// </param>
    /// <param name="sourceFile">
    /// The parsed declarations that may receive documentation.
    /// </param>
    /// <param name="diagnosticReporter">
    /// Receives one warning for each detached documentation block.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels processing between lexical elements and blocks.
    /// </param>
    /// <returns>
    /// Immutable documentation blocks in source order, associated with the same snapshot as the syntax tree.
    /// </returns>
    public static IReadOnlyList<DocumentationBlock> Process(
        LexerResult lexerResult,
        SourceFileSyntax sourceFile,
        IDiagnosticReporter diagnosticReporter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lexerResult);
        ArgumentNullException.ThrowIfNull(sourceFile);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);
        cancellationToken.ThrowIfCancellationRequested();

        if (!ReferenceEquals(lexerResult.Snapshot, sourceFile.Snapshot))
        {
            throw new ArgumentException("Documentation processing requires syntax and lexical results from the same source snapshot.", nameof(sourceFile));
        }

        List<SourceSpan[]> lineGroups = GroupDocumentationLines(lexerResult, cancellationToken);
        SyntaxNode[] declarations = GetEligibleDeclarations(sourceFile, cancellationToken);
        List<DocumentationBlock> blocks = new(lineGroups.Count);
        int declarationIndex = 0;

        foreach (SourceSpan[] lines in lineGroups)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SourceSpan span = new(sourceFile.Snapshot, lines[0].Start, lines[^1].End);
            SyntaxNode? declaration = FindAttachedDeclaration(sourceFile.Snapshot, span, declarations, ref declarationIndex, cancellationToken);
            DocumentationContent content = DocumentationContentParser.Parse(sourceFile.Snapshot, lines, diagnosticReporter, cancellationToken);
            DocumentationBlock block = new(span, lines, declaration, content);
            blocks.Add(block);

            if (declaration is null)
            {
                diagnosticReporter.GenerateWarning(WarningType.DetachedDocumentationBlock, span);
            }
        }

        return Array.AsReadOnly(blocks.ToArray());
    }

    private static List<SourceSpan[]> GroupDocumentationLines(LexerResult lexerResult, CancellationToken cancellationToken)
    {
        List<SourceSpan[]> groups = [];
        List<SourceSpan> current = [];
        ReadOnlySpan<byte> source = lexerResult.Snapshot.Bytes.Span;

        foreach (LexToken token in lexerResult.Tokens)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (token.Type is not LexTokenType.DocumentationLineComment)
            {
                continue;
            }

            if (current.Count > 0 && !HasSingleLineBreakAndIndentation(source, current[^1].End, token.Span.Start))
            {
                groups.Add([.. current]);
                current.Clear();
            }

            current.Add(token.Span);
        }

        if (current.Count > 0)
        {
            groups.Add([.. current]);
        }

        return groups;
    }

    private static SyntaxNode[] GetEligibleDeclarations(SourceFileSyntax sourceFile, CancellationToken cancellationToken)
    {
        List<SyntaxNode> declarations = [.. sourceFile.Declarations];

        foreach (ActivityDeclarationSyntax activity in sourceFile.ActivityDeclarations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (ActivityMemberSyntax member in activity.Members)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (member is ActivityMethodDeclarationSyntax method)
                {
                    declarations.Add(method);
                }
            }
        }

        declarations.Sort(static (left, right) => left.Span.Start.CompareTo(right.Span.Start));
        return [.. declarations];
    }

    private static SyntaxNode? FindAttachedDeclaration(
        SourceSnapshot snapshot,
        SourceSpan blockSpan,
        IReadOnlyList<SyntaxNode> declarations,
        ref int declarationIndex,
        CancellationToken cancellationToken)
    {
        ReadOnlySpan<byte> source = snapshot.Bytes.Span;

        while (declarationIndex < declarations.Count)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SyntaxNode declaration = declarations[declarationIndex];

            if (declaration.Span.Start < blockSpan.End)
            {
                declarationIndex++;
                continue;
            }

            if (!HasSingleLineBreakAndIndentation(source, blockSpan.End, declaration.Span.Start))
            {
                return null;
            }

            declarationIndex++;
            return declaration;
        }

        return null;
    }

    private static bool HasSingleLineBreakAndIndentation(ReadOnlySpan<byte> source, int start, int end)
    {
        if (start < 0 || end < start || end > source.Length)
        {
            return false;
        }

        int lineBreakCount = 0;

        for (int position = start; position < end; position++)
        {
            byte current = source[position];

            if (current is (byte)' ' or (byte)'\t')
            {
                continue;
            }

            if (current == (byte)'\r')
            {
                if (position + 1 < end && source[position + 1] == (byte)'\n')
                {
                    position++;
                }

                lineBreakCount++;
            }
            else if (current == (byte)'\n')
            {
                lineBreakCount++;
            }
            else
            {
                return false;
            }

            if (lineBreakCount > 1)
            {
                return false;
            }
        }

        return lineBreakCount == 1;
    }
}

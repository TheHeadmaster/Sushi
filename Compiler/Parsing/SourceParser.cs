
using Sushi.Diagnostics;
using Sushi.Lexing;
using Sushi.Lexing.Tokenization;
using Sushi.Parsing.Syntax;
using Sushi.Source;

namespace Sushi.Parsing;

/// <summary>
/// Coordinates source-file parsing and exposes entry points for parsing individual declarations.
/// </summary>
public sealed class SourceParser
{
    /// <summary>
    /// Parses supported file-level declarations in source order, retaining any unparsed remainder.
    /// </summary>
    /// <param name="lexerResult">
    /// The complete lexical result, including trivia and recovery elements.
    /// </param>
    /// <param name="diagnosticReporter">
    /// The reporter receiving accumulated parser diagnostics.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel parsing.
    /// </param>
    /// <returns>
    /// A concrete source-file syntax tree containing recognized declarations and any unparsed content.
    /// </returns>
    public static ParserResult ParseSourceFile(LexerResult lexerResult, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lexerResult);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);
        cancellationToken.ThrowIfCancellationRequested();

        IDiagnosticReporter localDiagnostics = new DiagnosticReporter();
        ParserCursor cursor = new(lexerResult, cancellationToken);
        DeclarationParser declarations = new(cursor, cancellationToken);

        List<PackageDeclarationSyntax> packageDeclarations = [];

        while (declarations.IsPackageDeclarationAhead())
        {
            cancellationToken.ThrowIfCancellationRequested();
            packageDeclarations.Add(declarations.ParsePackageDeclaration(localDiagnostics));
        }

        List<NamespaceDeclarationSyntax> namespaceDeclarations = [];

        while (declarations.IsNamespaceDeclarationAhead())
        {
            cancellationToken.ThrowIfCancellationRequested();
            namespaceDeclarations.Add(declarations.ParseNamespaceDeclaration(localDiagnostics));
        }

        List<FunctionDeclarationSyntax> functionDeclarations = [];
        List<ActivityDeclarationSyntax> activityDeclarations = [];

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (declarations.IsActivityDeclarationAhead())
            {
                activityDeclarations.Add(declarations.ParseActivityDeclaration(localDiagnostics));
            }
            else if (declarations.IsFunctionDeclarationAhead())
            {
                functionDeclarations.Add(declarations.ParseFunctionDeclaration(localDiagnostics));
            }
            else
            {
                break;
            }
        }

        SourceSpan? unparsedContentSpan = cursor.TryPeek(out LexToken next)
            ? new SourceSpan(cursor.Snapshot, next.Span.Start, cursor.Snapshot.SourceLength)
            : null;

        SourceFileSyntax sourceFile = new(
            cursor.Snapshot,
            packageDeclarations,
            namespaceDeclarations,
            functionDeclarations,
            activityDeclarations,
            unparsedContentSpan);

        diagnosticReporter.CommitReporter(localDiagnostics);
        return new ParserResult(new ConcreteSyntaxTree(lexerResult, sourceFile));
    }

    /// <summary>
    /// Parses a function declaration beginning at the first significant lexical element.
    /// </summary>
    /// <param name="lexerResult">
    /// The complete lexical result, including trivia and recovery elements.
    /// </param>
    /// <param name="diagnosticReporter">
    /// The diagnostic reporter used to accumulate diagnostics.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel parsing.
    /// </param>
    /// <returns>
    /// The parsed function declaration.
    /// </returns>
    public static ParserResult ParseFunctionDeclaration(LexerResult lexerResult, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lexerResult);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);
        cancellationToken.ThrowIfCancellationRequested();

        IDiagnosticReporter localDiagnostics = new DiagnosticReporter();
        DeclarationParser declarations = new(new ParserCursor(lexerResult, cancellationToken), cancellationToken);
        FunctionDeclarationSyntax declaration = declarations.ParseFunctionDeclaration(localDiagnostics);

        diagnosticReporter.CommitReporter(localDiagnostics);
        return new ParserResult(new ConcreteSyntaxTree(lexerResult, declaration));
    }

    /// <summary>
    /// Parses an activity declaration beginning at the first significant lexical element.
    /// </summary>
    /// <param name="lexerResult">
    /// The complete lexical result, including trivia and recovery elements.
    /// </param>
    /// <param name="diagnosticReporter">
    /// The reporter receiving syntax and recovery diagnostics.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel parsing.
    /// </param>
    /// <returns>
    /// The parsed activity declaration, including recovered syntax.
    /// </returns>
    public static ParserResult ParseActivityDeclaration(LexerResult lexerResult, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lexerResult);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);
        cancellationToken.ThrowIfCancellationRequested();

        IDiagnosticReporter localDiagnostics = new DiagnosticReporter();
        DeclarationParser declarations = new(new ParserCursor(lexerResult, cancellationToken), cancellationToken);
        ActivityDeclarationSyntax declaration = declarations.ParseActivityDeclaration(localDiagnostics);

        diagnosticReporter.CommitReporter(localDiagnostics);
        return new ParserResult(new ConcreteSyntaxTree(lexerResult, declaration));
    }

    /// <summary>
    /// Parses a package declaration beginning at the first significant lexical element.
    /// </summary>
    /// <param name="lexerResult">
    /// The complete lexical result, including trivia and recovery elements.
    /// </param>
    /// <param name="diagnosticReporter">
    /// The diagnostic reporter used to accumulate diagnostics.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel parsing.
    /// </param>
    /// <returns>
    /// The parsed package declaration.
    /// </returns>
    public static ParserResult ParsePackageDeclaration(LexerResult lexerResult, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lexerResult);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);
        cancellationToken.ThrowIfCancellationRequested();

        IDiagnosticReporter localDiagnostics = new DiagnosticReporter();
        DeclarationParser declarations = new(new ParserCursor(lexerResult, cancellationToken), cancellationToken);
        PackageDeclarationSyntax declaration = declarations.ParsePackageDeclaration(localDiagnostics);

        diagnosticReporter.CommitReporter(localDiagnostics);
        return new ParserResult(new ConcreteSyntaxTree(lexerResult, declaration));
    }

    /// <summary>
    /// Parses a namespace declaration beginning at the first significant lexical element.
    /// </summary>
    /// <param name="lexerResult">
    /// The complete lexical result, including trivia and recovery elements.
    /// </param>
    /// <param name="diagnosticReporter">
    /// The diagnostic reporter used to accumulate diagnostics.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel parsing.
    /// </param>
    /// <returns>
    /// The parsed namespace declaration.
    /// </returns>
    public static ParserResult ParseNamespaceDeclaration(LexerResult lexerResult, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lexerResult);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);
        cancellationToken.ThrowIfCancellationRequested();

        IDiagnosticReporter localDiagnostics = new DiagnosticReporter();
        DeclarationParser declarations = new(new ParserCursor(lexerResult, cancellationToken), cancellationToken);
        NamespaceDeclarationSyntax declaration = declarations.ParseNamespaceDeclaration(localDiagnostics);

        diagnosticReporter.CommitReporter(localDiagnostics);
        return new ParserResult(new ConcreteSyntaxTree(lexerResult, declaration));
    }
}

using Sushi.Diagnostics;
using Sushi.Parsing.Syntax;
using Sushi.Semantics;
using Sushi.Source;

namespace Sushi.Documentation;

/// <summary>
/// Validates parsed documentation directives against declarations in the source file.
/// </summary>
internal static class DocumentationValidator
{
    public static void Validate(SourceFileSyntax sourceFile, IReadOnlyList<DocumentationBlock> blocks, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceFile);
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);

        foreach (FunctionDeclarationSyntax function in sourceFile.FunctionDeclarations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateDeclaration(function, function.AccessModifier, function.Name, function.Parameters, function.ReturnType, IsPublic(function.AccessModifier), blocks, diagnosticReporter, cancellationToken);
        }

        foreach (ActivityDeclarationSyntax activity in sourceFile.ActivityDeclarations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool activityIsPublic = IsPublic(activity.AccessModifier);
            DocumentationBlock? activityBlock = FindBlock(activity, blocks);
            if (activityBlock is null && activityIsPublic && HasUsableDeclarationName(activity.Name))
            {
                diagnosticReporter.GenerateWarning(WarningType.MissingPublicDocumentation, activity.Name.Span);
            }

            foreach (ActivityMemberSyntax member in activity.Members)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (member is not ActivityMethodDeclarationSyntax method)
                {
                    continue;
                }

                bool methodIsPublic = activityIsPublic && IsPublic(method.AccessModifier);
                ValidateDeclaration(method, method.AccessModifier, method.Name, method.Parameters, method.ReturnType, methodIsPublic, blocks, diagnosticReporter, cancellationToken);
            }
        }
    }

    private static void ValidateDeclaration(
        SyntaxNode declaration,
        SyntaxToken accessModifier,
        SyntaxToken name,
        IReadOnlyList<ParameterSyntax> parameters,
        SyntaxToken returnType,
        bool isPublic,
        IReadOnlyList<DocumentationBlock> blocks,
        IDiagnosticReporter diagnosticReporter,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!HasUsableDeclarationName(name) || !accessModifier.IsSourceBacked)
        {
            return;
        }

        DocumentationBlock? block = FindBlock(declaration, blocks);
        if (block is null)
        {
            if (isPublic)
            {
                diagnosticReporter.GenerateWarning(WarningType.MissingPublicDocumentation, name.Span);
            }

            return;
        }

        bool validParameters = TryGetDeclaredParameters(parameters, cancellationToken, out Dictionary<string, SyntaxToken> declaredParameters);
        HashSet<string> documentedParameters = new(StringComparer.Ordinal);
        int returnDirectiveCount = 0;
        bool isVoid = returnType.IsSourceBacked && returnType.Type is SyntaxType.VoidKeyword;
        bool hasKnownReturnType = isVoid || (returnType.IsSourceBacked && IdentifierBinder.Bind(returnType) is "int32" or "bool");

        foreach (DocumentationDirective directive in block.Content.Directives)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (directive.Kind)
            {
                case DocumentationDirectiveKind.Parameter when validParameters:
                    ValidateParameterDirective(directive, declaredParameters, documentedParameters, diagnosticReporter);
                    break;

                case DocumentationDirectiveKind.Return:
                    returnDirectiveCount++;
                    if (returnDirectiveCount > 1)
                    {
                        diagnosticReporter.GenerateWarning(WarningType.DuplicateReturnDocumentation, directive.NameSpan);
                    }

                    if (isVoid)
                    {
                        diagnosticReporter.GenerateWarning(WarningType.ReturnDocumentationOnVoid, directive.NameSpan);
                    }

                    break;
            }
        }

        if (isPublic && validParameters)
        {
            foreach ((string parameterName, SyntaxToken parameterToken) in declaredParameters)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!documentedParameters.Contains(parameterName))
                {
                    diagnosticReporter.GenerateWarning(WarningType.MissingPublicParameterDocumentation, parameterToken.Span);
                }
            }
        }

        if (hasKnownReturnType && !isVoid && returnDirectiveCount == 0)
        {
            diagnosticReporter.GenerateWarning(WarningType.MissingReturnDocumentation, returnType.Span);
        }
    }

    private static void ValidateParameterDirective(
        DocumentationDirective directive,
        IReadOnlyDictionary<string, SyntaxToken> declaredParameters,
        HashSet<string> documentedParameters,
        IDiagnosticReporter diagnosticReporter)
    {
        string parameterName = directive.ParameterName ?? string.Empty;
        if (!declaredParameters.ContainsKey(parameterName))
        {
            diagnosticReporter.GenerateWarning(WarningType.UnknownDocumentationParameter, directive.ParameterNameSpan ?? directive.NameSpan);
            return;
        }

        if (!documentedParameters.Add(parameterName))
        {
            diagnosticReporter.GenerateWarning(WarningType.DuplicateParameterDocumentation, directive.NameSpan);
        }
    }

    private static bool TryGetDeclaredParameters(IReadOnlyList<ParameterSyntax> parameters, CancellationToken cancellationToken, out Dictionary<string, SyntaxToken> declaredParameters)
    {
        declaredParameters = new Dictionary<string, SyntaxToken>(StringComparer.Ordinal);
        foreach (ParameterSyntax parameter in parameters)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!parameter.TypeToken.IsSourceBacked
                || IdentifierBinder.Bind(parameter.TypeToken) is not ("int32" or "bool")
                || !parameter.NameToken.IsSourceBacked
                || IdentifierBinder.Bind(parameter.NameToken) is not string name
                || !declaredParameters.TryAdd(name, parameter.NameToken))
            {
                declaredParameters.Clear();
                return false;
            }
        }

        return true;
    }

    private static DocumentationBlock? FindBlock(SyntaxNode declaration, IReadOnlyList<DocumentationBlock> blocks)
        => blocks.FirstOrDefault(block => ReferenceEquals(block.Declaration, declaration));

    private static bool HasUsableDeclarationName(SyntaxToken name)
        => name.IsSourceBacked && IdentifierBinder.Bind(name) is not null;

    private static bool IsPublic(SyntaxToken accessModifier)
        => accessModifier.IsSourceBacked
            && accessModifier.Span.Snapshot.Bytes.Span[accessModifier.Span.Start..accessModifier.Span.End].SequenceEqual("public"u8);
}

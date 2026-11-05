using Sushi.Parsing.Syntax;
using Sushi.Semantics;

namespace Sushi.Diagnostics;

/// <summary>
/// Reports recognized placeholder spellings on source-backed callable parameters.
/// </summary>
internal static class ParameterNamingValidator
{
    /// <summary>
    /// Validates parameters on supported package functions and activity lifecycle methods.
    /// </summary>
    /// <param name="sourceFile">
    /// Parsed declarations from the authoritative source snapshot.
    /// </param>
    /// <param name="diagnosticReporter">
    /// Receives placeholder-name warnings.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels declaration validation.
    /// </param>
    public static void Validate(SourceFileSyntax sourceFile, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceFile);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);
        cancellationToken.ThrowIfCancellationRequested();

        foreach (SyntaxNode declaration in sourceFile.Declarations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (declaration)
            {
                case FunctionDeclarationSyntax function:
                    ValidateParameters(function.Parameters, diagnosticReporter, cancellationToken);
                    break;
                case ActivityDeclarationSyntax activity:
                    foreach (ActivityMemberSyntax member in activity.Members)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (member is ActivityMethodDeclarationSyntax method)
                        {
                            ValidateParameters(method.Parameters, diagnosticReporter, cancellationToken);
                        }
                    }

                    break;
            }
        }
    }

    private static void ValidateParameters(
        IReadOnlyList<ParameterSyntax> parameters,
        IDiagnosticReporter diagnosticReporter,
        CancellationToken cancellationToken)
    {
        foreach (ParameterSyntax parameter in parameters)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? semanticName = IdentifierBinder.Bind(parameter.NameToken);
            if (semanticName is not null && IsPlaceholderName(semanticName))
            {
                diagnosticReporter.GenerateWarning(WarningType.PlaceholderParameterName, parameter.NameToken.Span);
            }
        }
    }

    private static bool IsPlaceholderName(string name)
        => name is "arg" or "param" or "foo" or "temp"
            || HasNumberedSuffix(name, "arg")
            || HasNumberedSuffix(name, "param");

    private static bool HasNumberedSuffix(string name, string prefix)
    {
        if (!name.StartsWith(prefix, StringComparison.Ordinal) || name.Length == prefix.Length)
        {
            return false;
        }

        for (int index = prefix.Length; index < name.Length; index++)
        {
            if (name[index] is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }
}
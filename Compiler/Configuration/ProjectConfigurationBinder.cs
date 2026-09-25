using Sushi.Analysis;
using Sushi.Diagnostics;
using Sushi.Source;
using Tomlyn.Syntax;
using SushiSourceSpan = Sushi.Source.SourceSpan;
using TomlSourceSpan = Tomlyn.Syntax.SourceSpan;

namespace Sushi.Configuration;

public sealed class ProjectConfigurationBinder
{
    // Placeholders
    private const string MissingRequiredKeyDiagnosticCode = "SUSE1001";
    private const string InvalidValueTypeDiagnosticCode = "SUSE1002";
    private const string EmptyProjectNameDiagnosticCode = "SUSE1003";

    public ProjectConfigurationBindResult Bind(SourceSnapshot snapshot, DocumentSyntax syntax, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(syntax);

        cancellationToken.ThrowIfCancellationRequested();

        List<SushiDiagnostic> diagnostics = [];

        string? name = null;
        string? assembly = null;
        string? languageVersion = null;

        bool hasName = false;
        bool hasAssembly = false;
        bool hasLanguageVersion = false;

        foreach (KeyValueSyntax keyValue in syntax.KeyValues)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? keyName = GetSimpleKeyName(keyValue.Key);

            switch (keyName)
            {
                case "name":
                    hasName = true;

                    name = BindStringValue(snapshot, keyValue, "name", diagnostics);

                    if (name is { Length: 0 })
                    {
                        diagnostics.Add(CreateError(EmptyProjectNameDiagnosticCode, "Project name cannot be empty.", snapshot, keyValue.Value!.Span));
                    }

                    break;

                case "assembly":
                    hasAssembly = true;

                    assembly = BindStringValue(snapshot, keyValue, "assembly", diagnostics);

                    break;

                case "language-version":
                    hasLanguageVersion = true;

                    languageVersion = BindStringValue(snapshot, keyValue, "language-version", diagnostics);

                    break;
            }
        }

        if (!hasName)
        {
            diagnostics.Add(CreateMissingKeyDiagnostic(snapshot, "name"));
        }

        if (!hasAssembly)
        {
            diagnostics.Add(CreateMissingKeyDiagnostic(snapshot, "assembly"));
        }

        if (!hasLanguageVersion)
        {
            diagnostics.Add(CreateMissingKeyDiagnostic(snapshot, "language-version"));
        }

        bool hasErrors = diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

        ProjectDefinition? project =
            !hasErrors
            && name is not null
            && assembly is not null
            && languageVersion is not null
                ? new ProjectDefinition(name, assembly, languageVersion)
                : null;

        return new ProjectConfigurationBindResult(project, diagnostics);
    }

    private static string? GetSimpleKeyName(KeySyntax? key)
    {
        if (key is null || key.DotKeys.ChildrenCount != 0)
        {
            return null;
        }

        return key.Key switch
        {
            BareKeySyntax bareKey => bareKey.Key?.Text,
            StringValueSyntax stringKey => stringKey.Value,
            _ => null
        };
    }

    private static string? BindStringValue(SourceSnapshot snapshot, KeyValueSyntax keyValue, string keyName, List<SushiDiagnostic> diagnostics)
    {
        if (keyValue.Value is null)
        {
            // Tomlyn already owns the syntax diagnostic for a
            // syntactically missing value. Do not duplicate it.
            return null;
        }

        if (keyValue.Value is StringValueSyntax stringValue)
        {
            return stringValue.Value;
        }

        diagnostics.Add(CreateError(InvalidValueTypeDiagnosticCode, $"Project configuration key \"{keyName}\" must be a string.", snapshot, keyValue.Value.Span));

        return null;
    }

    private static SushiDiagnostic CreateMissingKeyDiagnostic(SourceSnapshot snapshot, string keyName)
    {
        int eof = snapshot.Bytes.Length;

        return new SushiDiagnostic(
            MissingRequiredKeyDiagnosticCode,
            $"Required project configuration key \"{keyName}\" is missing.",
            DiagnosticSeverity.Error,
            new SushiSourceSpan(snapshot, eof, eof)
        );
    }

    private static SushiDiagnostic CreateError(string code, string message, SourceSnapshot snapshot, TomlSourceSpan span)
    {
        return new SushiDiagnostic(
            code,
            message,
            DiagnosticSeverity.Error,
            span.ToSushiSpan(snapshot)
        );
    }
}
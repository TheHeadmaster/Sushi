using Sushi.Analysis;
using Sushi.Configuration.Toml;
using Sushi.Diagnostics;
using Sushi.Source;

namespace Sushi.Configuration;

public sealed class ProjectConfigurationBinder
{
    // Placeholders
    private const string MissingRequiredKeyDiagnosticCode = "SUSE1001";
    private const string InvalidValueTypeDiagnosticCode = "SUSE1002";
    private const string EmptyProjectNameDiagnosticCode = "SUSE1003";

    public ProjectConfigurationBindResult Bind(SourceSnapshot snapshot, TomlConfigurationTable document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(document);

        cancellationToken.ThrowIfCancellationRequested();

        List<SushiDiagnostic> diagnostics = [];

        string? name = BindRequiredString(snapshot, document, "name", "name", diagnostics);
        string? assembly = BindRequiredString(snapshot, document, "assembly", "assembly", diagnostics);

        string? languageVersion = BindRequiredString(snapshot, document, "language-version", "language-version", diagnostics);

        if (name is { Length: 0 } && document.TryGetProperty("name", out TomlConfigurationProperty nameProperty))
        {
            diagnostics.Add(CreateError(EmptyProjectNameDiagnosticCode, "Project name cannot be empty.", nameProperty.Value.Span));
        }

        ProjectSourceDefinition source = BindSource(document, diagnostics, cancellationToken);

        bool hasErrors = diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

        ProjectDefinition? project =
            !hasErrors
            && name is not null
            && assembly is not null
            && languageVersion is not null
                ? new ProjectDefinition(name, assembly, languageVersion, source)
                : null;

        return new ProjectConfigurationBindResult(project, diagnostics);
    }

    private static string? BindRequiredString(SourceSnapshot snapshot, TomlConfigurationTable table, string propertyName, string diagnosticName, List<SushiDiagnostic> diagnostics)
    {
        if (!table.TryGetProperty(propertyName, out TomlConfigurationProperty property))
        {
            diagnostics.Add(
                CreateMissingKeyDiagnostic(snapshot, diagnosticName));

            return null;
        }

        return BindStringValue(property, diagnosticName, diagnostics);
    }

    private static string? BindStringValue(TomlConfigurationProperty property, string diagnosticName, List<SushiDiagnostic> diagnostics)
    {
        if (property.Value is TomlConfigurationInvalid)
        {
            return null;
        }

        if (property.Value is TomlConfigurationString stringValue)
        {
            return stringValue.Value;
        }

        diagnostics.Add(CreateError(InvalidValueTypeDiagnosticCode, $"Project configuration key \"{diagnosticName}\" must be a string.", property.Value.Span));

        return null;
    }

    private static ProjectSourceDefinition BindSource(TomlConfigurationTable document, List<SushiDiagnostic> diagnostics, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!document.TryGetProperty("source", out TomlConfigurationProperty sourceProperty))
        {
            return new ProjectSourceDefinition(DefaultNamespace: null, Exclude: []);
        }

        if (sourceProperty.Value is TomlConfigurationInvalid)
        {
            return new ProjectSourceDefinition(
                DefaultNamespace: null,
                Exclude: []);
        }

        if (sourceProperty.Value is not TomlConfigurationTable sourceTable)
        {
            diagnostics.Add(CreateError(InvalidValueTypeDiagnosticCode, "Project configuration key \"source\" must be a table.", sourceProperty.Value.Span));

            return new ProjectSourceDefinition(DefaultNamespace: null, Exclude: []);
        }

        string? defaultNamespace = null;
        List<string> exclude = [];

        if (sourceTable.TryGetProperty("default-namespace", out TomlConfigurationProperty defaultNamespaceProperty))
        {
            defaultNamespace = BindStringValue(defaultNamespaceProperty, "source.default-namespace", diagnostics);
        }

        if (sourceTable.TryGetProperty("exclude", out TomlConfigurationProperty excludeProperty))
        {
            exclude.AddRange(BindStringArray(excludeProperty, "source.exclude", diagnostics, cancellationToken));
        }

        return new ProjectSourceDefinition(defaultNamespace, exclude);
    }

    private static IReadOnlyList<string> BindStringArray(TomlConfigurationProperty property, string diagnosticName, List<SushiDiagnostic> diagnostics, CancellationToken cancellationToken)
    {
        if (property.Value is TomlConfigurationInvalid)
        {
            return [];
        }

        if (property.Value is not TomlConfigurationArray array)
        {
            diagnostics.Add(CreateError(InvalidValueTypeDiagnosticCode, $"Project configuration key \"{diagnosticName}\" must be an array of strings.", property.Value.Span));

            return [];
        }

        List<string> values = [];

        foreach (TomlConfigurationValue item in array.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (item is TomlConfigurationString stringValue)
            {
                values.Add(stringValue.Value);

                continue;
            }

            diagnostics.Add(CreateError(InvalidValueTypeDiagnosticCode, $"Project configuration key \"{diagnosticName}\" must contain only strings.", item.Span));
        }

        return values;
    }

    private static SushiDiagnostic CreateMissingKeyDiagnostic(SourceSnapshot snapshot, string keyName)
    {
        int eof = snapshot.Bytes.Length;

        return new SushiDiagnostic(MissingRequiredKeyDiagnosticCode, $"Required project configuration key \"{keyName}\" is missing.", DiagnosticSeverity.Error, new SourceSpan(snapshot, eof, eof));
    }

    private static SushiDiagnostic CreateError(string code, string message, SourceSpan span) => new(code, message, DiagnosticSeverity.Error, span);
}
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

    private const string MissingBuildTargetsDiagnosticCode = "SUSE1004";
    private const string UnknownDefaultBuildTargetDiagnosticCode = "SUSE1005";

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

        ProjectBuildDefinition build = BindBuild(snapshot, document, diagnostics, cancellationToken);

        bool hasErrors = diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

        ProjectDefinition? project =
            !hasErrors
            && name is not null
            && assembly is not null
            && languageVersion is not null
                ? new ProjectDefinition(name, assembly, languageVersion, source, build)
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

    private static ProjectBuildDefinition BindBuild(SourceSnapshot snapshot, TomlConfigurationTable document, List<SushiDiagnostic> diagnostics, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!document.TryGetProperty("build", out TomlConfigurationProperty buildProperty))
        {
            diagnostics.Add(CreateMissingKeyDiagnostic(snapshot, "build"));

            return CreateEmptyBuild();
        }

        if (buildProperty.Value is TomlConfigurationInvalid)
        {
            return CreateEmptyBuild();
        }

        if (buildProperty.Value is not TomlConfigurationTable buildTable)
        {
            diagnostics.Add(CreateError(InvalidValueTypeDiagnosticCode, "Project configuration key \"build\" must be a table.", buildProperty.Value.Span));

            return CreateEmptyBuild();
        }

        string? defaultTarget = null;
        TomlConfigurationProperty? defaultProperty = null;
        List<string> sources = [];

        Dictionary<string, ProjectBuildTargetDefinition> targets = [with(StringComparer.Ordinal)];
        HashSet<string> declaredTargetNames = [with(StringComparer.Ordinal)];

        if (buildTable.TryGetProperty("default", out TomlConfigurationProperty foundDefaultProperty))
        {
            defaultProperty = foundDefaultProperty;

            defaultTarget = BindStringValue(foundDefaultProperty, "build.default", diagnostics);
        }
        else
        {
            diagnostics.Add(CreateMissingKeyDiagnostic(snapshot, "build.default"));
        }

        if (buildTable.TryGetProperty("sources", out TomlConfigurationProperty sourcesProperty))
        {
            sources.AddRange(BindStringArray(sourcesProperty, "build.sources", diagnostics, cancellationToken));
        }

        if (buildTable.TryGetProperty("targets", out TomlConfigurationProperty targetsProperty))
        {
            BindBuildTargets(snapshot, targetsProperty, targets, declaredTargetNames, diagnostics, cancellationToken);
        }

        if (declaredTargetNames.Count == 0)
        {
            diagnostics.Add(CreateError(MissingBuildTargetsDiagnosticCode, "Project configuration must define at least one build target.", buildTable.Span));
        }

        if (defaultTarget is not null && defaultProperty is not null && !declaredTargetNames.Contains(defaultTarget))
        {
            diagnostics.Add(CreateError(UnknownDefaultBuildTargetDiagnosticCode, $"Default build target \"{defaultTarget}\" is not declared in \"build.targets\".", defaultProperty.Value.Span));
        }

        return new ProjectBuildDefinition(defaultTarget, sources, targets);
    }

    private static void BindBuildTargets(SourceSnapshot snapshot, TomlConfigurationProperty targetsProperty, Dictionary<string, ProjectBuildTargetDefinition> targets, HashSet<string> declaredTargetNames, List<SushiDiagnostic> diagnostics, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (targetsProperty.Value is TomlConfigurationInvalid)
        {
            return;
        }

        if (targetsProperty.Value is not TomlConfigurationTable targetsTable)
        {
            diagnostics.Add(CreateError(InvalidValueTypeDiagnosticCode, "Project configuration key \"build.targets\" must be a table.", targetsProperty.Value.Span));

            return;
        }

        foreach (TomlConfigurationProperty targetProperty in targetsTable.Properties.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            declaredTargetNames.Add(targetProperty.Name);

            if (targetProperty.Value is TomlConfigurationInvalid)
            {
                continue;
            }

            if (targetProperty.Value is not TomlConfigurationTable targetTable)
            {
                diagnostics.Add(CreateError(InvalidValueTypeDiagnosticCode, $"Build target \"{targetProperty.Name}\" must be a table.", targetProperty.Value.Span));

                continue;
            }

            string? type = BindRequiredString(snapshot, targetTable, "type", $"build.targets.{targetProperty.Name}.type", diagnostics);

            if (type is null)
            {
                continue;
            }

            targets.TryAdd(targetProperty.Name, new ProjectBuildTargetDefinition(type));
        }
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

    private static ProjectBuildDefinition CreateEmptyBuild() => new(DefaultTarget: null, Sources: [], Targets: new Dictionary<string, ProjectBuildTargetDefinition>(StringComparer.Ordinal));

    private static SushiDiagnostic CreateMissingKeyDiagnostic(SourceSnapshot snapshot, string keyName)
    {
        int eof = snapshot.Bytes.Length;

        return new SushiDiagnostic(MissingRequiredKeyDiagnosticCode, $"Required project configuration key \"{keyName}\" is missing.", DiagnosticSeverity.Error, new SourceSpan(snapshot, eof, eof));
    }

    private static SushiDiagnostic CreateError(string code, string message, SourceSpan span) => new(code, message, DiagnosticSeverity.Error, span);
}
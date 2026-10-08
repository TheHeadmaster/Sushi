using Sushi.Analysis;
using Sushi.Configuration.Toml;
using Sushi.Diagnostics;
using Sushi.Source;

namespace Sushi.Configuration;

public sealed class ProjectConfigurationBinder
{
    public ProjectConfigurationBindResult Bind(SourceSnapshot snapshot, IDiagnosticReporter diagnosticReporter, TomlConfigurationTable document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(diagnosticReporter);
        ArgumentNullException.ThrowIfNull(document);

        cancellationToken.ThrowIfCancellationRequested();

        string? name = BindRequiredString(snapshot, document, diagnosticReporter, "name");
        string? assembly = BindRequiredString(snapshot, document, diagnosticReporter, "assembly");

        string? languageVersion = BindRequiredString(snapshot, document, diagnosticReporter, "language-version");

        if (name is { Length: 0 } && document.TryGetProperty("name", out TomlConfigurationProperty nameProperty))
        {
            diagnosticReporter.GenerateError(ErrorType.TomlEmptyProjectName, nameProperty.Value.Span);
        }

        ProjectSourceDefinition source = BindSource(document, diagnosticReporter, cancellationToken);

        ProjectBuildDefinition build = BindBuild(snapshot, document, diagnosticReporter, cancellationToken);

        ProjectDefinition? project =
            !diagnosticReporter.HasErrors()
            && name is not null
            && assembly is not null
            && languageVersion is not null
                ? new ProjectDefinition(name, assembly, languageVersion, source, build)
                : null;

        return new ProjectConfigurationBindResult(project);
    }

    private static string? BindRequiredString(SourceSnapshot snapshot, TomlConfigurationTable table, IDiagnosticReporter diagnosticReporter, string propertyName, string diagnosticPropertyName = "")
    {
        if (string.IsNullOrWhiteSpace(diagnosticPropertyName))
        {
            diagnosticPropertyName = propertyName;
        }

        if (!table.TryGetProperty(propertyName, out TomlConfigurationProperty property))
        {
            AccumulateMissingKeyDiagnostic(snapshot, diagnosticReporter, diagnosticPropertyName);

            return null;
        }

        return BindStringValue(property, propertyName, diagnosticReporter);
    }

    private static string? BindStringValue(TomlConfigurationProperty property, string diagnosticName, IDiagnosticReporter diagnosticReporter)
    {
        if (property.Value is TomlConfigurationInvalid)
        {
            return null;
        }

        if (property.Value is TomlConfigurationString stringValue)
        {
            return stringValue.Value;
        }

        AccumulateInvalidValueTypeDiagnostic(diagnosticReporter, diagnosticName, "string", property);

        return null;
    }

    private static ProjectSourceDefinition BindSource(TomlConfigurationTable document, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!document.TryGetProperty("source", out TomlConfigurationProperty sourceProperty))
        {
            return new ProjectSourceDefinition(DefaultNamespace: null, Exclude: []);
        }

        if (sourceProperty.Value is TomlConfigurationInvalid)
        {
            return new ProjectSourceDefinition(DefaultNamespace: null, Exclude: []);
        }

        if (sourceProperty.Value is not TomlConfigurationTable sourceTable)
        {
            AccumulateInvalidValueTypeDiagnostic(diagnosticReporter, "source", "table", sourceProperty);

            return new ProjectSourceDefinition(DefaultNamespace: null, Exclude: []);
        }

        string? defaultNamespace = null;
        List<string> exclude = [];

        if (sourceTable.TryGetProperty("default-namespace", out TomlConfigurationProperty defaultNamespaceProperty))
        {
            defaultNamespace = BindStringValue(defaultNamespaceProperty, "source.default-namespace", diagnosticReporter);
        }

        if (sourceTable.TryGetProperty("exclude", out TomlConfigurationProperty excludeProperty))
        {
            exclude.AddRange(BindStringArray(excludeProperty, "source.exclude", diagnosticReporter, cancellationToken));
        }

        return new ProjectSourceDefinition(defaultNamespace, exclude);
    }

    private static ProjectBuildDefinition BindBuild(SourceSnapshot snapshot, TomlConfigurationTable document, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!document.TryGetProperty("build", out TomlConfigurationProperty buildProperty))
        {
            AccumulateMissingKeyDiagnostic(snapshot, diagnosticReporter, "build");

            return CreateEmptyBuild();
        }

        if (buildProperty.Value is TomlConfigurationInvalid)
        {
            return CreateEmptyBuild();
        }

        if (buildProperty.Value is not TomlConfigurationTable buildTable)
        {
            AccumulateInvalidValueTypeDiagnostic(diagnosticReporter, "build", "table", buildProperty);

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

            defaultTarget = BindStringValue(foundDefaultProperty, "build.default", diagnosticReporter);
        }
        else
        {
            AccumulateMissingKeyDiagnostic(snapshot, diagnosticReporter, "build.default");
        }

        if (buildTable.TryGetProperty("sources", out TomlConfigurationProperty sourcesProperty))
        {
            sources.AddRange(BindStringArray(sourcesProperty, "build.sources", diagnosticReporter, cancellationToken));
        }

        if (buildTable.TryGetProperty("targets", out TomlConfigurationProperty targetsProperty))
        {
            BindBuildTargets(snapshot, targetsProperty, targets, declaredTargetNames, diagnosticReporter, cancellationToken);
        }

        if (declaredTargetNames.Count == 0)
        {
            diagnosticReporter.GenerateError(ErrorType.TomlMissingBuildTargets, buildTable.Span);
        }

        if (defaultTarget is not null && defaultProperty is not null && !declaredTargetNames.Contains(defaultTarget))
        {
            diagnosticReporter.GenerateErrorWithCustomMessage(ErrorType.TomlUnknownDefaultBuildTarget, $"Default build target \"{defaultTarget}\" is not declared in \"build.targets\".", defaultProperty.Value.Span);
        }

        return new ProjectBuildDefinition(defaultTarget, sources, targets);
    }

    private static void BindBuildTargets(SourceSnapshot snapshot, TomlConfigurationProperty targetsProperty, Dictionary<string, ProjectBuildTargetDefinition> targets, HashSet<string> declaredTargetNames, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (targetsProperty.Value is TomlConfigurationInvalid)
        {
            return;
        }

        if (targetsProperty.Value is not TomlConfigurationTable targetsTable)
        {
            AccumulateInvalidValueTypeDiagnostic(diagnosticReporter, "build.targets", "table", targetsProperty);

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
                AccumulateInvalidValueTypeDiagnostic(diagnosticReporter, targetProperty.Name, "table", targetProperty);

                continue;
            }

            string? type = BindRequiredString(snapshot, targetTable, diagnosticReporter, "type", $"build.targets.{targetProperty.Name}.type");

            if (type is null)
            {
                continue;
            }

            targets.TryAdd(targetProperty.Name, new ProjectBuildTargetDefinition(type));
        }
    }

    private static IReadOnlyList<string> BindStringArray(TomlConfigurationProperty property, string diagnosticName, IDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken)
    {
        if (property.Value is TomlConfigurationInvalid)
        {
            return [];
        }

        if (property.Value is not TomlConfigurationArray array)
        {
            AccumulateInvalidValueTypeDiagnostic(diagnosticReporter, diagnosticName, "string array", property);

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

            diagnosticReporter.GenerateErrorWithCustomMessage(ErrorType.TomlInvalidValueType, $"Project configuration key \"{diagnosticName}\" must contain only strings.", item.Span);
        }

        return values;
    }

    private static ProjectBuildDefinition CreateEmptyBuild() => new(DefaultTarget: null, Sources: [], Targets: new Dictionary<string, ProjectBuildTargetDefinition>(StringComparer.Ordinal));

    private static void AccumulateMissingKeyDiagnostic(SourceSnapshot snapshot, IDiagnosticReporter diagnosticReporter, string keyName)
    {
        int eof = snapshot.Bytes.Length;
        diagnosticReporter.GenerateErrorWithCustomMessage(ErrorType.TomlMissingRequiredKey, $"Required project configuration key \"{keyName}\" is missing.", new SourceSpan(snapshot, eof, eof));
    }
  
    private static void AccumulateInvalidValueTypeDiagnostic(IDiagnosticReporter diagnosticReporter, string propertyName, string typeName, TomlConfigurationProperty sourceProperty)
        => diagnosticReporter.GenerateErrorWithCustomMessage(
            ErrorType.TomlInvalidValueType,
            $"Project configuration key \"{propertyName}\" must be a {typeName}.",
            sourceProperty.Value.Span);
}
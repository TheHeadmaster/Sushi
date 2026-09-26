namespace Sushi.Analysis;

public sealed record ProjectBuildDefinition(string? DefaultTarget, IReadOnlyList<string> Sources, IReadOnlyDictionary<string, ProjectBuildTargetDefinition> Targets);
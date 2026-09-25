namespace Sushi.Analysis;

public sealed record ProjectSourceDefinition(string? DefaultNamespace, IReadOnlyList<string> Exclude);
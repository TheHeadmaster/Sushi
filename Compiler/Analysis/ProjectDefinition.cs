namespace Sushi.Analysis;

public sealed record ProjectDefinition(string Name, string Assembly, string LanguageVersion, ProjectSourceDefinition Source);
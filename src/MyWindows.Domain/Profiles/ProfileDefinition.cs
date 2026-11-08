namespace MyWindows.Domain.Profiles;

public sealed record ProfileDefinition(int SchemaVersion, string Id, IReadOnlyList<string> Modules);

namespace MyWindows.Domain.Modules;

public sealed record ModuleDefinition(
    string Id,
    string Version,
    IReadOnlyList<string> Requires,
    IReadOnlyList<string> Conflicts);

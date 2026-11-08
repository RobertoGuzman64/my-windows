namespace MyWindows.Domain.Profiles;

public sealed record ResolvedProfiles(
    IReadOnlyList<string> ProfileIds,
    IReadOnlyList<string> ModuleIds);

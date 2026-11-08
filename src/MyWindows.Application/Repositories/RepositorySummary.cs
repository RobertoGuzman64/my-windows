namespace MyWindows.Application.Repositories;

public sealed record RepositorySummary(string RootPath, string Branch, bool IsDetached, bool HasChanges);

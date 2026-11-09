namespace MyWindows.Application.Repositories;

public sealed record RepositoryInspectionEntry(string RequestedPath, RepositorySummary? Summary, string? Error)
{
    public bool Success => Summary is not null;
}

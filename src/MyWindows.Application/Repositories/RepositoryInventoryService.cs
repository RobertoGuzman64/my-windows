namespace MyWindows.Application.Repositories;

public sealed class RepositoryInventoryService
{
    private readonly RepositorySummaryService _service;

    public RepositoryInventoryService(RepositorySummaryService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    public IReadOnlyList<RepositoryInspectionEntry> Inspect(IReadOnlyList<string> directories)
    {
        ArgumentNullException.ThrowIfNull(directories);
        if (directories.Count == 0)
        {
            throw new ArgumentException("Es necesario indicar al menos un repositorio.", nameof(directories));
        }

        List<RepositoryInspectionEntry> results = new(directories.Count);
        foreach (string directory in directories)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(directory);
            try
            {
                RepositorySummary summary = _service.Inspect(directory);
                results.Add(new RepositoryInspectionEntry(directory, summary, null));
            }
            catch (Exception exception) when (exception is GitInspectionException or IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
            {
                results.Add(new RepositoryInspectionEntry(directory, null, exception.Message));
            }
        }

        return results;
    }
}

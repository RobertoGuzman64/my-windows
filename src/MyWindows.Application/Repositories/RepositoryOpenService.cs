namespace MyWindows.Application.Repositories;

public sealed class RepositoryOpenService
{
    private readonly RepositorySummaryService _service;
    private readonly IRepositoryFolderOpener _opener;

    public RepositoryOpenService(RepositorySummaryService service, IRepositoryFolderOpener opener)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _opener = opener ?? throw new ArgumentNullException(nameof(opener));
    }

    public string Open(string directory)
    {
        RepositorySummary summary = _service.Inspect(directory);
        _opener.Open(summary.RootPath);
        return summary.RootPath;
    }
}

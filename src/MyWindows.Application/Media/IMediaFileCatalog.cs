namespace MyWindows.Application.Media;

public interface IMediaFileCatalog
{
    MediaDiscovery Discover(string rootPath, CancellationToken cancellationToken = default);
}

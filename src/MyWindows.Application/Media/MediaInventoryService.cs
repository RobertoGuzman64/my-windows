using System.Security;
using MyWindows.Application.Files;

namespace MyWindows.Application.Media;

public sealed class MediaInventoryService
{
    private static readonly HashSet<string> MusicExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".aac", ".aif", ".aiff", ".alac", ".flac", ".m4a", ".mp3", ".ogg", ".opus", ".wav", ".wma"
    };

    private static readonly HashSet<string> PhotoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".arw", ".bmp", ".cr2", ".cr3", ".dng", ".gif", ".heic", ".heif", ".jpeg", ".jpg",
        ".nef", ".orf", ".png", ".raf", ".raw", ".rw2", ".tif", ".tiff", ".webp"
    };

    private readonly IMediaFileCatalog _catalog;
    private readonly FileHashService _hashes;

    public MediaInventoryService(IMediaFileCatalog catalog, FileHashService hashes)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _hashes = hashes ?? throw new ArgumentNullException(nameof(hashes));
    }

    public async Task<MediaInventoryResult> InspectAsync(MediaKind kind, string rootPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        if (kind is not (MediaKind.Music or MediaKind.Photos))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        cancellationToken.ThrowIfCancellationRequested();
        MediaDiscovery discovery = _catalog.Discover(rootPath, cancellationToken);
        HashSet<string> extensions = kind == MediaKind.Music ? MusicExtensions : PhotoExtensions;
        List<MediaScanIssue> issues = new(discovery.Issues);
        List<MediaFileEntry> files = [];

        foreach (string path in discovery.FilePaths.OrderBy(path => path, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!extensions.Contains(Path.GetExtension(path)))
            {
                continue;
            }

            try
            {
                FileHashResult hash = await _hashes.ComputeSha256Async(path, cancellationToken).ConfigureAwait(false);
                files.Add(new MediaFileEntry(hash.FullPath, hash.LengthBytes, hash.Sha256));
            }
            catch (Exception exception) when (exception is FileHashException or IOException or UnauthorizedAccessException or SecurityException)
            {
                issues.Add(new MediaScanIssue(path, exception.Message));
            }
        }

        ExactDuplicateGroup[] duplicates = files
            .GroupBy(file => (file.LengthBytes, file.Sha256))
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key.Sha256, StringComparer.Ordinal)
            .ThenBy(group => group.Key.LengthBytes)
            .Select(group => new ExactDuplicateGroup(group.Key.Sha256, group.Key.LengthBytes,
                group.Select(file => file.FullPath).ToArray()))
            .ToArray();

        return new MediaInventoryResult(kind, discovery.RootPath, files, duplicates, issues);
    }
}

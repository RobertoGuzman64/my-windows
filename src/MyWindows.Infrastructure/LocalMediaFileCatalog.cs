using System.Security;
using MyWindows.Application.Media;

namespace MyWindows.Infrastructure;

public sealed class LocalMediaFileCatalog : IMediaFileCatalog
{
    public MediaDiscovery Discover(string rootPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        cancellationToken.ThrowIfCancellationRequested();

        if (OperatingSystem.IsWindows() &&
            (rootPath.StartsWith(@"\\.\", StringComparison.Ordinal) ||
             rootPath.StartsWith(@"\\?\", StringComparison.Ordinal)))
        {
            throw new ArgumentException("No se admiten rutas de dispositivo de Windows.", nameof(rootPath));
        }

        string fullPath = Path.GetFullPath(rootPath);
        if (OperatingSystem.IsWindows() && fullPath.StartsWith(@"\\", StringComparison.Ordinal))
        {
            throw new ArgumentException("El análisis multimedia solo admite carpetas locales.", nameof(rootPath));
        }

        FileAttributes rootAttributes = File.GetAttributes(fullPath);
        if ((rootAttributes & FileAttributes.Directory) == 0 || (rootAttributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new ArgumentException("Es necesario indicar una carpeta real, no un enlace.", nameof(rootPath));
        }

        List<string> files = [];
        List<MediaScanIssue> issues = [];
        Stack<string> pending = new();
        pending.Push(fullPath);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string directory = pending.Pop();
            try
            {
                foreach (string path in Directory.EnumerateFileSystemEntries(directory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        FileAttributes attributes = File.GetAttributes(path);
                        if ((attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            issues.Add(new MediaScanIssue(path, "Enlace o punto de unión omitido."));
                        }
                        else if ((attributes & FileAttributes.Directory) != 0)
                        {
                            pending.Push(path);
                        }
                        else if ((attributes & FileAttributes.Device) != 0)
                        {
                            issues.Add(new MediaScanIssue(path, "Recurso no regular omitido."));
                        }
                        else
                        {
                            files.Add(path);
                        }
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
                    {
                        issues.Add(new MediaScanIssue(path, exception.Message));
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
            {
                issues.Add(new MediaScanIssue(directory, exception.Message));
            }
        }

        return new MediaDiscovery(fullPath, files, issues);
    }
}

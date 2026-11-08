using MyWindows.Application.Files;

namespace MyWindows.Infrastructure;

public sealed class LocalFileReadGateway : IFileReadGateway
{
    public IFileReadSession OpenRead(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (OperatingSystem.IsWindows() &&
            (path.StartsWith(@"\\.\", StringComparison.Ordinal) ||
             path.StartsWith(@"\\?\", StringComparison.Ordinal)))
        {
            throw new FileHashException("No se admiten rutas de dispositivo de Windows.");
        }

        string fullPath = Path.GetFullPath(path);
        if (OperatingSystem.IsWindows() && fullPath.StartsWith(@"\\", StringComparison.Ordinal))
        {
            throw new FileHashException("La primera acción de archivos solo admite rutas locales.");
        }

        FileAttributes attributes = File.GetAttributes(fullPath);
        if ((attributes & FileAttributes.Directory) != 0 || (attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new FileHashException("Solo se admiten archivos regulares; no carpetas ni enlaces simbólicos.");
        }

        FileStream stream = new(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        try
        {
            FileInfo info = new(fullPath);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0 || info.Length != stream.Length)
            {
                throw new FileHashException("El recurso cambió durante la apertura.");
            }

            return new Session(stream, fullPath, info.Length, info.LastWriteTimeUtc);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    private sealed class Session : IFileReadSession
    {
        private readonly FileStream _stream;
        private readonly DateTime _initialWriteTimeUtc;

        public Session(FileStream stream, string fullPath, long length, DateTime initialWriteTimeUtc)
        {
            _stream = stream;
            FullPath = fullPath;
            InitialLength = length;
            _initialWriteTimeUtc = initialWriteTimeUtc;
        }

        public string FullPath { get; }

        public long InitialLength { get; }

        public Stream Content => _stream;

        public void VerifyUnchanged()
        {
            FileInfo current = new(FullPath);
            current.Refresh();
            if (!current.Exists || (current.Attributes & FileAttributes.ReparsePoint) != 0 ||
                current.Length != InitialLength || current.LastWriteTimeUtc != _initialWriteTimeUtc ||
                _stream.Length != InitialLength)
            {
                throw new FileHashException("El archivo ha cambiado durante el cálculo; resultado descartado.");
            }
        }

        public void Dispose() => _stream.Dispose();
    }
}

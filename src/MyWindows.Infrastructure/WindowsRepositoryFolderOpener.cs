using System.ComponentModel;
using System.Diagnostics;
using MyWindows.Application.Repositories;

namespace MyWindows.Infrastructure;

public sealed class WindowsRepositoryFolderOpener : IRepositoryFolderOpener
{
    public void Open(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Abrir carpetas en Explorer requiere Windows.");
        }

        string fullPath = Path.GetFullPath(directory);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException("La carpeta del repositorio ya no existe.");
        }

        string windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrWhiteSpace(windowsDirectory))
        {
            throw new InvalidOperationException("No se ha encontrado la ubicación de Windows.");
        }

        ProcessStartInfo start = new(Path.Combine(windowsDirectory, "explorer.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add(fullPath);
        try
        {
            using Process? process = Process.Start(start);
            if (process is null)
            {
                throw new InvalidOperationException("Explorer no ha podido abrir el repositorio.");
            }
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException("Explorer no está disponible.", exception);
        }
    }
}

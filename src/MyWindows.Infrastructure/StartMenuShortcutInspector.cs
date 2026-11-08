using MyWindows.Application.Planning;

namespace MyWindows.Infrastructure;

public static class StartMenuShortcutInspector
{
    public static ShortcutObservation Inspect(string repositoryPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("La inspección del menú Inicio requiere Windows.");
        }

        string programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs, Environment.SpecialFolderOption.DoNotVerify);
        return Inspect(repositoryPath, programs);
    }

    public static ShortcutObservation Inspect(string repositoryPath, string programsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(programsDirectory);
        string fullRepositoryPath = Path.GetFullPath(repositoryPath);
        bool available = Directory.Exists(programsDirectory);
        string shortcut = Path.Combine(programsDirectory, RepoShortcutModule.ShortcutFileName);
        bool occupied = PathIsOccupied(shortcut);

        return new ShortcutObservation(fullRepositoryPath, Directory.Exists(fullRepositoryPath),
            programsDirectory, available, shortcut, occupied);
    }

    private static bool PathIsOccupied(string path)
    {
        try
        {
            _ = File.GetAttributes(path);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }
}

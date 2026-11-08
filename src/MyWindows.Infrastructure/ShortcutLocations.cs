using MyWindows.Application.Planning;

namespace MyWindows.Infrastructure;

public sealed record ShortcutLocations(string ProgramsDirectory, string OwnershipDirectory)
{
    public static ShortcutLocations CurrentUser()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("El menú Inicio requiere Windows.");
        }

        string programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs, Environment.SpecialFolderOption.DoNotVerify);
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
        if (string.IsNullOrWhiteSpace(programs) || string.IsNullOrWhiteSpace(local))
        {
            throw new InvalidOperationException("No se han podido resolver las carpetas del usuario.");
        }

        return new ShortcutLocations(programs, Path.Combine(local, "MyWindows", "ownership"));
    }

    public string ShortcutPath => Path.Combine(ProgramsDirectory, RepoShortcutModule.ShortcutFileName);
    public string ReceiptPath => Path.Combine(OwnershipDirectory, "repo-shortcut.json");
}

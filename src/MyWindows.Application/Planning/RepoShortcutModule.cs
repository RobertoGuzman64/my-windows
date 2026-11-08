using MyWindows.Domain.Modules;

namespace MyWindows.Application.Planning;

public static class RepoShortcutModule
{
    public const string Id = "repo-shortcut";
    public const string ShortcutFileName = "MyWindows — Abrir repositorio.lnk";

    public static ModuleDefinition Definition { get; } = new(
        Id, "1.0.0", Array.Empty<string>(), Array.Empty<string>());
}

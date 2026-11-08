using MyWindows.Application.Planning;
using MyWindows.Infrastructure;

namespace MyWindows.Tests.Planning;

[TestClass]
public sealed class StartMenuShortcutInspectorTests
{
    [TestMethod]
    public void Inspect_DoesNotCreateOrRewriteUserShortcut()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.ThrowsExactly<PlatformNotSupportedException>(() => StartMenuShortcutInspector.Inspect("."));
            return;
        }

        string programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs, Environment.SpecialFolderOption.DoNotVerify);
        if (string.IsNullOrWhiteSpace(programs) || !Directory.Exists(programs))
        {
            Assert.Inconclusive("No se ha podido comprobar la carpeta Programas en este runner Windows.");
            return;
        }

        string shortcut = Path.Combine(programs, RepoShortcutModule.ShortcutFileName);
        string[] before = Directory.GetFileSystemEntries(programs).OrderBy(path => path, StringComparer.Ordinal).ToArray();
        bool existed = File.Exists(shortcut);
        byte[]? bytes = existed ? File.ReadAllBytes(shortcut) : null;

        ShortcutObservation result = StartMenuShortcutInspector.Inspect(Directory.GetCurrentDirectory());

        string[] after = Directory.GetFileSystemEntries(programs).OrderBy(path => path, StringComparer.Ordinal).ToArray();
        Assert.AreSequenceEqual(before, after);
        Assert.AreEqual(existed, File.Exists(shortcut));
        if (bytes is not null)
        {
            Assert.AreSequenceEqual(bytes, File.ReadAllBytes(shortcut));
        }

        Assert.AreEqual(shortcut, result.ShortcutPath);
    }
}

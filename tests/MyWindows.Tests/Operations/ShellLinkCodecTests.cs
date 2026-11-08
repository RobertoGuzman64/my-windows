using MyWindows.Infrastructure;

namespace MyWindows.Tests.Operations;

[TestClass]
public sealed class ShellLinkCodecTests
{
    [TestMethod]
    public void WindowsShellLink_RoundTripsTargetAndOwnerTagInSandbox()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string root = Path.Combine(Path.GetTempPath(), $"mw-shell-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "prueba.lnk");
        try
        {
            ShellLinkCodec codec = new();
            string tag = "MyWindows:repo-shortcut:0123456789abcdef0123456789abcdef";
            File.WriteAllBytes(path, codec.Create(root, tag));
            ShortcutMetadata read = codec.Read(path);
            Assert.IsTrue(string.Equals(Path.GetFullPath(root), Path.GetFullPath(read.TargetPath), StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual(tag, read.OwnerTag);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}

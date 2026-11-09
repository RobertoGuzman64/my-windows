using MyWindows.Application.Programs;
using MyWindows.Infrastructure;

namespace MyWindows.Tests.Programs;

[TestClass]
public sealed class InstalledProgramInventoryServiceTests
{
    [TestMethod]
    public void Inspect_SortsRegistryEntriesWithoutInventingVersionsOrRemovingDuplicates()
    {
        InstalledProgramInventory snapshot = new(
            new DateTimeOffset(2026, 11, 8, 0, 0, 0, TimeSpan.Zero),
            [
                new InstalledProgramEntry("Zeta", null, null, "HKLM-native\\id1"),
                new InstalledProgramEntry("Alpha", "2.0", "Editor", "HKCU\\id2"),
                new InstalledProgramEntry("Alpha", "2.0", "Editor", "HKLM-32\\id3")
            ],
            [new InstalledProgramIssue("HKLM-32\\blocked", "Acceso denegado")]);

        InstalledProgramInventory result = new InstalledProgramInventoryService(new FixedSource(snapshot)).Inspect();

        Assert.AreEqual("windows-uninstall-registry", result.Scope);
        Assert.IsFalse(result.IsComplete);
        Assert.AreEqual(snapshot.CapturedAtUtc, result.CapturedAtUtc);
        Assert.AreEqual(3, result.Programs.Count);
        Assert.AreEqual("HKCU\\id2", result.Programs[0].Source);
        Assert.AreEqual("HKLM-32\\id3", result.Programs[1].Source);
        Assert.AreEqual("Zeta", result.Programs[2].Name);
        Assert.IsNull(result.Programs[2].Version);
        Assert.AreEqual(1, result.Issues.Count);
    }

    [TestMethod]
    public void Inspect_EmptyRegistrySource_DoesNotPretendToDiscoverOtherInstallers()
    {
        InstalledProgramInventory empty = new(DateTimeOffset.UnixEpoch, [], []);

        InstalledProgramInventory result = new InstalledProgramInventoryService(new FixedSource(empty)).Inspect();

        Assert.AreEqual(0, result.Programs.Count);
        Assert.IsTrue(result.IsComplete);
        Assert.AreEqual("windows-uninstall-registry", result.Scope);
    }

    [TestMethod]
    public void Read_ReturnsInventoryOnWindowsAndRejectsOtherPlatforms()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.ThrowsExactly<PlatformNotSupportedException>(() => new WindowsInstalledProgramSource().Read());
            return;
        }

        InstalledProgramInventory inventory = new WindowsInstalledProgramSource().Read();
        Assert.AreEqual("windows-uninstall-registry", inventory.Scope);
        Assert.IsTrue(inventory.CapturedAtUtc > DateTimeOffset.UnixEpoch);
        Assert.IsNotNull(inventory.Programs);
        Assert.IsNotNull(inventory.Issues);
    }

    private sealed class FixedSource(InstalledProgramInventory inventory) : IInstalledProgramSource
    {
        public InstalledProgramInventory Read() => inventory;
    }
}

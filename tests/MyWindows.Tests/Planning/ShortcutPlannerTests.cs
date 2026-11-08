using System.Text.Json;
using MyWindows.Application.Planning;
using MyWindows.Domain.Modules;

namespace MyWindows.Tests.Planning;

[TestClass]
public sealed class ShortcutPlannerTests
{
    [TestMethod]
    public void Create_EmptyModules_ReturnsEmptyPlan()
    {
        PlanResult result = ShortcutPlanner.Create(Array.Empty<ModuleDefinition>(), null);

        Assert.IsTrue(result.IsFeasible);
        Assert.IsEmpty(result.Resources);
    }

    [TestMethod]
    public void Create_AbsentShortcut_ProposesCreateWithoutMutatingFilesystem()
    {
        string temp = Path.Combine(Path.GetTempPath(), $"mw-plan-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);
        string sentinel = Path.Combine(temp, "sentinel.txt");
        File.WriteAllText(sentinel, "intacto");
        try
        {
            ShortcutObservation observed = new(temp, true, temp, true, Path.Combine(temp, RepoShortcutModule.ShortcutFileName), false);
            string[] before = Directory.GetFileSystemEntries(temp).OrderBy(path => path, StringComparer.Ordinal).ToArray();

            PlanResult first = ShortcutPlanner.Create(new[] { RepoShortcutModule.Definition }, observed);
            PlanResult second = ShortcutPlanner.Create(new[] { RepoShortcutModule.Definition }, observed);

            Assert.IsTrue(first.IsFeasible);
            Assert.AreEqual(PlanOperation.Create, first.Resources[0].Operation);
            Assert.AreEqual(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
            Assert.AreSequenceEqual(before, Directory.GetFileSystemEntries(temp).OrderBy(path => path, StringComparer.Ordinal).ToArray());
            Assert.AreEqual("intacto", File.ReadAllText(sentinel));
        }
        finally
        {
            Directory.Delete(temp, true);
        }
    }

    [TestMethod]
    public void Create_ForeignOrUnknownShortcut_BlocksPlan()
    {
        ShortcutObservation observed = new("C:\\repo", true, "C:\\start", true, "C:\\start\\MyWindows.lnk", true);

        PlanResult result = ShortcutPlanner.Create(new[] { RepoShortcutModule.Definition }, observed);

        Assert.IsFalse(result.IsFeasible);
        Assert.AreEqual(PlanOperation.Blocked, result.Resources[0].Operation);
        StringAssert.Contains(result.Resources[0].Reason, "propiedad");
    }

    [TestMethod]
    public void Create_MissingTarget_BlocksPlan()
    {
        ShortcutObservation observed = new("C:\\repo", false, "C:\\start", true, "C:\\start\\MyWindows.lnk", false);

        Assert.IsFalse(ShortcutPlanner.Create(new[] { RepoShortcutModule.Definition }, observed).IsFeasible);
    }

    [TestMethod]
    public void Create_UnavailableProgramsDirectory_BlocksPlan()
    {
        ShortcutObservation observed = new("C:\\repo", true, null, false, null, false);

        Assert.IsFalse(ShortcutPlanner.Create(new[] { RepoShortcutModule.Definition }, observed).IsFeasible);
    }

    [TestMethod]
    public void Create_UnknownModule_RejectsPlanning()
    {
        ModuleDefinition unknown = new("other", "1.0.0", Array.Empty<string>(), Array.Empty<string>());

        Assert.ThrowsExactly<ArgumentException>(() =>
            ShortcutPlanner.Create(new[] { unknown }, null));
    }
}

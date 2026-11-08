using MyWindows.Domain.Modules;
using MyWindows.Domain.Profiles;

namespace MyWindows.Tests.Modules;

[TestClass]
public sealed class ModuleResolverTests
{
    [TestMethod]
    public void Resolve_KnownModule_ReturnsItsContract()
    {
        ModuleDefinition definition = Module("repo-shortcut");

        IReadOnlyList<ModuleDefinition> result = ModuleResolver.Resolve(Selected("repo-shortcut"), new[] { definition });

        Assert.AreEqual(1, result.Count);
        Assert.AreSame(definition, result[0]);
    }

    [TestMethod]
    public void Resolve_UnknownModule_RejectsConfiguration()
    {
        Assert.ThrowsExactly<ModuleResolutionException>(() =>
            ModuleResolver.Resolve(Selected("unknown"), new[] { Module("repo-shortcut") }));
    }

    [TestMethod]
    public void Resolve_MissingDependency_RejectsConfiguration()
    {
        Assert.ThrowsExactly<ModuleResolutionException>(() =>
            ModuleResolver.Resolve(Selected("tools"), new[] { Module("tools", requires: new[] { "repo-shortcut" }) }));
    }

    [TestMethod]
    public void Resolve_ConflictingModules_RejectsConfiguration()
    {
        ModuleDefinition[] catalog =
        {
            Module("tools", conflicts: new[] { "repo-shortcut" }),
            Module("repo-shortcut")
        };

        Assert.ThrowsExactly<ModuleResolutionException>(() =>
            ModuleResolver.Resolve(Selected("tools", "repo-shortcut"), catalog));
    }

    [TestMethod]
    public void Resolve_PresentDependency_AllowsModule()
    {
        ModuleDefinition[] catalog =
        {
            Module("tools", requires: new[] { "repo-shortcut" }),
            Module("repo-shortcut")
        };

        IReadOnlyList<ModuleDefinition> result = ModuleResolver.Resolve(Selected("tools", "repo-shortcut"), catalog);

        Assert.AreSequenceEqual(new[] { "repo-shortcut", "tools" }, result.Select(module => module.Id));
    }

    [TestMethod]
    public void Resolve_IllegalSelfDependency_RejectsDefinition()
    {
        Assert.ThrowsExactly<ModuleResolutionException>(() =>
            ModuleResolver.Resolve(Selected("tools"), new[] { Module("tools", requires: new[] { "tools" }) }));
    }

    [TestMethod]
    public void Resolve_DuplicateDefinitions_RejectsCatalog()
    {
        Assert.ThrowsExactly<ModuleResolutionException>(() =>
            ModuleResolver.Resolve(Selected("tools"), new[] { Module("tools"), Module("tools") }));
    }

    private static ResolvedProfiles Selected(params string[] ids)
    {
        return new ResolvedProfiles(new[] { "base" }, ids);
    }

    private static ModuleDefinition Module(
        string id,
        IReadOnlyList<string>? requires = null,
        IReadOnlyList<string>? conflicts = null)
    {
        return new ModuleDefinition(id, "1.0.0", requires ?? Array.Empty<string>(), conflicts ?? Array.Empty<string>());
    }
}

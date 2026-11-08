using MyWindows.Domain.Profiles;

namespace MyWindows.Tests.Profiles;

[TestClass]
public sealed class ProfileResolverTests
{
    [TestMethod]
    public void Resolve_BaseRolesAndMachine_DeduplicatesAndOrdersDeterministically()
    {
        ProfileDefinition[] catalog =
        {
            Profile("laptop", "repo-shortcut"),
            Profile("role-z", "repo-shortcut", "tools"),
            Profile("base", "repo-shortcut"),
            Profile("role-a", "tools")
        };

        ResolvedProfiles result = ProfileResolver.Resolve(catalog, new[] { "role-z", "role-a" }, "laptop");
        ResolvedProfiles reordered = ProfileResolver.Resolve(catalog.Reverse(), new[] { "role-a", "role-z" }, "laptop");

        Assert.AreSequenceEqual(new[] { "base", "role-a", "role-z", "laptop" }, result.ProfileIds);
        Assert.AreSequenceEqual(new[] { "repo-shortcut", "tools" }, result.ModuleIds);
        Assert.AreSequenceEqual(result.ProfileIds, reordered.ProfileIds);
        Assert.AreSequenceEqual(result.ModuleIds, reordered.ModuleIds);
    }

    [TestMethod]
    public void Resolve_BaseOnly_ProducesCurrentModules()
    {
        ResolvedProfiles result = ProfileResolver.Resolve(new[] { Profile("base", "repo-shortcut") }, Array.Empty<string>(), null);

        Assert.AreSequenceEqual(new[] { "base" }, result.ProfileIds);
        Assert.AreSequenceEqual(new[] { "repo-shortcut" }, result.ModuleIds);
    }

    [TestMethod]
    public void Resolve_NoBase_RejectsConfiguration()
    {
        Assert.ThrowsExactly<ProfileValidationException>(() =>
            ProfileResolver.Resolve(new[] { Profile("laptop") }, Array.Empty<string>(), null));
    }

    [TestMethod]
    public void Resolve_MissingSelectedRole_RejectsConfiguration()
    {
        Assert.ThrowsExactly<ProfileValidationException>(() =>
            ProfileResolver.Resolve(new[] { Profile("base") }, new[] { "dev" }, null));
    }

    [TestMethod]
    public void Resolve_OverlappingSelections_RejectsConfiguration()
    {
        Assert.ThrowsExactly<ProfileValidationException>(() =>
            ProfileResolver.Resolve(new[] { Profile("base"), Profile("dev") }, new[] { "dev" }, "dev"));
    }

    [TestMethod]
    public void Resolve_RepeatedRole_RejectsConfiguration()
    {
        Assert.ThrowsExactly<ProfileValidationException>(() =>
            ProfileResolver.Resolve(new[] { Profile("base"), Profile("dev") }, new[] { "dev", "dev" }, null));
    }

    private static ProfileDefinition Profile(string id, params string[] modules)
    {
        return new ProfileDefinition(ProfileJson.CurrentSchemaVersion, id, modules);
    }
}

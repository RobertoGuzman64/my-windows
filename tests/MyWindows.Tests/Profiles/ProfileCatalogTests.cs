using MyWindows.Domain.Profiles;

namespace MyWindows.Tests.Profiles;

[TestClass]
public sealed class ProfileCatalogTests
{
    [TestMethod]
    public void Validate_UnsortedProfiles_SortsDeterministically()
    {
        ProfileDefinition laptop = ProfileJson.Parse("""{"schemaVersion":1,"id":"laptop","modules":[]}""");
        ProfileDefinition baseProfile = ProfileJson.Parse("""{"schemaVersion":1,"id":"base","modules":[]}""");

        IReadOnlyList<ProfileDefinition> catalog = ProfileCatalog.Validate(new[] { laptop, baseProfile });

        Assert.AreSequenceEqual(new[] { "base", "laptop" }, catalog.Select(profile => profile.Id));
    }

    [TestMethod]
    public void Validate_DuplicateIdentifiers_RejectsInput()
    {
        ProfileDefinition profile = ProfileJson.Parse("""{"schemaVersion":1,"id":"base","modules":[]}""");

        Assert.ThrowsExactly<ProfileValidationException>(() => ProfileCatalog.Validate(new[] { profile, profile }));
    }

    [TestMethod]
    public void Validate_EmptyCatalog_ReturnsEmpty()
    {
        Assert.IsEmpty(ProfileCatalog.Validate(Array.Empty<ProfileDefinition>()));
    }
    [TestMethod]
    public void Validate_InvalidProgrammaticDefinition_RejectsInput()
    {
        ProfileDefinition invalid = new(1, "base", new[] { "repo-shortcut", "repo-shortcut" });

        Assert.ThrowsExactly<ProfileValidationException>(() => ProfileCatalog.Validate(new[] { invalid }));
    }

}

using MyWindows.Domain.Profiles;

namespace MyWindows.Tests.Profiles;

[TestClass]
public sealed class ProfileJsonTests
{
    [TestMethod]
    public void Parse_ValidProfile_ReturnsNormalizedContract()
    {
        ProfileDefinition profile = ProfileJson.Parse("""{"schemaVersion":1,"id":"base","modules":["dev-tools","network"]}""");

        Assert.AreEqual(ProfileJson.CurrentSchemaVersion, profile.SchemaVersion);
        Assert.AreEqual("base", profile.Id);
        Assert.AreSequenceEqual(new[] { "dev-tools", "network" }, profile.Modules);
    }

    [TestMethod]
    public void Parse_EmptyModuleList_IsValid()
    {
        ProfileDefinition profile = ProfileJson.Parse("""{"schemaVersion":1,"id":"base","modules":[]}""");

        Assert.IsEmpty(profile.Modules);
    }

    [TestMethod]
    [DataRow("[]")]
    [DataRow("null")]
    [DataRow("""{"schemaVersion":1,"id":"base","modules":{}}""")]
    [DataRow("""{"schemaVersion":2,"id":"base","modules":[]}""")]
    [DataRow("""{"schemaVersion":"1","id":"base","modules":[]}""")]
    [DataRow("""{"schemaVersion":1,"id":"Base","modules":[]}""")]
    [DataRow("""{"schemaVersion":1,"id":"base","modules":[],"secret":"value"}""")]
    [DataRow("""{"schemaVersion":1,"id":"base","modules":["dev","dev"]}""")]
    [DataRow("""{"schemaVersion":1,"id":"base","modules":[null]}""")]
    [DataRow("""{"schemaVersion":1,"id":"base","modules":[],"id":"other"}""")]
    public void Parse_InvalidProfile_RejectsInput(string json)
    {
        Assert.ThrowsExactly<ProfileValidationException>(() => ProfileJson.Parse(json));
    }

    [TestMethod]
    public void Parse_InvalidJson_RejectsInput()
    {
        Assert.ThrowsExactly<ProfileValidationException>(() => ProfileJson.Parse("{"));
    }

    [TestMethod]
    public void Parse_Null_RejectsInput()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => ProfileJson.Parse(null!));
    }
}

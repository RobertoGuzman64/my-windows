using MyWindows.Domain.Profiles;

namespace MyWindows.Tests.Architecture;

[TestClass]
public sealed class DomainBoundaryTests
{
    [TestMethod]
    public void Domain_DoesNotReferenceOtherMyWindowsProjectsOrWindowsAssemblies()
    {
        string[] dependencies = typeof(ProfileDefinition).Assembly.GetReferencedAssemblies()
            .Select(assembly => assembly.Name ?? string.Empty)
            .ToArray();

        Assert.IsFalse(dependencies.Any(name => name.StartsWith("MyWindows.", StringComparison.Ordinal)));
        Assert.IsFalse(dependencies.Any(name => name.StartsWith("Microsoft.Win32", StringComparison.Ordinal)));
        Assert.IsFalse(dependencies.Any(name => name.StartsWith("System.Windows", StringComparison.Ordinal)));
    }
}

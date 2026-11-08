using MyWindows.Application.Planning;

namespace MyWindows.Tests.Architecture;

[TestClass]
public sealed class ApplicationBoundaryTests
{
    [TestMethod]
    public void Application_DoesNotDependOnInfrastructureOrHost()
    {
        string[] dependencies = typeof(ShortcutPlanner).Assembly.GetReferencedAssemblies()
            .Select(assembly => assembly.Name ?? string.Empty)
            .ToArray();

        Assert.IsFalse(dependencies.Contains("MyWindows.Infrastructure", StringComparer.Ordinal));
        Assert.IsFalse(dependencies.Contains("MyWindows.Cli", StringComparer.Ordinal));
    }
}

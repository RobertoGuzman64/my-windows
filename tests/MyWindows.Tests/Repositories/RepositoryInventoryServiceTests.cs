using MyWindows.Application.Repositories;

namespace MyWindows.Tests.Repositories;

[TestClass]
public sealed class RepositoryInventoryServiceTests
{
    [TestMethod]
    public void Inspect_CleanDirtyAndInaccessibleRepositories_PreservesOrderAndContinues()
    {
        RecordingRunner runner = new([
            new GitCommandResult(0, "C:/projects/clean\n", ""),
            new GitCommandResult(0, "main\n", ""),
            new GitCommandResult(0, "", ""),
            new GitCommandResult(128, "", "not a repository"),
            new GitCommandResult(0, "D:/projects/dirty\n", ""),
            new GitCommandResult(0, "feature\n", ""),
            new GitCommandResult(0, "?? file.txt\n", "")
        ]);
        RepositoryInventoryService service = new(new RepositorySummaryService(runner));

        IReadOnlyList<RepositoryInspectionEntry> results = service.Inspect([
            "C:/projects/clean", "C:/projects/missing", "D:/projects/dirty"
        ]);

        Assert.AreEqual(3, results.Count);
        Assert.AreEqual("C:/projects/clean", results[0].RequestedPath);
        Assert.IsTrue(results[0].Success);
        RepositorySummary? clean = results[0].Summary;
        Assert.IsNotNull(clean);
        Assert.IsFalse(clean.HasChanges);
        Assert.IsNull(results[0].Error);

        Assert.AreEqual("C:/projects/missing", results[1].RequestedPath);
        Assert.IsFalse(results[1].Success);
        Assert.IsNull(results[1].Summary);
        string? inspectionError = results[1].Error;
        Assert.IsNotNull(inspectionError);
        StringAssert.Contains(inspectionError, "localizar");

        Assert.AreEqual("D:/projects/dirty", results[2].RequestedPath);
        Assert.IsTrue(results[2].Success);
        RepositorySummary? dirty = results[2].Summary;
        Assert.IsNotNull(dirty);
        Assert.IsTrue(dirty.HasChanges);
        Assert.AreEqual("feature", dirty.Branch);
        Assert.AreEqual(7, runner.Commands.Count);
        Assert.IsTrue(runner.Commands.All(command => command[0] is "rev-parse" or "symbolic-ref" or "-c"));
    }

    [TestMethod]
    public void Inspect_MissingDirectory_DoesNotPreventInspectionOfFollowingRepository()
    {
        MissingDirectoryRunner runner = new();
        RepositoryInventoryService service = new(new RepositorySummaryService(runner));

        IReadOnlyList<RepositoryInspectionEntry> results = service.Inspect(["C:/missing", "C:/present"]);

        Assert.AreEqual(2, results.Count);
        Assert.IsFalse(results[0].Success);
        Assert.IsNull(results[0].Summary);
        Assert.IsNotNull(results[0].Error);
        Assert.IsTrue(results[1].Success);
        Assert.AreEqual("main", results[1].Summary?.Branch);
    }

    [TestMethod]
    public void Inspect_EmptySelectionAndBlankPath_AreRejected()
    {
        RepositoryInventoryService service = new(new RepositorySummaryService(new RecordingRunner([])));

        Assert.ThrowsExactly<ArgumentException>(() => service.Inspect([]));
        Assert.ThrowsExactly<ArgumentException>(() => service.Inspect(["C:/valid", " "]));
    }

    private sealed class RecordingRunner(IEnumerable<GitCommandResult> results) : IGitCommandRunner
    {
        private readonly Queue<GitCommandResult> _results = new(results);

        public List<string[]> Commands { get; } = [];

        public GitCommandResult Run(string directory, IReadOnlyList<string> arguments)
        {
            Commands.Add(arguments.ToArray());
            return _results.Dequeue();
        }
    }

    private sealed class MissingDirectoryRunner : IGitCommandRunner
    {
        public GitCommandResult Run(string directory, IReadOnlyList<string> arguments)
        {
            if (directory == "C:/missing")
            {
                throw new DirectoryNotFoundException("El repositorio no está disponible.");
            }

            return arguments[0] switch
            {
                "rev-parse" => new GitCommandResult(0, "C:/present\n", ""),
                "symbolic-ref" => new GitCommandResult(0, "main\n", ""),
                "-c" => new GitCommandResult(0, "", ""),
                _ => throw new InvalidOperationException("Comando inesperado.")
            };
        }
    }
}

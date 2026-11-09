using MyWindows.Application.Repositories;

namespace MyWindows.Tests.Repositories;

[TestClass]
public sealed class RepositoryOpenServiceTests
{
    [TestMethod]
    public void Open_ValidRepository_OpensDetectedRootOnlyAfterInspection()
    {
        RecordingRunner runner = new([
            new GitCommandResult(0, "C:/projects/root\n", ""),
            new GitCommandResult(0, "main\n", ""),
            new GitCommandResult(0, "", "")
        ]);
        RecordingOpener opener = new();
        RepositoryOpenService service = new(new RepositorySummaryService(runner), opener);

        string result = service.Open("C:/projects/root/subfolder");

        Assert.AreEqual("C:/projects/root", result);
        Assert.AreEqual("C:/projects/root", opener.LastPath);
        Assert.AreEqual(3, runner.Commands.Count);
    }

    [TestMethod]
    public void Open_NonRepository_DoesNotLaunchExplorer()
    {
        RecordingRunner runner = new([new GitCommandResult(128, "", "not a repository")]);
        RecordingOpener opener = new();
        RepositoryOpenService service = new(new RepositorySummaryService(runner), opener);

        Assert.ThrowsExactly<GitInspectionException>(() => service.Open("C:/not-repository"));
        Assert.IsNull(opener.LastPath);
    }

    [TestMethod]
    public void Open_FailedExplorerLaunch_DoesNotReportSuccess()
    {
        RecordingRunner runner = new([
            new GitCommandResult(0, "C:/projects/root\n", ""),
            new GitCommandResult(0, "main\n", ""),
            new GitCommandResult(0, "", "")
        ]);
        RecordingOpener opener = new() { ShouldFail = true };
        RepositoryOpenService service = new(new RepositorySummaryService(runner), opener);

        Assert.ThrowsExactly<InvalidOperationException>(() => service.Open("C:/projects/root"));
        Assert.AreEqual("C:/projects/root", opener.LastPath);
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

    private sealed class RecordingOpener : IRepositoryFolderOpener
    {
        public string? LastPath { get; private set; }
        public bool ShouldFail { get; init; }

        public void Open(string directory)
        {
            LastPath = directory;
            if (ShouldFail)
            {
                throw new InvalidOperationException("No se ha podido abrir Explorer.");
            }
        }
    }
}

using MyWindows.Application.Repositories;

namespace MyWindows.Tests.Repositories;

[TestClass]
public sealed class RepositorySummaryServiceTests
{
    [TestMethod]
    public void Inspect_CleanBranch_ReturnsRepositorySummaryAndExpectedCommands()
    {
        RecordingRunner runner = new([
            new GitCommandResult(0, "C:/work/project\n", ""),
            new GitCommandResult(0, "main\n", ""),
            new GitCommandResult(0, "", "")
        ]);

        RepositorySummary result = new RepositorySummaryService(runner).Inspect("C:/work/project");

        Assert.AreEqual("C:/work/project", result.RootPath);
        Assert.AreEqual("main", result.Branch);
        Assert.IsFalse(result.IsDetached);
        Assert.IsFalse(result.HasChanges);
        Assert.AreEqual(3, runner.Commands.Count);
        Assert.AreSequenceEqual(new[] { "rev-parse", "--show-toplevel" }, runner.Commands[0]);
        Assert.AreSequenceEqual(new[] { "symbolic-ref", "--quiet", "--short", "HEAD" }, runner.Commands[1]);
        Assert.AreSequenceEqual(new[] { "-c", "core.fsmonitor=false", "status", "--porcelain=v1", "--untracked-files=normal" }, runner.Commands[2]);
    }

    [TestMethod]
    public void Inspect_DetachedHeadWithUntrackedChanges_ReturnsShaAndDirtyState()
    {
        RecordingRunner runner = new([
            new GitCommandResult(0, "D:/checkout\r\n", ""),
            new GitCommandResult(1, "", ""),
            new GitCommandResult(0, "a1b2c3d\n", ""),
            new GitCommandResult(0, "?? new-file.txt\n", "")
        ]);

        RepositorySummary result = new RepositorySummaryService(runner).Inspect("D:/checkout");

        Assert.AreEqual("D:/checkout", result.RootPath);
        Assert.AreEqual("a1b2c3d", result.Branch);
        Assert.IsTrue(result.IsDetached);
        Assert.IsTrue(result.HasChanges);
        Assert.AreSequenceEqual(new[] { "rev-parse", "--short", "HEAD" }, runner.Commands[2]);
    }

    [TestMethod]
    public void Inspect_NotGitRepository_FailsBeforeAdditionalGitCommands()
    {
        RecordingRunner runner = new([new GitCommandResult(128, "", "fatal: not a git repository")]);

        GitInspectionException exception = Assert.ThrowsExactly<GitInspectionException>(
            () => new RepositorySummaryService(runner).Inspect("C:/missing-git"));

        StringAssert.Contains(exception.Message, "localizar");
        Assert.AreEqual(1, runner.Commands.Count);
    }

    [TestMethod]
    public void Inspect_BranchFailureOtherThanDetached_DoesNotHideTheError()
    {
        RecordingRunner runner = new([
            new GitCommandResult(0, "C:/project\n", ""),
            new GitCommandResult(128, "", "fatal")
        ]);

        Assert.ThrowsExactly<GitInspectionException>(() => new RepositorySummaryService(runner).Inspect("C:/project"));
        Assert.AreEqual(2, runner.Commands.Count);
    }

    [TestMethod]
    public void Inspect_EmptyBranchOrRoot_RejectsIncompleteResult()
    {
        RecordingRunner noRoot = new([new GitCommandResult(0, "\n", "")]);
        RecordingRunner noBranch = new([
            new GitCommandResult(0, "C:/project\n", ""),
            new GitCommandResult(0, "", "")
        ]);

        Assert.ThrowsExactly<GitInspectionException>(() => new RepositorySummaryService(noRoot).Inspect("C:/project"));
        Assert.ThrowsExactly<GitInspectionException>(() => new RepositorySummaryService(noBranch).Inspect("C:/project"));
    }

    [TestMethod]
    public void Inspect_StatusFailureDoesNotReportCleanTree()
    {
        RecordingRunner runner = new([
            new GitCommandResult(0, "C:/project\n", ""),
            new GitCommandResult(0, "feature\n", ""),
            new GitCommandResult(128, "", "error")
        ]);

        Assert.ThrowsExactly<GitInspectionException>(() => new RepositorySummaryService(runner).Inspect("C:/project"));
    }

    [TestMethod]
    public void Inspect_WhitespacePathIsRejectedWithoutExecutingGit()
    {
        RecordingRunner runner = new([]);

        Assert.ThrowsExactly<ArgumentException>(() => new RepositorySummaryService(runner).Inspect("  "));
        Assert.IsEmpty(runner.Commands);
    }

    private sealed class RecordingRunner : IGitCommandRunner
    {
        private readonly Queue<GitCommandResult> _results;

        public RecordingRunner(IEnumerable<GitCommandResult> results)
        {
            _results = new Queue<GitCommandResult>(results);
        }

        public List<string[]> Commands { get; } = [];

        public GitCommandResult Run(string directory, IReadOnlyList<string> arguments)
        {
            Commands.Add(arguments.ToArray());
            Assert.IsFalse(string.IsNullOrWhiteSpace(directory));
            return _results.Dequeue();
        }
    }
}

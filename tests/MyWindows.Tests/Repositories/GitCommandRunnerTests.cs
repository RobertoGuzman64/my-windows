using MyWindows.Application.Repositories;
using MyWindows.Infrastructure;

namespace MyWindows.Tests.Repositories;

[TestClass]
public sealed class GitCommandRunnerTests
{
    [TestMethod]
    public void Run_RepositoryWithSpaces_ReturnsBranchAndDirtyStateWithoutChangingGitIndex()
    {
        string root = Path.Combine(Path.GetTempPath(), $"mw-git-test {Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            GitCommandRunner runner = new();
            GitCommandResult initialized = runner.Run(root, ["init", "--initial-branch=main"]);
            Assert.AreEqual(0, initialized.ExitCode);
            File.WriteAllText(Path.Combine(root, "file.txt"), "untracked content");
            string index = Path.Combine(root, ".git", "index");
            byte[]? indexBefore = File.Exists(index) ? File.ReadAllBytes(index) : null;

            RepositorySummary result = new RepositorySummaryService(runner).Inspect(root);

            StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            Assert.IsTrue(string.Equals(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(result.RootPath).TrimEnd(Path.DirectorySeparatorChar), comparison));
            Assert.AreEqual("main", result.Branch);
            Assert.IsFalse(result.IsDetached);
            Assert.IsTrue(result.HasChanges);
            byte[]? indexAfter = File.Exists(index) ? File.ReadAllBytes(index) : null;
            if (indexBefore is null)
            {
                Assert.IsNull(indexAfter);
            }
            else
            {
                Assert.IsNotNull(indexAfter);
                Assert.AreSequenceEqual(indexBefore, indexAfter);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Run_PathThatIsNotARepository_ReturnsNonzeroExitAndNoUnexpectedFilesystemEntries()
    {
        string root = Path.Combine(Path.GetTempPath(), $"mw-not-repo-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            GitCommandRunner runner = new();
            string[] before = Directory.GetFileSystemEntries(root);

            Assert.ThrowsExactly<GitInspectionException>(() => new RepositorySummaryService(runner).Inspect(root));
            Assert.AreSequenceEqual(before, Directory.GetFileSystemEntries(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Run_InventoryWithUnavailableRepository_ReportsPartialResultWithoutChangingGitIndex()
    {
        string root = Path.Combine(Path.GetTempPath(), $"mw-repo-inventory {Guid.NewGuid():N}");
        string clean = Path.Combine(root, "clean repo");
        string dirty = Path.Combine(root, "dirty repo");
        string missing = Path.Combine(root, "missing repo");
        Directory.CreateDirectory(clean);
        Directory.CreateDirectory(dirty);
        try
        {
            GitCommandRunner runner = new();
            Assert.AreEqual(0, runner.Run(clean, ["init", "--initial-branch=main"]).ExitCode);
            Assert.AreEqual(0, runner.Run(dirty, ["init", "--initial-branch=main"]).ExitCode);
            File.WriteAllText(Path.Combine(dirty, "new.txt"), "untracked");
            string cleanIndex = Path.Combine(clean, ".git", "index");
            string dirtyIndex = Path.Combine(dirty, ".git", "index");
            byte[]? cleanBefore = File.Exists(cleanIndex) ? File.ReadAllBytes(cleanIndex) : null;
            byte[]? dirtyBefore = File.Exists(dirtyIndex) ? File.ReadAllBytes(dirtyIndex) : null;

            RepositoryInventoryService service = new(new RepositorySummaryService(runner));
            IReadOnlyList<RepositoryInspectionEntry> results = service.Inspect([clean, missing, dirty]);

            Assert.AreEqual(3, results.Count);
            Assert.IsTrue(results[0].Success);
            RepositorySummary? cleanSummary = results[0].Summary;
            Assert.IsNotNull(cleanSummary);
            Assert.IsFalse(cleanSummary.HasChanges);
            Assert.IsFalse(results[1].Success);
            Assert.IsNotNull(results[1].Error);
            Assert.IsTrue(results[2].Success);
            RepositorySummary? dirtySummary = results[2].Summary;
            Assert.IsNotNull(dirtySummary);
            Assert.IsTrue(dirtySummary.HasChanges);
            Assert.IsFalse(Directory.Exists(missing));
            byte[]? cleanAfter = File.Exists(cleanIndex) ? File.ReadAllBytes(cleanIndex) : null;
            byte[]? dirtyAfter = File.Exists(dirtyIndex) ? File.ReadAllBytes(dirtyIndex) : null;
            if (cleanBefore is null)
            {
                Assert.IsNull(cleanAfter);
            }
            else
            {
                Assert.IsNotNull(cleanAfter);
                Assert.AreSequenceEqual(cleanBefore, cleanAfter);
            }
            if (dirtyBefore is null)
            {
                Assert.IsNull(dirtyAfter);
            }
            else
            {
                Assert.IsNotNull(dirtyAfter);
                Assert.AreSequenceEqual(dirtyBefore, dirtyAfter);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Run_MissingGitWorkspace_DoesNotCreateDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"mw-missing-{Guid.NewGuid():N}");
        Assert.ThrowsExactly<DirectoryNotFoundException>(() => new GitCommandRunner().Run(path, ["status"]));
        Assert.IsFalse(Directory.Exists(path));
    }
}

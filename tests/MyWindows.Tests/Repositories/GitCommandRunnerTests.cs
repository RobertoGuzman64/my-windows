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
    public void Run_MissingGitWorkspace_DoesNotCreateDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"mw-missing-{Guid.NewGuid():N}");
        Assert.ThrowsExactly<DirectoryNotFoundException>(() => new GitCommandRunner().Run(path, ["status"]));
        Assert.IsFalse(Directory.Exists(path));
    }
}

namespace MyWindows.Application.Repositories;

public sealed class RepositorySummaryService
{
    private readonly IGitCommandRunner _runner;

    public RepositorySummaryService(IGitCommandRunner runner)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    public RepositorySummary Inspect(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        GitCommandResult root = Execute(directory, ["rev-parse", "--show-toplevel"], "localizar el repositorio");
        string rootPath = root.StandardOutput.TrimEnd('\r', '\n');
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new GitInspectionException("Git no ha devuelto la ruta del repositorio.");
        }

        GitCommandResult branch = _runner.Run(directory, ["symbolic-ref", "--quiet", "--short", "HEAD"]);
        bool detached = branch.ExitCode == 1;
        string branchName;
        if (branch.ExitCode == 0)
        {
            branchName = branch.StandardOutput.TrimEnd('\r', '\n');
        }
        else if (detached)
        {
            GitCommandResult commit = Execute(directory, ["rev-parse", "--short", "HEAD"], "identificar HEAD separado");
            branchName = commit.StandardOutput.TrimEnd('\r', '\n');
        }
        else
        {
            throw new GitInspectionException("No ha sido posible consultar la rama de Git.");
        }

        if (string.IsNullOrWhiteSpace(branchName))
        {
            throw new GitInspectionException("Git no ha devuelto una rama ni un commit válido.");
        }

        GitCommandResult status = Execute(directory,
            ["-c", "core.fsmonitor=false", "status", "--porcelain=v1", "--untracked-files=normal"],
            "consultar el estado del repositorio");

        return new RepositorySummary(rootPath, branchName, detached, status.StandardOutput.Length != 0);
    }

    private GitCommandResult Execute(string directory, IReadOnlyList<string> arguments, string operation)
    {
        GitCommandResult result = _runner.Run(directory, arguments);
        if (result.ExitCode != 0)
        {
            throw new GitInspectionException($"Git no ha podido {operation} (código {result.ExitCode}).");
        }

        return result;
    }
}

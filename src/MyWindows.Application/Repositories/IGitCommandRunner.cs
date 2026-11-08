namespace MyWindows.Application.Repositories;

public interface IGitCommandRunner
{
    GitCommandResult Run(string directory, IReadOnlyList<string> arguments);
}

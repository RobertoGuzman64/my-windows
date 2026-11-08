using MyWindows.Application.Planning;

namespace MyWindows.Application.Operations;

public interface IRepoShortcutGateway
{
    ShortcutObservation Inspect(string repositoryPath);
    void Create(string repositoryPath);
    void Repair(string repositoryPath);
    void Remove(string repositoryPath);
}

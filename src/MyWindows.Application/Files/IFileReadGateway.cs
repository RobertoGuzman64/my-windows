namespace MyWindows.Application.Files;

public interface IFileReadGateway
{
    IFileReadSession OpenRead(string path);
}

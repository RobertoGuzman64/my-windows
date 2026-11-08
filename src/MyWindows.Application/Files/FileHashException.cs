namespace MyWindows.Application.Files;

public sealed class FileHashException : Exception
{
    public FileHashException(string message) : base(message)
    {
    }
}

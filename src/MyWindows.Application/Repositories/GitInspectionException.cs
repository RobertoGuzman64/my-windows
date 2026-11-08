namespace MyWindows.Application.Repositories;

public sealed class GitInspectionException : Exception
{
    public GitInspectionException(string message) : base(message)
    {
    }

    public GitInspectionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

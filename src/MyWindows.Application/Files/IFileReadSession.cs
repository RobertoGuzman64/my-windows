namespace MyWindows.Application.Files;

public interface IFileReadSession : IDisposable
{
    string FullPath { get; }

    long InitialLength { get; }

    Stream Content { get; }

    void VerifyUnchanged();
}

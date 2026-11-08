using System.Security.Cryptography;

namespace MyWindows.Application.Files;

public sealed class FileHashService
{
    private readonly IFileReadGateway _gateway;

    public FileHashService(IFileReadGateway gateway)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
    }

    public async Task<FileHashResult> ComputeSha256Async(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        using IFileReadSession session = _gateway.OpenRead(path);
        if (!session.Content.CanRead || !session.Content.CanSeek || session.InitialLength < 0)
        {
            throw new FileHashException("El origen no proporciona un archivo legible y verificable.");
        }

        byte[] digest = await SHA256.HashDataAsync(session.Content, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (session.Content.Position != session.InitialLength)
        {
            throw new FileHashException("El tamaño observado durante la lectura no coincide con el inicial.");
        }

        session.VerifyUnchanged();
        return new FileHashResult(session.FullPath, session.InitialLength, Convert.ToHexString(digest));
    }
}

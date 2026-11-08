namespace MyWindows.Application.Files;

public sealed record FileHashResult(string FullPath, long LengthBytes, string Sha256);

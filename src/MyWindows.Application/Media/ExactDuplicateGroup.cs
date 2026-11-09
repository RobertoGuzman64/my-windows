namespace MyWindows.Application.Media;

public sealed record ExactDuplicateGroup(string Sha256, long LengthBytes, IReadOnlyList<string> Paths);

namespace MyWindows.Application.Media;

public sealed record MediaDiscovery(string RootPath, IReadOnlyList<string> FilePaths, IReadOnlyList<MediaScanIssue> Issues);

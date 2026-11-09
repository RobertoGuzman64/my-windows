namespace MyWindows.Application.Media;

public sealed record MediaInventoryResult(
    MediaKind Kind,
    string RootPath,
    IReadOnlyList<MediaFileEntry> Files,
    IReadOnlyList<ExactDuplicateGroup> ExactDuplicates,
    IReadOnlyList<MediaScanIssue> Issues)
{
    public bool IsComplete => Issues.Count == 0;
}

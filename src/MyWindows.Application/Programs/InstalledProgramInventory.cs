namespace MyWindows.Application.Programs;

public sealed record InstalledProgramInventory(
    DateTimeOffset CapturedAtUtc,
    IReadOnlyList<InstalledProgramEntry> Programs,
    IReadOnlyList<InstalledProgramIssue> Issues)
{
    public string Scope { get; } = "windows-uninstall-registry";

    public bool IsComplete => Issues.Count == 0;
}

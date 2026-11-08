namespace MyWindows.Application.Planning;

public sealed record ShortcutObservation(
    string RepositoryPath,
    bool RepositoryExists,
    string? ProgramsDirectory,
    bool ProgramsDirectoryExists,
    string? ShortcutPath,
    bool ShortcutOccupied,
    ShortcutOwnership Ownership = ShortcutOwnership.Unknown);

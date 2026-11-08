using MyWindows.Application.Planning;

namespace MyWindows.Application.Operations;

public sealed record ShortcutOperationResult(
    string Command,
    ShortcutOwnership Ownership,
    string Outcome,
    bool Success,
    PlanResult Plan);

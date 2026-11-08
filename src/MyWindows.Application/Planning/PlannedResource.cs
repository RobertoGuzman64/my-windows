namespace MyWindows.Application.Planning;

public sealed record PlannedResource(
    string ModuleId,
    string ModuleVersion,
    string Scope,
    string ResourcePath,
    string TargetPath,
    PlanOperation Operation,
    string Reason,
    string RequiredPrivilege,
    string Risk);

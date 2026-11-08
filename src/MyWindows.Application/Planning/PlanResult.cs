namespace MyWindows.Application.Planning;

public sealed record PlanResult(IReadOnlyList<PlannedResource> Resources)
{
    public bool IsFeasible => Resources.All(resource => resource.Operation != PlanOperation.Blocked);
}

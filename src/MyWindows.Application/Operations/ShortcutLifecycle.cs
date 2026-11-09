using MyWindows.Application.Planning;
using MyWindows.Domain.Modules;

namespace MyWindows.Application.Operations;

public static class ShortcutLifecycle
{
    public static ShortcutOperationResult Execute(
        ShortcutCommand command,
        IReadOnlyList<ModuleDefinition> modules,
        string repositoryPath,
        IRepoShortcutGateway? gateway)
    {
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        if (modules.Count == 0)
        {
            return new ShortcutOperationResult(command.ToString().ToLowerInvariant(), ShortcutOwnership.Absent,
                "No hay módulos seleccionados; no se han realizado cambios.", command != ShortcutCommand.Verify,
                ShortcutPlanner.Create(modules, null));
        }

        ArgumentNullException.ThrowIfNull(gateway);
        ShortcutObservation before = gateway.Inspect(repositoryPath);
        PlanResult plan = ShortcutPlanner.Create(modules, before);
        ShortcutOwnership ownership = before.Ownership;
        if (command == ShortcutCommand.Status)
        {
            return new ShortcutOperationResult("status", ownership,
                plan.IsFeasible ? "Estado inspeccionado." : plan.Resources[0].Reason,
                plan.IsFeasible, plan);
        }

        if (command == ShortcutCommand.Verify)
        {
            bool verifiedBefore = ownership == ShortcutOwnership.Owned && before.RepositoryExists;
            return new ShortcutOperationResult("verify", ownership,
                verifiedBefore ? "Acceso propio íntegro; destino presente y comprobado." : "No se cumplen las postcondiciones de verificación.",
                verifiedBefore, plan);
        }

        if (command == ShortcutCommand.Remove)
        {
            if (ownership == ShortcutOwnership.Absent)
            {
                return Result("remove", ownership, "Nada propio que retirar.", true, plan);
            }

            if (ownership is not (ShortcutOwnership.Owned or ShortcutOwnership.RecoverableMissing))
            {
                return Result("remove", ownership, "Retirada bloqueada: propiedad no demostrada.", false, plan);
            }

            gateway.Remove(repositoryPath);
            ShortcutOwnership afterRemove = gateway.Inspect(repositoryPath).Ownership;
            return Result("remove", afterRemove,
                afterRemove == ShortcutOwnership.Absent ? "Acceso y recibo propios retirados." : "Retirada incompleta; consultar status.",
                afterRemove == ShortcutOwnership.Absent, plan);
        }

        if (!plan.IsFeasible)
        {
            return Result(command.ToString().ToLowerInvariant(), ownership, plan.Resources[0].Reason, false, plan);
        }

        PlanOperation operation = plan.Resources[0].Operation;
        if (operation == PlanOperation.Create)
        {
            if (command != ShortcutCommand.Apply)
            {
                return Result("repair", ownership, "No existe un acceso propio que reparar; usar apply explícitamente.", false, plan);
            }

            gateway.Create(repositoryPath);
        }
        else if (operation == PlanOperation.Repair)
        {
            if (command != ShortcutCommand.Repair)
            {
                return Result("apply", ownership, "Hay una desviación reparable; usar repair explícitamente.", false, plan);
            }

            gateway.Repair(repositoryPath);
        }

        ShortcutObservation afterObservation = gateway.Inspect(repositoryPath);
        ShortcutOwnership after = afterObservation.Ownership;
        bool verified = after == ShortcutOwnership.Owned && afterObservation.RepositoryExists;
        return Result(command.ToString().ToLowerInvariant(), after,
            verified ? operation == PlanOperation.NoChange ? "Sin cambios (idempotente)." : "Postcondiciones comprobadas." : "La operación no alcanzó el estado esperado.",
            verified, plan);
    }

    private static ShortcutOperationResult Result(string command, ShortcutOwnership ownership, string outcome, bool success, PlanResult plan)
    {
        return new ShortcutOperationResult(command, ownership, outcome, success, plan);
    }
}

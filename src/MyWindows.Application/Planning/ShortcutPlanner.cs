using MyWindows.Domain.Modules;

namespace MyWindows.Application.Planning;

public static class ShortcutPlanner
{
    public static PlanResult Create(IReadOnlyList<ModuleDefinition> modules, ShortcutObservation? observation)
    {
        ArgumentNullException.ThrowIfNull(modules);
        if (modules.Count == 0)
        {
            return new PlanResult(Array.Empty<PlannedResource>());
        }

        if (modules.Count != 1 || modules[0].Id != RepoShortcutModule.Id ||
            modules[0].Version != RepoShortcutModule.Definition.Version || observation is null)
        {
            throw new ArgumentException("El planificador solo admite el módulo de acceso al repositorio.");
        }

        ShortcutOwnership ownership = observation.Ownership == ShortcutOwnership.Unknown
            ? observation.ShortcutOccupied ? ShortcutOwnership.Foreign : ShortcutOwnership.Absent
            : observation.Ownership;

        PlanOperation operation;
        string reason;
        if (!observation.RepositoryExists)
        {
            operation = PlanOperation.Blocked;
            reason = "El repositorio de destino no existe o no es una carpeta accesible.";
        }
        else if (!observation.ProgramsDirectoryExists || string.IsNullOrWhiteSpace(observation.ShortcutPath))
        {
            operation = PlanOperation.Blocked;
            reason = "No se ha podido comprobar la carpeta Programas del menú Inicio del usuario.";
        }
        else
        {
            (operation, reason) = ownership switch
            {
                ShortcutOwnership.Absent => (PlanOperation.Create, "Crear acceso y recibo de propiedad del usuario actual."),
                ShortcutOwnership.Owned => (PlanOperation.NoChange, "Acceso propio íntegro y destino comprobado; sin cambios."),
                ShortcutOwnership.RecoverableMissing => (PlanOperation.Repair, "Existe recibo válido; restaurar únicamente el acceso propio ausente."),
                ShortcutOwnership.Foreign => (PlanOperation.Blocked, "Acceso preexistente sin prueba de propiedad; prohibido sobrescribirlo."),
                ShortcutOwnership.Modified => (PlanOperation.Blocked, "El acceso propio fue alterado; requiere revisión manual antes de actuar."),
                _ => (PlanOperation.Blocked, "Recibo o recurso ambiguo; no se ha demostrado su propiedad.")
            };
        }

        PlannedResource resource = new(
            modules[0].Id,
            modules[0].Version,
            "current-user",
            observation.ShortcutPath ?? "unresolved:current-user-start-menu",
            observation.RepositoryPath,
            operation,
            reason,
            "Usuario actual; escritura comprobada al ejecutar, no durante plan",
            "El acceso y su recibo se mantienen en el perfil del usuario; colisiones y modificaciones bloquean la operación.");

        return new PlanResult(new[] { resource });
    }
}

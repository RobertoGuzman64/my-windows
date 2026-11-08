using MyWindows.Domain.Profiles;

namespace MyWindows.Domain.Modules;

public static class ModuleResolver
{
    public static IReadOnlyList<ModuleDefinition> Resolve(
        ResolvedProfiles profiles,
        IEnumerable<ModuleDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(definitions);

        Dictionary<string, ModuleDefinition> catalog = new(StringComparer.Ordinal);
        foreach (ModuleDefinition definition in definitions)
        {
            if (!IdentifierRules.IsValid(definition.Id) ||
                !Version.TryParse(definition.Version, out _) ||
                definition.Requires is null || definition.Conflicts is null ||
                !catalog.TryAdd(definition.Id, definition))
            {
                throw new ModuleResolutionException($"Contrato de módulo inválido o duplicado: '{definition.Id}'.");
            }

            ValidateReferences(definition.Id, definition.Requires, "requires");
            ValidateReferences(definition.Id, definition.Conflicts, "conflicts");
        }

        HashSet<string> selected = new(profiles.ModuleIds, StringComparer.Ordinal);
        List<ModuleDefinition> resolved = new();
        foreach (string id in selected.OrderBy(id => id, StringComparer.Ordinal))
        {
            if (!catalog.TryGetValue(id, out ModuleDefinition? definition))
            {
                throw new ModuleResolutionException($"El módulo '{id}' no está disponible en esta versión de MyWindows.");
            }

            foreach (string requirement in definition.Requires)
            {
                if (!selected.Contains(requirement))
                {
                    throw new ModuleResolutionException($"El módulo '{id}' requiere '{requirement}', que no está seleccionado.");
                }
            }

            foreach (string conflict in definition.Conflicts)
            {
                if (selected.Contains(conflict))
                {
                    throw new ModuleResolutionException($"Conflicto entre módulos '{id}' y '{conflict}'.");
                }
            }

            resolved.Add(definition);
        }

        return resolved.AsReadOnly();
    }

    private static void ValidateReferences(string owner, IReadOnlyList<string> references, string field)
    {
        HashSet<string> unique = new(StringComparer.Ordinal);
        foreach (string id in references)
        {
            if (!IdentifierRules.IsValid(id) || id == owner || !unique.Add(id))
            {
                throw new ModuleResolutionException($"Referencia inválida en {field} del módulo '{owner}'.");
            }
        }
    }
}

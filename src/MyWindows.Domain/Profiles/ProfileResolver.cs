namespace MyWindows.Domain.Profiles;

public static class ProfileResolver
{
    public static ResolvedProfiles Resolve(
        IEnumerable<ProfileDefinition> definitions,
        IEnumerable<string> roleIds,
        string? machineId)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(roleIds);

        IReadOnlyList<ProfileDefinition> catalog = ProfileCatalog.Validate(definitions);
        Dictionary<string, ProfileDefinition> profiles = catalog.ToDictionary(profile => profile.Id, StringComparer.Ordinal);
        if (!profiles.ContainsKey("base"))
        {
            throw new ProfileValidationException("Falta el perfil obligatorio 'base'.");
        }

        HashSet<string> roles = new(StringComparer.Ordinal);
        foreach (string roleId in roleIds)
        {
            if (!IdentifierRules.IsValid(roleId) || roleId == "base" || !roles.Add(roleId))
            {
                throw new ProfileValidationException($"Rol repetido o inválido: '{roleId}'.");
            }
        }

        if (machineId is not null && (!IdentifierRules.IsValid(machineId) || machineId == "base" || roles.Contains(machineId)))
        {
            throw new ProfileValidationException($"Perfil de equipo repetido o inválido: '{machineId}'.");
        }

        List<string> selected = new() { "base" };
        selected.AddRange(roles.OrderBy(id => id, StringComparer.Ordinal));
        if (machineId is not null)
        {
            selected.Add(machineId);
        }

        HashSet<string> moduleIds = new(StringComparer.Ordinal);
        foreach (string id in selected)
        {
            if (!profiles.TryGetValue(id, out ProfileDefinition? profile))
            {
                throw new ProfileValidationException($"No existe el perfil seleccionado: '{id}'.");
            }

            foreach (string moduleId in profile.Modules)
            {
                moduleIds.Add(moduleId);
            }
        }

        return new ResolvedProfiles(
            selected.AsReadOnly(),
            moduleIds.OrderBy(id => id, StringComparer.Ordinal).ToArray());
    }
}

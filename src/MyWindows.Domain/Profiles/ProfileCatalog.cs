namespace MyWindows.Domain.Profiles;

public static class ProfileCatalog
{
    public static IReadOnlyList<ProfileDefinition> Validate(IEnumerable<ProfileDefinition> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);

        List<ProfileDefinition> ordered = new();
        foreach (ProfileDefinition profile in profiles)
        {
            if (profile is null ||
                profile.SchemaVersion != ProfileJson.CurrentSchemaVersion ||
                !IdentifierRules.IsValid(profile.Id) ||
                profile.Modules is null)
            {
                throw new ProfileValidationException("El catálogo contiene una definición de perfil inválida.");
            }

            HashSet<string> moduleIds = new(StringComparer.Ordinal);
            foreach (string moduleId in profile.Modules)
            {
                if (!IdentifierRules.IsValid(moduleId) || !moduleIds.Add(moduleId))
                {
                    throw new ProfileValidationException($"El perfil '{profile.Id}' contiene un módulo inválido o duplicado.");
                }
            }

            ordered.Add(profile);
        }

        ordered.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
        HashSet<string> identifiers = new(StringComparer.Ordinal);
        foreach (ProfileDefinition profile in ordered)
        {
            if (!identifiers.Add(profile.Id))
            {
                throw new ProfileValidationException($"Perfil duplicado: '{profile.Id}'.");
            }
        }

        return ordered.AsReadOnly();
    }
}

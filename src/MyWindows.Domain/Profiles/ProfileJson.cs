using System.Text.Json;

namespace MyWindows.Domain.Profiles;

public static class ProfileJson
{
    public const int CurrentSchemaVersion = 1;

    public static ProfileDefinition Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        try
        {
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Disallow,
                AllowTrailingCommas = false,
                MaxDepth = 8
            });

            return ParseRoot(document.RootElement);
        }
        catch (JsonException exception)
        {
            throw new ProfileValidationException("El perfil contiene JSON inválido.", exception);
        }
    }

    private static ProfileDefinition ParseRoot(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new ProfileValidationException("El perfil debe ser un objeto JSON.");
        }

        HashSet<string> keys = new(StringComparer.Ordinal);
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (!keys.Add(property.Name))
            {
                throw new ProfileValidationException($"Campo duplicado: '{property.Name}'.");
            }

            if (property.Name is not ("schemaVersion" or "id" or "modules"))
            {
                throw new ProfileValidationException($"Campo desconocido: '{property.Name}'.");
            }
        }

        if (!root.TryGetProperty("schemaVersion", out JsonElement version) ||
            version.ValueKind != JsonValueKind.Number ||
            !version.TryGetInt32(out int schemaVersion) || schemaVersion != CurrentSchemaVersion)
        {
            throw new ProfileValidationException($"Se requiere schemaVersion = {CurrentSchemaVersion}.");
        }

        if (!root.TryGetProperty("id", out JsonElement idElement) ||
            idElement.ValueKind != JsonValueKind.String ||
            !IdentifierRules.IsValid(idElement.GetString()))
        {
            throw new ProfileValidationException("'id' debe ser un identificador válido en minúsculas.");
        }

        if (!root.TryGetProperty("modules", out JsonElement modulesElement) ||
            modulesElement.ValueKind != JsonValueKind.Array)
        {
            throw new ProfileValidationException("'modules' debe ser un array JSON.");
        }

        List<string> modules = new();
        HashSet<string> uniqueModules = new(StringComparer.Ordinal);
        foreach (JsonElement item in modulesElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || !IdentifierRules.IsValid(item.GetString()))
            {
                throw new ProfileValidationException("Cada módulo debe tener un identificador válido.");
            }

            string module = item.GetString()!;
            if (!uniqueModules.Add(module))
            {
                throw new ProfileValidationException($"Módulo duplicado: '{module}'.");
            }

            modules.Add(module);
        }

        return new ProfileDefinition(schemaVersion, idElement.GetString()!, modules.AsReadOnly());
    }
}

using MyWindows.Domain.Profiles;

namespace MyWindows.Infrastructure;

public static class ProfileDirectoryReader
{
    public static IReadOnlyList<ProfileDefinition> Read(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        string fullPath = Path.GetFullPath(directory);
        if (!Directory.Exists(fullPath))
        {
            throw new ProfileValidationException("La carpeta de perfiles no existe.");
        }

        string[] paths = Directory.GetFiles(fullPath, "*.json", SearchOption.TopDirectoryOnly);
        Array.Sort(paths, StringComparer.Ordinal);
        if (paths.Length == 0)
        {
            throw new ProfileValidationException("No hay perfiles JSON en la carpeta indicada.");
        }

        List<ProfileDefinition> profiles = new();
        foreach (string path in paths)
        {
            ProfileDefinition profile = ProfileJson.Parse(File.ReadAllText(path));
            if (!string.Equals(Path.GetFileNameWithoutExtension(path), profile.Id, StringComparison.Ordinal))
            {
                throw new ProfileValidationException($"El nombre del archivo debe coincidir con el id: '{Path.GetFileName(path)}'.");
            }

            profiles.Add(profile);
        }

        return ProfileCatalog.Validate(profiles);
    }
}

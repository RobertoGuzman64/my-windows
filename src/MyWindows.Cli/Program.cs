using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using MyWindows.Application.Files;
using MyWindows.Application.Operations;
using MyWindows.Application.Planning;
using MyWindows.Application.Repositories;
using MyWindows.Domain.Modules;
using MyWindows.Domain.Profiles;
using MyWindows.Infrastructure;

namespace MyWindows.Cli;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 3 && args[0] == "profiles" && args[1] is ("validate" or "list" or "resolve"))
            {
                return RunProfiles(args[1], args[2]);
            }

            if (args.Length == 3 && args[0] == "repo" && args[1] == "inspect")
            {
                return RunRepositoryInspect(args[2]);
            }

            if (args.Length == 3 && args[0] == "file" && args[1] == "hash")
            {
                return RunFileHash(args[2]);
            }

            if (args.Length >= 3 && args[0] is ("plan" or "apply" or "verify" or "status" or "repair" or "remove"))
            {
                return RunAction(args);
            }

            Console.Error.WriteLine("Uso: mywindows profiles <validate|list|resolve> <carpeta> | repo inspect <ruta-repositorio> | file hash <ruta-archivo> | <plan|apply|verify|status|repair|remove> <carpeta-perfiles> <repositorio> [--role <id>]... [--machine <id>]");
            return 2;
        }
        catch (Exception exception) when (exception is ProfileValidationException or ModuleResolutionException or GitInspectionException or FileHashException or OperationCanceledException or IOException or UnauthorizedAccessException or ArgumentException or PlatformNotSupportedException or InvalidOperationException or COMException)
        {
            Console.Error.WriteLine($"FAIL: {exception.Message}");
            return 1;
        }
    }

    private static int RunProfiles(string operation, string directory)
    {
        IReadOnlyList<ProfileDefinition> catalog = ProfileDirectoryReader.Read(directory);
        if (operation == "list")
        {
            foreach (ProfileDefinition profile in catalog)
            {
                Console.WriteLine($"{profile.Id}: {profile.Modules.Count} módulos declarados");
            }
        }
        else if (operation == "resolve")
        {
            ModuleDefinition[] definitions = RegisteredModules();
            HashSet<string> registered = new(definitions.Select(module => module.Id), StringComparer.Ordinal);
            foreach (ProfileDefinition profile in catalog)
            {
                foreach (string moduleId in profile.Modules)
                {
                    if (!registered.Contains(moduleId))
                    {
                        throw new ModuleResolutionException($"El módulo '{moduleId}' del perfil '{profile.Id}' no está registrado.");
                    }
                }
            }

            ResolvedProfiles profiles = ProfileResolver.Resolve(catalog, Array.Empty<string>(), null);
            IReadOnlyList<ModuleDefinition> modules = ModuleResolver.Resolve(profiles, definitions);
            Console.WriteLine($"PASS: {catalog.Count} perfiles con módulos registrados; base resuelto ({modules.Count} módulos).");
        }
        else
        {
            Console.WriteLine($"PASS: {catalog.Count} perfiles válidos (esquema v{ProfileJson.CurrentSchemaVersion}).");
        }

        return 0;
    }

    private static int RunRepositoryInspect(string directory)
    {
        RepositorySummaryService service = new(new GitCommandRunner());
        RepositorySummary summary = service.Inspect(directory);
        Console.WriteLine(JsonSerializer.Serialize(summary, JsonOptions));
        return 0;
    }

    private static int RunFileHash(string path)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
        FileHashResult result = new FileHashService(new LocalFileReadGateway())
            .ComputeSha256Async(path, timeout.Token).GetAwaiter().GetResult();
        Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
        return 0;
    }

    private static ModuleDefinition[] RegisteredModules() => new[] { RepoShortcutModule.Definition };

    private static int RunAction(string[] args)
    {
        List<string> roles = new();
        string? machine = null;
        for (int index = 3; index < args.Length; index += 2)
        {
            if (index + 1 >= args.Length)
            {
                Console.Error.WriteLine("Cada opción necesita un valor.");
                return 2;
            }

            if (args[index] == "--role")
            {
                roles.Add(args[index + 1]);
            }
            else if (args[index] == "--machine" && machine is null)
            {
                machine = args[index + 1];
            }
            else
            {
                Console.Error.WriteLine($"Opción desconocida o repetida: '{args[index]}'.");
                return 2;
            }
        }

        ResolvedProfiles profiles = ProfileResolver.Resolve(ProfileDirectoryReader.Read(args[1]), roles, machine);
        IReadOnlyList<ModuleDefinition> modules = ModuleResolver.Resolve(profiles, RegisteredModules());
        if (args[0] == "plan")
        {
            PlanResult plan = ShortcutPlanner.Create(modules,
                modules.Count == 0 ? null : RepoShortcutGateway.ForCurrentUser().Inspect(args[2]));
            Console.WriteLine(JsonSerializer.Serialize(plan, JsonOptions));
            return plan.IsFeasible ? 0 : 3;
        }

        ShortcutCommand command = args[0] switch
        {
            "apply" => ShortcutCommand.Apply,
            "verify" => ShortcutCommand.Verify,
            "status" => ShortcutCommand.Status,
            "repair" => ShortcutCommand.Repair,
            "remove" => ShortcutCommand.Remove,
            _ => throw new ArgumentException("Comando inválido.")
        };

        ShortcutOperationResult result = ShortcutLifecycle.Execute(command, modules, args[2],
            modules.Count == 0 ? null : RepoShortcutGateway.ForCurrentUser());
        Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
        return result.Success ? 0 : 3;
    }

}

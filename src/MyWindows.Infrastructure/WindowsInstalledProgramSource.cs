using System.Security;
using Microsoft.Win32;
using MyWindows.Application.Programs;

namespace MyWindows.Infrastructure;

public sealed class WindowsInstalledProgramSource : IInstalledProgramSource
{
    private const string UninstallSubKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    public InstalledProgramInventory Read()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("El inventario de programas requiere Windows.");
        }

        List<InstalledProgramEntry> programs = [];
        List<InstalledProgramIssue> issues = [];
        List<(RegistryHive Hive, RegistryView View, string Label)> sources =
        [
            (RegistryHive.CurrentUser, RegistryView.Default, "HKCU"),
            (RegistryHive.LocalMachine, Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32, "HKLM-native")
        ];
        if (Environment.Is64BitOperatingSystem)
        {
            sources.Add((RegistryHive.LocalMachine, RegistryView.Registry32, "HKLM-32"));
        }

        foreach ((RegistryHive hive, RegistryView view, string label) in sources)
        {
            try
            {
                using RegistryKey root = RegistryKey.OpenBaseKey(hive, view);
                using RegistryKey? uninstall = root.OpenSubKey(UninstallSubKey, writable: false);
                if (uninstall is null)
                {
                    continue;
                }

                foreach (string keyName in uninstall.GetSubKeyNames())
                {
                    string source = $"{label}\\{keyName}";
                    try
                    {
                        using RegistryKey? key = uninstall.OpenSubKey(keyName, writable: false);
                        if (key is null)
                        {
                            issues.Add(new InstalledProgramIssue(source, "La entrada desapareció durante la lectura."));
                            continue;
                        }

                        string? name = key.GetValue("DisplayName") as string;
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            continue;
                        }

                        programs.Add(new InstalledProgramEntry(name.Trim(),
                            (key.GetValue("DisplayVersion") as string)?.Trim(),
                            (key.GetValue("Publisher") as string)?.Trim(), source));
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
                    {
                        issues.Add(new InstalledProgramIssue(source, exception.Message));
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
            {
                issues.Add(new InstalledProgramIssue(label, exception.Message));
            }
        }

        return new InstalledProgramInventory(DateTimeOffset.UtcNow, programs, issues);
    }
}

namespace MyWindows.Application.Programs;

public sealed class InstalledProgramInventoryService
{
    private readonly IInstalledProgramSource _source;

    public InstalledProgramInventoryService(IInstalledProgramSource source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public InstalledProgramInventory Inspect()
    {
        InstalledProgramInventory inventory = _source.Read();
        return inventory with
        {
            Programs = inventory.Programs
                .OrderBy(program => program.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(program => program.Version, StringComparer.OrdinalIgnoreCase)
                .ThenBy(program => program.Source, StringComparer.Ordinal)
                .ToArray()
        };
    }
}

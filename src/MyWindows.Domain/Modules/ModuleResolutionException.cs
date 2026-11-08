namespace MyWindows.Domain.Modules;

public sealed class ModuleResolutionException : Exception
{
    public ModuleResolutionException(string message)
        : base(message)
    {
    }
}

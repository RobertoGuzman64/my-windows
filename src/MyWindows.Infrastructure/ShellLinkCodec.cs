using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using System.Text;

namespace MyWindows.Infrastructure;

[SupportedOSPlatform("windows")]
public sealed class ShellLinkCodec : IShortcutCodec
{
    private static readonly Guid ShellLinkClassId = new("00021401-0000-0000-C000-000000000046");

    public byte[] Create(string targetPath, string ownerTag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerTag);
        string tempPath = Path.Combine(Path.GetTempPath(), $"mywindows-{Guid.NewGuid():N}.lnk");
        object instance = Instantiate();
        try
        {
            IShellLinkW shell = (IShellLinkW)instance;
            shell.SetPath(targetPath);
            shell.SetWorkingDirectory(targetPath);
            shell.SetDescription(ownerTag);
            ((IPersistFile)instance).Save(tempPath, true);
            return File.ReadAllBytes(tempPath);
        }
        finally
        {
            Marshal.ReleaseComObject(instance);
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    public ShortcutMetadata Read(string shortcutPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shortcutPath);
        object instance = Instantiate();
        try
        {
            ((IPersistFile)instance).Load(shortcutPath, 0);
            IShellLinkW shell = (IShellLinkW)instance;
            StringBuilder target = new(32768);
            StringBuilder description = new(512);
            shell.GetPath(target, target.Capacity, IntPtr.Zero, 4);
            shell.GetDescription(description, description.Capacity);
            return new ShortcutMetadata(target.ToString(), description.ToString());
        }
        finally
        {
            Marshal.ReleaseComObject(instance);
        }
    }

    private static object Instantiate()
    {
        Type type = Type.GetTypeFromCLSID(ShellLinkClassId)
            ?? throw new InvalidOperationException("La extensión ShellLink de Windows no está disponible.");
        return Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("No se pudo crear el acceso ShellLink de Windows.");
    }

    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int length, IntPtr findData, uint flags);
        void GetIDList(out IntPtr idList);
        void SetIDList(IntPtr idList);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder description, int length);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string description);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int length);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int length);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        void GetHotkey(out ushort hotkey);
        void SetHotkey(ushort hotkey);
        void GetShowCmd(out int showCommand);
        void SetShowCmd(int showCommand);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int length, out int icon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int icon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(IntPtr window, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }
}

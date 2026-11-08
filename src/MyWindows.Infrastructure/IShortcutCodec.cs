namespace MyWindows.Infrastructure;

public interface IShortcutCodec
{
    byte[] Create(string targetPath, string ownerTag);
    ShortcutMetadata Read(string shortcutPath);
}

public sealed record ShortcutMetadata(string TargetPath, string OwnerTag);

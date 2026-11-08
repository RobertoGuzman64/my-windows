namespace MyWindows.Infrastructure;

internal sealed record ShortcutReceipt(
    int SchemaVersion,
    string ModuleId,
    string RepositoryPath,
    string OwnerTag,
    string Sha256,
    string PayloadBase64);

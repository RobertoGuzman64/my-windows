using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MyWindows.Application.Operations;
using MyWindows.Application.Planning;

namespace MyWindows.Infrastructure;

public sealed class RepoShortcutGateway : IRepoShortcutGateway
{
    private const string TagPrefix = "MyWindows:repo-shortcut:";
    private const int ReceiptVersion = 1;
    private const int MaxReceiptBytes = 2_000_000;
    private const int MaxPayloadBytes = 500_000;
    private static readonly JsonSerializerOptions ReceiptJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly ShortcutLocations _locations;
    private readonly IShortcutCodec _codec;

    public RepoShortcutGateway(ShortcutLocations locations, IShortcutCodec codec)
    {
        _locations = locations ?? throw new ArgumentNullException(nameof(locations));
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
    }

    public static RepoShortcutGateway ForCurrentUser()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("El módulo de menú Inicio requiere Windows.");
        }

        return new RepoShortcutGateway(ShortcutLocations.CurrentUser(), new ShellLinkCodec());
    }

    public ShortcutObservation Inspect(string repositoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        string repository = Path.GetFullPath(repositoryPath);
        ShortcutObservation baseObservation = StartMenuShortcutInspector.Inspect(repository, _locations.ProgramsDirectory);
        bool programsAvailable = baseObservation.ProgramsDirectoryExists;
        bool shortcutExists = baseObservation.ShortcutOccupied;
        bool receiptExists = Exists(_locations.ReceiptPath);
        bool stageExists = Exists(StagePath);
        ShortcutOwnership ownership;

        if (!receiptExists)
        {
            ownership = stageExists ? ShortcutOwnership.Invalid :
                shortcutExists ? ShortcutOwnership.Foreign : ShortcutOwnership.Absent;
        }
        else if (IsLink(_locations.ReceiptPath) || (shortcutExists && IsLink(_locations.ShortcutPath)) ||
            (stageExists && IsLink(StagePath)))
        {
            ownership = ShortcutOwnership.Invalid;
        }
        else
        {
            ShortcutReceipt? receipt = ReadValidReceipt(repository);
            if (receipt is null)
            {
                ownership = ShortcutOwnership.Invalid;
            }
            else if (stageExists && (!MatchesHash(StagePath, receipt.Sha256) || shortcutExists))
            {
                ownership = ShortcutOwnership.Invalid;
            }
            else if (!shortcutExists)
            {
                ownership = ShortcutOwnership.RecoverableMissing;
            }
            else if (!MatchesHash(_locations.ShortcutPath, receipt.Sha256) || !MetadataMatches(_locations.ShortcutPath, receipt))
            {
                ownership = ShortcutOwnership.Modified;
            }
            else
            {
                ownership = ShortcutOwnership.Owned;
            }
        }

        return baseObservation with { Ownership = ownership };
    }

    public void Create(string repositoryPath)
    {
        ShortcutObservation observed = Inspect(repositoryPath);
        Require(observed, ShortcutOwnership.Absent);
        if (!observed.RepositoryExists || !observed.ProgramsDirectoryExists)
        {
            throw new InvalidOperationException("La carpeta del repositorio o del menú Inicio no está disponible.");
        }

        string ownerTag = TagPrefix + Guid.NewGuid().ToString("N");
        byte[] payload = _codec.Create(observed.RepositoryPath, ownerTag);
        if (payload.Length is 0 or > MaxPayloadBytes)
        {
            throw new InvalidDataException("El acceso directo generado supera el límite permitido.");
        }

        ShortcutReceipt receipt = new(ReceiptVersion, RepoShortcutModule.Id,
            observed.RepositoryPath, ownerTag, Hash(payload), Convert.ToBase64String(payload));
        Directory.CreateDirectory(_locations.OwnershipDirectory);
        string receiptTemp = Path.Combine(_locations.OwnershipDirectory, $".receipt-{Guid.NewGuid():N}.tmp");
        try
        {
            WriteStage(payload);
            if (!MetadataMatches(StagePath, receipt))
            {
                throw new InvalidDataException("El acceso generado no conserva el destino y el identificador de propiedad.");
            }

            byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(receipt, ReceiptJsonOptions));
            WriteNew(receiptTemp, bytes);
            File.Move(receiptTemp, _locations.ReceiptPath);
            File.Move(StagePath, _locations.ShortcutPath);
        }
        finally
        {
            if (File.Exists(receiptTemp))
            {
                File.Delete(receiptTemp);
            }

            if (!Exists(_locations.ReceiptPath) && File.Exists(StagePath) && MatchesHash(StagePath, receipt.Sha256))
            {
                File.Delete(StagePath);
            }
        }
    }

    public void Repair(string repositoryPath)
    {
        ShortcutObservation observed = Inspect(repositoryPath);
        Require(observed, ShortcutOwnership.RecoverableMissing);
        if (!observed.RepositoryExists || !observed.ProgramsDirectoryExists)
        {
            throw new InvalidOperationException("No se puede reparar sin las carpetas de destino disponibles.");
        }

        ShortcutReceipt receipt = ReadValidReceipt(observed.RepositoryPath)
            ?? throw new InvalidDataException("El recibo de propiedad no es válido.");
        if (!Exists(StagePath))
        {
            WriteStage(Convert.FromBase64String(receipt.PayloadBase64));
        }

        if (!MatchesHash(StagePath, receipt.Sha256) ||
            !MetadataMatches(StagePath, receipt) || Exists(_locations.ShortcutPath))
        {
            throw new InvalidOperationException("La reparación encuentra una colisión o una etapa alterada.");
        }

        File.Move(StagePath, _locations.ShortcutPath);
    }

    public void Remove(string repositoryPath)
    {
        ShortcutObservation observed = Inspect(repositoryPath);
        if (observed.Ownership is not (ShortcutOwnership.Owned or ShortcutOwnership.RecoverableMissing))
        {
            throw new InvalidOperationException("La retirada solo admite recursos con propiedad demostrada.");
        }

        if (observed.Ownership == ShortcutOwnership.Owned)
        {
            Require(Inspect(repositoryPath), ShortcutOwnership.Owned);
            File.Delete(_locations.ShortcutPath);
        }

        ShortcutReceipt receipt = ReadValidReceipt(observed.RepositoryPath)
            ?? throw new InvalidDataException("El recibo ya no es válido.");
        if (Exists(StagePath))
        {
            if (IsLink(StagePath) || !MatchesHash(StagePath, receipt.Sha256))
            {
                throw new InvalidOperationException("La etapa pendiente cambió; retirada bloqueada.");
            }

            File.Delete(StagePath);
        }

        if (Exists(_locations.ShortcutPath))
        {
            throw new InvalidOperationException("El nombre del acceso fue ocupado durante la retirada.");
        }

        File.Delete(_locations.ReceiptPath);
    }

    private string StagePath => Path.Combine(_locations.ProgramsDirectory, ".mywindows-repo-shortcut.pending.lnk");

    private ShortcutReceipt? ReadValidReceipt(string repository)
    {
        try
        {
            FileInfo info = new(_locations.ReceiptPath);
            if (info.Length is <= 0 or > MaxReceiptBytes)
            {
                return null;
            }

            string json = File.ReadAllText(_locations.ReceiptPath);
            using JsonDocument parsed = JsonDocument.Parse(json);
            if (parsed.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            HashSet<string> keys = new(StringComparer.Ordinal);
            foreach (JsonProperty property in parsed.RootElement.EnumerateObject())
            {
                if (!keys.Add(property.Name) || property.Name is not ("schemaVersion" or "moduleId" or "repositoryPath" or "ownerTag" or "sha256" or "payloadBase64"))
                {
                    return null;
                }
            }

            if (keys.Count != 6)
            {
                return null;
            }

            ShortcutReceipt? receipt = JsonSerializer.Deserialize<ShortcutReceipt>(json, ReceiptJsonOptions);
            if (receipt is null || receipt.SchemaVersion != ReceiptVersion ||
                receipt.ModuleId != RepoShortcutModule.Id ||
                receipt.OwnerTag is null || !receipt.OwnerTag.StartsWith(TagPrefix, StringComparison.Ordinal) ||
                !Guid.TryParseExact(receipt.OwnerTag[TagPrefix.Length..], "N", out _) ||
                string.IsNullOrWhiteSpace(receipt.RepositoryPath) ||
                !string.Equals(Path.GetFullPath(receipt.RepositoryPath), repository, StringComparison.OrdinalIgnoreCase) ||
                receipt.PayloadBase64 is null || receipt.PayloadBase64.Length > MaxReceiptBytes ||
                receipt.Sha256 is null || receipt.Sha256.Length != 64)
            {
                return null;
            }

            byte[] payload = Convert.FromBase64String(receipt.PayloadBase64);
            return payload.Length is > 0 and <= MaxPayloadBytes &&
                string.Equals(Hash(payload), receipt.Sha256, StringComparison.Ordinal) ? receipt : null;
        }
        catch (Exception exception) when (exception is JsonException or FormatException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private bool MetadataMatches(string path, ShortcutReceipt receipt)
    {
        try
        {
            ShortcutMetadata metadata = _codec.Read(path);
            return string.Equals(Path.GetFullPath(metadata.TargetPath), receipt.RepositoryPath, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(metadata.OwnerTag, receipt.OwnerTag, StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return false;
        }
    }

    private static void Require(ShortcutObservation observation, ShortcutOwnership expected)
    {
        if (observation.Ownership != expected)
        {
            throw new InvalidOperationException($"Estado cambiado: se esperaba {expected} y se obtuvo {observation.Ownership}.");
        }
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static bool MatchesHash(string path, string expected)
    {
        using FileStream input = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return string.Equals(Convert.ToHexString(SHA256.HashData(input)), expected, StringComparison.Ordinal);
    }

    private static bool Exists(string path)
    {
        try
        {
            _ = File.GetAttributes(path);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    private static bool IsLink(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private void WriteStage(byte[] payload)
    {
        if (Exists(StagePath))
        {
            throw new IOException("Existe un recurso de preparación en el menú Inicio; se requiere revisión manual.");
        }

        WriteNew(StagePath, payload);
    }

    private static void WriteNew(string path, byte[] payload)
    {
        using FileStream output = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        output.Write(payload);
        output.Flush(true);
    }
}

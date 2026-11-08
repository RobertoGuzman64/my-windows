using System.Security.Cryptography;
using System.Text;
using MyWindows.Application.Files;
using MyWindows.Infrastructure;

namespace MyWindows.Tests.Files;

[TestClass]
public sealed class LocalFileReadGatewayTests
{
    [TestMethod]
    public async Task OpenRead_LocalFileWithSpaces_ComputesDigestWithoutWriting()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"mw hash {Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "prueba con espacios.txt");
            byte[] content = Encoding.UTF8.GetBytes("línea de prueba para MyWindows");
            File.WriteAllBytes(path, content);
            DateTime beforeWrite = File.GetLastWriteTimeUtc(path);
            byte[] beforeBytes = File.ReadAllBytes(path);
            string[] beforeEntries = Directory.GetFileSystemEntries(directory);

            FileHashResult result = await new FileHashService(new LocalFileReadGateway()).ComputeSha256Async(path);

            Assert.AreEqual(Path.GetFullPath(path), result.FullPath);
            Assert.AreEqual(content.LongLength, result.LengthBytes);
            Assert.AreEqual(Convert.ToHexString(SHA256.HashData(content)), result.Sha256);
            Assert.AreSequenceEqual(beforeBytes, File.ReadAllBytes(path));
            Assert.AreEqual(beforeWrite, File.GetLastWriteTimeUtc(path));
            Assert.AreSequenceEqual(beforeEntries, Directory.GetFileSystemEntries(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task OpenRead_MultimegabyteFile_ReturnsStreamingDigest()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"mw-hash-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "big.bin");
            byte[] bytes = new byte[4 * 1024 * 1024];
            RandomNumberGenerator.Fill(bytes);
            File.WriteAllBytes(path, bytes);

            FileHashResult result = await new FileHashService(new LocalFileReadGateway()).ComputeSha256Async(path);

            Assert.AreEqual(bytes.LongLength, result.LengthBytes);
            Assert.AreEqual(Convert.ToHexString(SHA256.HashData(bytes)), result.Sha256);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task OpenRead_Directory_IsRejected()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"mw-hash-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            await Assert.ThrowsExactlyAsync<FileHashException>(
                () => new FileHashService(new LocalFileReadGateway()).ComputeSha256Async(directory));
        }
        finally
        {
            Directory.Delete(directory);
        }
    }

    [TestMethod]
    public async Task OpenRead_MissingFile_DoesNotCreateIt()
    {
        string path = Path.Combine(Path.GetTempPath(), $"mw-hash-missing-{Guid.NewGuid():N}");

        await Assert.ThrowsExactlyAsync<FileNotFoundException>(
            () => new FileHashService(new LocalFileReadGateway()).ComputeSha256Async(path));

        Assert.IsFalse(File.Exists(path));
    }

    [TestMethod]
    public void OpenRead_ReparsePoint_IsRejectedWhenSupported()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"mw-link-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string source = Path.Combine(directory, "source.bin");
            string link = Path.Combine(directory, "link.bin");
            File.WriteAllText(source, "source");
            try
            {
                File.CreateSymbolicLink(link, source);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
            {
                Assert.Inconclusive("El entorno no permite crear enlaces simbólicos para esta prueba.");
                return;
            }

            Assert.ThrowsExactly<FileHashException>(() => new LocalFileReadGateway().OpenRead(link));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}

using System.Text;
using MyWindows.Application.Files;
using MyWindows.Application.Media;
using MyWindows.Infrastructure;

namespace MyWindows.Tests.Media;

[TestClass]
public sealed class MediaInventoryServiceTests
{
    [TestMethod]
    public async Task InspectAsync_Music_FindsExactDuplicatesWithoutChangingFiles()
    {
        string directory = CreateDirectory();
        try
        {
            string subdirectory = Path.Combine(directory, "Album");
            Directory.CreateDirectory(subdirectory);
            string original = Path.Combine(directory, "original.MP3");
            string copy = Path.Combine(subdirectory, "copia.flac");
            string different = Path.Combine(directory, "otra.wav");
            string photo = Path.Combine(directory, "imagen.jpg");
            File.WriteAllBytes(original, Encoding.UTF8.GetBytes("contenido idéntico"));
            File.Copy(original, copy);
            File.WriteAllBytes(different, [1, 2, 3]);
            File.WriteAllBytes(photo, [4, 5, 6]);
            DateTime originalWrite = File.GetLastWriteTimeUtc(original);
            byte[] originalBytes = File.ReadAllBytes(original);
            string[] before = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);

            MediaInventoryResult result = await CreateService().InspectAsync(MediaKind.Music, directory);

            Assert.IsTrue(result.IsComplete);
            Assert.AreEqual(3, result.Files.Count);
            Assert.AreEqual(1, result.ExactDuplicates.Count);
            CollectionAssert.AreEquivalent(new[] { original, copy }, result.ExactDuplicates[0].Paths.ToArray());
            Assert.AreEqual(2, result.ExactDuplicates[0].Paths.Count);
            Assert.AreSequenceEqual(originalBytes, File.ReadAllBytes(original));
            Assert.AreEqual(originalWrite, File.GetLastWriteTimeUtc(original));
            CollectionAssert.AreEquivalent(before, Directory.GetFiles(directory, "*", SearchOption.AllDirectories));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task InspectAsync_Photos_SeparatesDuplicateContentFromDifferentFiles()
    {
        string directory = CreateDirectory();
        try
        {
            string jpeg = Path.Combine(directory, "captura.jpg");
            string png = Path.Combine(directory, "misma.PNG");
            string raw = Path.Combine(directory, "original.dng");
            string audio = Path.Combine(directory, "audio.mp3");
            File.WriteAllBytes(jpeg, [1, 2, 3]);
            File.Copy(jpeg, png);
            File.WriteAllBytes(raw, [4, 5, 6]);
            File.Copy(jpeg, audio);

            MediaInventoryResult result = await CreateService().InspectAsync(MediaKind.Photos, directory);

            Assert.IsTrue(result.IsComplete);
            Assert.AreEqual(3, result.Files.Count);
            Assert.AreEqual(1, result.ExactDuplicates.Count);
            CollectionAssert.AreEquivalent(new[] { jpeg, png }, result.ExactDuplicates[0].Paths.ToArray());
            Assert.IsFalse(result.Files.Any(file => file.FullPath == audio));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task InspectAsync_DisappearingFile_ReturnsPartialResultAndReportsIssue()
    {
        string directory = CreateDirectory();
        try
        {
            string present = Path.Combine(directory, "presente.mp3");
            string missing = Path.Combine(directory, "ausente.mp3");
            File.WriteAllBytes(present, [1, 2, 3]);
            MediaDiscovery discovery = new(directory, [missing, present], []);
            MediaInventoryService service = new(new FixedCatalog(discovery), new FileHashService(new LocalFileReadGateway()));

            MediaInventoryResult result = await service.InspectAsync(MediaKind.Music, directory);

            Assert.IsFalse(result.IsComplete);
            Assert.AreEqual(1, result.Files.Count);
            Assert.AreEqual(present, result.Files[0].FullPath);
            Assert.AreEqual(1, result.Issues.Count);
            Assert.AreEqual(missing, result.Issues[0].Path);
            Assert.AreEqual(0, result.ExactDuplicates.Count);
            Assert.IsFalse(File.Exists(missing));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task InspectAsync_CancellationBeforeDiscovery_DoesNotReadDirectory()
    {
        FixedCatalog catalog = new(new MediaDiscovery("unused", [], []));
        MediaInventoryService service = new(catalog, new FileHashService(new LocalFileReadGateway()));
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => service.InspectAsync(MediaKind.Photos, "test", cancellation.Token));

        Assert.IsFalse(catalog.Called);
    }

    [TestMethod]
    public async Task InspectAsync_EmptyFolder_ReturnsCompleteEmptyInventory()
    {
        string directory = CreateDirectory();
        try
        {
            MediaInventoryResult result = await CreateService().InspectAsync(MediaKind.Photos, directory);

            Assert.IsTrue(result.IsComplete);
            Assert.AreEqual(0, result.Files.Count);
            Assert.AreEqual(0, result.ExactDuplicates.Count);
            Assert.AreEqual(0, result.Issues.Count);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task InspectAsync_UnsupportedKind_DoesNotDiscoverFiles()
    {
        FixedCatalog catalog = new(new MediaDiscovery("unused", [], []));
        MediaInventoryService service = new(catalog, new FileHashService(new LocalFileReadGateway()));

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(
            () => service.InspectAsync((MediaKind)999, "test"));

        Assert.IsFalse(catalog.Called);
    }

    [TestMethod]
    public void Discover_RejectsSymbolicLinkRoot_WhenSupported()
    {
        string directory = CreateDirectory();
        try
        {
            string real = Path.Combine(directory, "real");
            string link = Path.Combine(directory, "link");
            Directory.CreateDirectory(real);
            try
            {
                Directory.CreateSymbolicLink(link, real);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                Assert.Inconclusive("El entorno no permite crear enlaces de carpeta para la prueba.");
                return;
            }

            Assert.ThrowsExactly<ArgumentException>(() => new LocalMediaFileCatalog().Discover(link));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void Discover_OmitsNestedLinkAndReportsIncompleteInventory_WhenSupported()
    {
        string directory = CreateDirectory();
        try
        {
            string external = Path.Combine(directory, "external");
            string selected = Path.Combine(directory, "selected");
            Directory.CreateDirectory(external);
            Directory.CreateDirectory(selected);
            string externalPhoto = Path.Combine(external, "outside.jpg");
            File.WriteAllBytes(externalPhoto, [1, 2, 3]);
            string link = Path.Combine(selected, "linked");
            try
            {
                Directory.CreateSymbolicLink(link, external);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                Assert.Inconclusive("El entorno no permite crear enlaces de carpeta para la prueba.");
                return;
            }

            MediaDiscovery discovery = new LocalMediaFileCatalog().Discover(selected);

            Assert.AreEqual(0, discovery.FilePaths.Count);
            Assert.AreEqual(1, discovery.Issues.Count);
            Assert.AreEqual(link, discovery.Issues[0].Path);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"mw-media-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static MediaInventoryService CreateService() =>
        new(new LocalMediaFileCatalog(), new FileHashService(new LocalFileReadGateway()));

    private sealed class FixedCatalog(MediaDiscovery discovery) : IMediaFileCatalog
    {
        public bool Called { get; private set; }

        public MediaDiscovery Discover(string rootPath, CancellationToken cancellationToken = default)
        {
            Called = true;
            return discovery;
        }
    }
}

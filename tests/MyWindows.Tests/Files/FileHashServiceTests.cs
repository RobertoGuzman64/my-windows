using System.Security.Cryptography;
using System.Text;
using MyWindows.Application.Files;

namespace MyWindows.Tests.Files;

[TestClass]
public sealed class FileHashServiceTests
{
    [TestMethod]
    public async Task ComputeSha256Async_KnownContent_ReturnsFullSha256AndLength()
    {
        FakeGateway gateway = new(Encoding.UTF8.GetBytes("abc"));

        FileHashResult result = await new FileHashService(gateway).ComputeSha256Async("sample.txt");

        Assert.AreEqual("sample.txt", gateway.RequestedPath);
        Assert.AreEqual("sample.txt", result.FullPath);
        Assert.AreEqual(3L, result.LengthBytes);
        Assert.AreEqual("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", result.Sha256);
        Assert.IsTrue(gateway.LastSession!.Verified);
        Assert.IsTrue(gateway.LastSession.Disposed);
    }

    [TestMethod]
    public async Task ComputeSha256Async_EmptyFile_IsSupported()
    {
        FakeGateway gateway = new([]);
        FileHashResult result = await new FileHashService(gateway).ComputeSha256Async("empty.txt");

        Assert.AreEqual(0L, result.LengthBytes);
        Assert.AreEqual(Convert.ToHexString(SHA256.HashData(Array.Empty<byte>())), result.Sha256);
    }

    [TestMethod]
    public async Task ComputeSha256Async_DeclaredSizeDiffers_RejectsResultAndDisposes()
    {
        FakeGateway gateway = new(Encoding.UTF8.GetBytes("abc"), declaredLength: 4);

        await Assert.ThrowsExactlyAsync<FileHashException>(
            () => new FileHashService(gateway).ComputeSha256Async("changed.txt"));

        Assert.IsFalse(gateway.LastSession!.Verified);
        Assert.IsTrue(gateway.LastSession.Disposed);
    }

    [TestMethod]
    public async Task ComputeSha256Async_VerificationFails_DoesNotReturnDigest()
    {
        FakeGateway gateway = new([1, 2, 3], verificationFails: true);

        await Assert.ThrowsExactlyAsync<FileHashException>(
            () => new FileHashService(gateway).ComputeSha256Async("changed.txt"));

        Assert.IsTrue(gateway.LastSession!.Disposed);
    }

    [TestMethod]
    public async Task ComputeSha256Async_CancelledBeforeOpening_DoesNotAccessSource()
    {
        FakeGateway gateway = new([1, 2, 3]);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => new FileHashService(gateway).ComputeSha256Async("data.bin", cancellation.Token));

        Assert.IsNull(gateway.RequestedPath);
    }

    [TestMethod]
    public async Task ComputeSha256Async_CancelledDuringRead_DisposesSession()
    {
        FakeGateway gateway = new([1, 2, 3], blockedRead: true);
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(100));

        bool cancelled = false;
        try
        {
            await new FileHashService(gateway).ComputeSha256Async("data.bin", cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        Assert.IsTrue(cancelled);
        Assert.IsTrue(gateway.LastSession!.Disposed);
    }

    [TestMethod]
    public async Task ComputeSha256Async_EmptyPath_IsRejectedWithoutOpening()
    {
        FakeGateway gateway = new([]);

        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => new FileHashService(gateway).ComputeSha256Async("  "));

        Assert.IsNull(gateway.RequestedPath);
    }

    private sealed class FakeGateway : IFileReadGateway
    {
        private readonly byte[] _content;
        private readonly long? _declaredLength;
        private readonly bool _verificationFails;
        private readonly bool _blockedRead;

        public FakeGateway(byte[] content, long? declaredLength = null, bool verificationFails = false, bool blockedRead = false)
        {
            _content = content;
            _declaredLength = declaredLength;
            _verificationFails = verificationFails;
            _blockedRead = blockedRead;
        }

        public string? RequestedPath { get; private set; }

        public FakeSession? LastSession { get; private set; }

        public IFileReadSession OpenRead(string path)
        {
            RequestedPath = path;
            LastSession = new FakeSession(path, _content, _declaredLength ?? _content.Length, _verificationFails, _blockedRead);
            return LastSession;
        }
    }

    private sealed class FakeSession : IFileReadSession
    {
        private readonly bool _verificationFails;
        private readonly Stream _content;

        public FakeSession(string path, byte[] data, long length, bool verificationFails, bool blockedRead)
        {
            FullPath = path;
            InitialLength = length;
            _verificationFails = verificationFails;
            _content = blockedRead ? new BlockingStream(data) : new MemoryStream(data, writable: false);
        }

        public string FullPath { get; }

        public long InitialLength { get; }

        public Stream Content => _content;

        public bool Verified { get; private set; }

        public bool Disposed { get; private set; }

        public void VerifyUnchanged()
        {
            Verified = true;
            if (_verificationFails)
            {
                throw new FileHashException("Archivo alterado.");
            }
        }

        public void Dispose()
        {
            _content.Dispose();
            Disposed = true;
        }
    }

    private sealed class BlockingStream : MemoryStream
    {
        public BlockingStream(byte[] content) : base(content, writable: false)
        {
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
    }
}

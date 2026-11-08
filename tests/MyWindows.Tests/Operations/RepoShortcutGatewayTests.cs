using System.Text;
using MyWindows.Application.Operations;
using MyWindows.Application.Planning;
using MyWindows.Infrastructure;

namespace MyWindows.Tests.Operations;

[TestClass]
public sealed class RepoShortcutGatewayTests
{
    [TestMethod]
    public void CreateRepairAndRemove_ProtectsOwnResourceAndLeavesNoArtifacts()
    {
        using TestWorkspace workspace = new();
        RepoShortcutGateway gateway = workspace.Gateway;
        Assert.AreEqual(ShortcutOwnership.Absent, gateway.Inspect(workspace.Repository).Ownership);

        Assert.IsTrue(workspace.Run(ShortcutCommand.Apply).Success);
        Assert.IsTrue(workspace.Run(ShortcutCommand.Apply).Success);
        Assert.IsTrue(workspace.Run(ShortcutCommand.Verify).Success);
        Assert.AreEqual(ShortcutOwnership.Owned, gateway.Inspect(workspace.Repository).Ownership);

        File.Delete(workspace.Shortcut);
        Assert.AreEqual(ShortcutOwnership.RecoverableMissing, gateway.Inspect(workspace.Repository).Ownership);
        Assert.IsFalse(workspace.Run(ShortcutCommand.Apply).Success);
        Assert.IsTrue(workspace.Run(ShortcutCommand.Repair).Success);
        Assert.IsTrue(workspace.Run(ShortcutCommand.Verify).Success);
        Assert.IsTrue(workspace.Run(ShortcutCommand.Remove).Success);
        Assert.IsFalse(File.Exists(workspace.Shortcut));
        Assert.IsFalse(File.Exists(workspace.Receipt));
        Assert.IsTrue(workspace.Run(ShortcutCommand.Remove).Success);
    }

    [TestMethod]
    public void ExistingForeignShortcut_IsNeverClaimedOrDeleted()
    {
        using TestWorkspace workspace = new();
        byte[] original = Encoding.UTF8.GetBytes("tercero");
        File.WriteAllBytes(workspace.Shortcut, original);

        Assert.AreEqual(ShortcutOwnership.Foreign, workspace.Gateway.Inspect(workspace.Repository).Ownership);
        Assert.IsFalse(workspace.Run(ShortcutCommand.Apply).Success);
        Assert.IsFalse(workspace.Run(ShortcutCommand.Remove).Success);
        Assert.AreSequenceEqual(original, File.ReadAllBytes(workspace.Shortcut));
        Assert.IsFalse(File.Exists(workspace.Receipt));
    }

    [TestMethod]
    public void ModifiedShortcut_IsBlockedWithoutOverwritingOrDeleting()
    {
        using TestWorkspace workspace = new();
        Assert.IsTrue(workspace.Run(ShortcutCommand.Apply).Success);
        File.WriteAllText(workspace.Shortcut, "modificado externamente");

        Assert.AreEqual(ShortcutOwnership.Modified, workspace.Gateway.Inspect(workspace.Repository).Ownership);
        Assert.IsFalse(workspace.Run(ShortcutCommand.Repair).Success);
        Assert.IsFalse(workspace.Run(ShortcutCommand.Remove).Success);
        Assert.AreEqual("modificado externamente", File.ReadAllText(workspace.Shortcut));
        Assert.IsTrue(File.Exists(workspace.Receipt));
    }

    [TestMethod]
    public void MissingOrInvalidReceipt_DoesNotAuthorizeDeletion()
    {
        using TestWorkspace workspace = new();
        Assert.IsTrue(workspace.Run(ShortcutCommand.Apply).Success);
        File.WriteAllText(workspace.Receipt, "contenido corrupto");
        Assert.AreEqual(ShortcutOwnership.Invalid, workspace.Gateway.Inspect(workspace.Repository).Ownership);
        Assert.IsFalse(workspace.Run(ShortcutCommand.Remove).Success);
        File.Delete(workspace.Receipt);
        Assert.AreEqual(ShortcutOwnership.Foreign, workspace.Gateway.Inspect(workspace.Repository).Ownership);
        Assert.IsFalse(workspace.Run(ShortcutCommand.Remove).Success);
    }

    [TestMethod]
    public void CodecFailure_DoesNotCreateOwnershipOrShortcut()
    {
        using TestWorkspace workspace = new(new DeniedCodec());
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => workspace.Gateway.Create(workspace.Repository));
        Assert.IsFalse(File.Exists(workspace.Shortcut));
        Assert.IsFalse(File.Exists(workspace.Receipt));
    }

    [TestMethod]
    public void InterruptedCreateAfterReceipt_CanBeRepaired()
    {
        using TestWorkspace workspace = new();
        Assert.IsTrue(workspace.Run(ShortcutCommand.Apply).Success);
        byte[] original = File.ReadAllBytes(workspace.Shortcut);
        File.Move(workspace.Shortcut, workspace.Stage);

        Assert.AreEqual(ShortcutOwnership.RecoverableMissing, workspace.Gateway.Inspect(workspace.Repository).Ownership);
        Assert.IsTrue(workspace.Run(ShortcutCommand.Repair).Success);
        Assert.AreSequenceEqual(original, File.ReadAllBytes(workspace.Shortcut));
        Assert.IsFalse(File.Exists(workspace.Stage));
    }

    [TestMethod]
    public void MaliciousPendingStage_BlocksInstallationWithoutRemovingIt()
    {
        using TestWorkspace workspace = new();
        File.WriteAllText(workspace.Stage, "archivo no propio");
        Assert.AreEqual(ShortcutOwnership.Invalid, workspace.Gateway.Inspect(workspace.Repository).Ownership);
        Assert.IsFalse(workspace.Run(ShortcutCommand.Apply).Success);
        Assert.AreEqual("archivo no propio", File.ReadAllText(workspace.Stage));
    }

    private sealed class TestWorkspace : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"mw-f3-{Guid.NewGuid():N}");
        private readonly ShortcutLocations _locations;

        public TestWorkspace(IShortcutCodec? codec = null)
        {
            string programs = Path.Combine(_root, "programs");
            string ownership = Path.Combine(_root, "ownership");
            Repository = Path.Combine(_root, "repository");
            Directory.CreateDirectory(programs);
            Directory.CreateDirectory(Repository);
            _locations = new ShortcutLocations(programs, ownership);
            Gateway = new RepoShortcutGateway(_locations, codec ?? new FakeCodec());
        }

        public string Repository { get; }
        public RepoShortcutGateway Gateway { get; }
        public string Shortcut => _locations.ShortcutPath;
        public string Receipt => _locations.ReceiptPath;
        public string Stage => Path.Combine(_locations.ProgramsDirectory, ".mywindows-repo-shortcut.pending.lnk");

        public ShortcutOperationResult Run(ShortcutCommand command) =>
            ShortcutLifecycle.Execute(command, new[] { RepoShortcutModule.Definition }, Repository, Gateway);

        public void Dispose() => Directory.Delete(_root, true);
    }

    private sealed class FakeCodec : IShortcutCodec
    {
        public byte[] Create(string targetPath, string ownerTag) => Encoding.UTF8.GetBytes(targetPath + "|" + ownerTag);

        public ShortcutMetadata Read(string shortcutPath)
        {
            string[] parts = File.ReadAllText(shortcutPath).Split('|', 2);
            if (parts.Length != 2)
            {
                throw new InvalidDataException("Acceso corrupto.");
            }

            return new ShortcutMetadata(parts[0], parts[1]);
        }
    }

    private sealed class DeniedCodec : IShortcutCodec
    {
        public byte[] Create(string targetPath, string ownerTag) => throw new UnauthorizedAccessException("Acceso denegado.");
        public ShortcutMetadata Read(string shortcutPath) => throw new UnauthorizedAccessException("Acceso denegado.");
    }
}

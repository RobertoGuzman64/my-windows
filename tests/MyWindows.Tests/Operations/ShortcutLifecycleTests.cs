using MyWindows.Application.Operations;
using MyWindows.Application.Planning;

namespace MyWindows.Tests.Operations;

[TestClass]
public sealed class ShortcutLifecycleTests
{
    [TestMethod]
    public void Execute_ApplyTwiceAndVerify_RemainsIdempotent()
    {
        Gateway gateway = new(ShortcutOwnership.Absent);
        ShortcutOperationResult first = Run(ShortcutCommand.Apply, gateway);
        ShortcutOperationResult second = Run(ShortcutCommand.Apply, gateway);
        ShortcutOperationResult verified = Run(ShortcutCommand.Verify, gateway);

        Assert.IsTrue(first.Success);
        Assert.IsTrue(second.Success);
        Assert.IsTrue(verified.Success);
        Assert.AreEqual(1, gateway.Created);
        Assert.AreEqual(PlanOperation.NoChange, second.Plan.Resources[0].Operation);
    }

    [TestMethod]
    public void Execute_RepairRequiresExplicitCommand()
    {
        Gateway gateway = new(ShortcutOwnership.RecoverableMissing);
        ShortcutOperationResult apply = Run(ShortcutCommand.Apply, gateway);
        ShortcutOperationResult repaired = Run(ShortcutCommand.Repair, gateway);

        Assert.IsFalse(apply.Success);
        Assert.IsTrue(repaired.Success);
        Assert.AreEqual(1, gateway.Repaired);
    }

    [TestMethod]
    public void Execute_NeverRemovesForeignOrModifiedResource()
    {
        foreach (ShortcutOwnership ownership in new[] { ShortcutOwnership.Foreign, ShortcutOwnership.Modified, ShortcutOwnership.Invalid })
        {
            Gateway gateway = new(ownership);
            Assert.IsFalse(Run(ShortcutCommand.Remove, gateway).Success);
            Assert.IsFalse(Run(ShortcutCommand.Apply, gateway).Success);
            Assert.AreEqual(0, gateway.Removed);
            Assert.AreEqual(0, gateway.Created);
        }
    }

    [TestMethod]
    public void Execute_RemoveOwnedAndMissingReceiptLeavesAbsent()
    {
        foreach (ShortcutOwnership ownership in new[] { ShortcutOwnership.Owned, ShortcutOwnership.RecoverableMissing })
        {
            Gateway gateway = new(ownership);
            Assert.IsTrue(Run(ShortcutCommand.Remove, gateway).Success);
            Assert.AreEqual(ShortcutOwnership.Absent, gateway.State);
            Assert.AreEqual(1, gateway.Removed);
            Assert.IsTrue(Run(ShortcutCommand.Remove, gateway).Success);
            Assert.AreEqual(1, gateway.Removed);
        }
    }

    private static ShortcutOperationResult Run(ShortcutCommand command, Gateway gateway)
    {
        return ShortcutLifecycle.Execute(command, new[] { RepoShortcutModule.Definition }, @"C:\workspace", gateway);
    }

    private sealed class Gateway(ShortcutOwnership initial) : IRepoShortcutGateway
    {
        public ShortcutOwnership State { get; private set; } = initial;
        public int Created { get; private set; }
        public int Repaired { get; private set; }
        public int Removed { get; private set; }

        public ShortcutObservation Inspect(string repositoryPath)
        {
            return new ShortcutObservation(repositoryPath, true, @"C:\programs", true,
                @"C:\programs\shortcut.lnk", State is ShortcutOwnership.Owned or ShortcutOwnership.Foreign or ShortcutOwnership.Modified,
                State);
        }

        public void Create(string repositoryPath)
        {
            Created++;
            State = ShortcutOwnership.Owned;
        }

        public void Repair(string repositoryPath)
        {
            Repaired++;
            State = ShortcutOwnership.Owned;
        }

        public void Remove(string repositoryPath)
        {
            Removed++;
            State = ShortcutOwnership.Absent;
        }
    }
}

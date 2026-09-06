using System;
using System.Collections.Generic;
using TouristTransfer;

internal sealed class FakeContext : ITransferContext<string>
{
    internal readonly HashSet<string> Source = new HashSet<string> { "A", "B", "C", "Pilot" };
    internal readonly HashSet<string> Destination = new HashSet<string>();
    internal readonly HashSet<string> Eligible = new HashSet<string> { "A", "B", "C" };
    internal readonly List<string> Notifications = new List<string>();
    internal int Capacity = 4;
    internal string Reject;
    internal string ThrowOnAdd;
    internal bool PartialAdd;
    internal bool ThrowAfterRemove;
    internal bool RejectRestore;
    internal bool ThrowNotify;
    internal Action<string> OnNotify;
    public bool IsEligible(string crew) { return Eligible.Contains(crew); }
    public bool HasSpace { get { return Destination.Count < Capacity; } }
    public bool AtSource(string crew) { return Source.Contains(crew); }
    public bool AtDestination(string crew) { return Destination.Contains(crew); }
    public void RemoveSource(string crew)
    {
        Source.Remove(crew);
        if (ThrowAfterRemove) throw new Exception("remove exception");
    }
    public bool AddDestination(string crew)
    {
        if (crew == Reject) return false;
        if (crew == ThrowOnAdd)
        {
            if (PartialAdd) Destination.Add(crew);
            throw new Exception("add exception");
        }
        return Destination.Add(crew);
    }
    public void RemoveDestination(string crew) { Destination.Remove(crew); }
    public bool RestoreSource(string crew) { return !RejectRestore && Source.Add(crew); }
    public void Notify(string crew)
    {
        Notifications.Add(crew);
        if (OnNotify != null) OnNotify(crew);
        if (ThrowNotify) throw new Exception("listener exception");
    }
}

internal static class Tests
{
    private static int passed;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        passed++;
        Console.WriteLine("PASS: " + name);
    }

    public static int Main()
    {
        try
        {
            var context = new FakeContext();
            var result = TransferBatch.Run(new[] { "A", "B", "C" }, context);
            Check(result.Moved == 3 && result.Remaining == 0 && result.Error == null && context.Source.SetEquals(new[] { "Pilot" }), "whole contract transfers, ordinary crew stays");
            context = new FakeContext { Capacity = 2 };
            result = TransferBatch.Run(new[] { "C", "A", "B" }, context);
            Check(result.Moved == 2 && result.Remaining == 1 && context.Source.Contains("B") && string.Join(",", context.Notifications) == "C,A", "partial capacity preserves candidate order and remainder");
            context = new FakeContext { Capacity = 0 };
            result = TransferBatch.Run(new[] { "A" }, context);
            Check(result.Moved == 0 && context.Source.Contains("A") && context.Notifications.Count == 0, "full destination makes no mutations");
            context = new FakeContext { Capacity = 2 };
            context.Destination.Add("Existing");
            result = TransferBatch.Run(new[] { "A", "B" }, context);
            Check(result.Moved == 1 && context.Source.Contains("B") && context.Destination.Contains("Existing"), "existing occupants count against capacity");
            context = new FakeContext();
            result = TransferBatch.Run(new[] { "A", "A", "Pilot", "Unknown" }, context);
            Check(result.Moved == 1 && context.Notifications.Count == 1 && context.Source.Contains("Pilot"), "duplicate membership and ineligible crew do not transfer twice");
            context = new FakeContext();
            context.Source.Remove("B");
            result = TransferBatch.Run(new[] { "B", "C" }, context);
            Check(result.Moved == 1 && context.Destination.Contains("C"), "stale source membership is skipped");
            context = new FakeContext();
            context.OnNotify = delegate { context.Eligible.Clear(); };
            result = TransferBatch.Run(new[] { "A", "B" }, context);
            Check(result.Moved == 1 && context.Source.Contains("B"), "contract or vessel eligibility rechecked between tourists");
            context = new FakeContext();
            context.OnNotify = delegate { context.Capacity = 1; };
            result = TransferBatch.Run(new[] { "A", "B" }, context);
            Check(result.Moved == 1 && context.Source.Contains("B"), "capacity rechecked after callbacks");
            context = new FakeContext { Reject = "B" };
            result = TransferBatch.Run(new[] { "A", "B", "C" }, context);
            Check(result.Moved == 1 && result.Error != null && context.Source.Contains("B") && context.Source.Contains("C") && context.Notifications.Count == 1, "rejected add restores tourist and stops batch");
            context = new FakeContext { ThrowOnAdd = "A" };
            result = TransferBatch.Run(new[] { "A", "B" }, context);
            Check(result.Moved == 0 && result.Error != null && context.Source.Contains("A") && context.Destination.Count == 0, "exception before add restores tourist");
            context = new FakeContext { ThrowOnAdd = "A", PartialAdd = true };
            result = TransferBatch.Run(new[] { "A" }, context);
            Check(result.Moved == 0 && context.Source.Contains("A") && !context.Destination.Contains("A"), "exception after partial add removes duplicate before restoration");
            context = new FakeContext { ThrowAfterRemove = true };
            result = TransferBatch.Run(new[] { "A" }, context);
            Check(result.Moved == 0 && context.Source.Contains("A") && result.Error != null, "exception after removal restores tourist");
            context = new FakeContext { Reject = "A", RejectRestore = true };
            result = TransferBatch.Run(new[] { "A", "B" }, context);
            Check(result.Moved == 0 && result.Error.Contains("reload") && context.Source.Contains("B"), "failed restoration reports recovery requirement and stops");
            context = new FakeContext { ThrowNotify = true };
            result = TransferBatch.Run(new[] { "A", "B" }, context);
            Check(result.Moved == 1 && result.Remaining == 1 && context.Destination.Contains("A") && !context.Source.Contains("A") && context.Source.Contains("B"), "listener failure keeps committed move and accurate count");
            context = new FakeContext();
            result = TransferBatch.Run(new string[0], context);
            Check(result.Moved == 0 && result.Remaining == 0 && result.Error == null, "empty selection is a no-op");
            Console.WriteLine(passed + " tests passed.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}

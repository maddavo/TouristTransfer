// Minimal in-memory API models for testing the REAL adapters' rules.
// These do not model Unity lifetimes, IVA, persistence or ModuleManager behavior.
using System;
using System.Collections.Generic;
using System.Linq;
using TouristTransfer;

public class ProtoCrewMember
{
    public enum KerbalType { Crew, Tourist }
    public string name;
    public KerbalType type;
}
public class Part
{
    public Vessel vessel;
    public int CrewCapacity = 4;
    public bool crewTransferAvailable = true;
    public List<ProtoCrewMember> protoModuleCrew = new List<ProtoCrewMember>();
    public void RemoveCrewmember(ProtoCrewMember crew) { protoModuleCrew.Remove(crew); }
    public bool AddCrewmember(ProtoCrewMember crew)
    {
        if (protoModuleCrew.Count >= CrewCapacity || protoModuleCrew.Contains(crew)) return false;
        protoModuleCrew.Add(crew);
        return true;
    }
}
public class Vessel
{
    public bool loaded = true;
    public bool packed;
    public bool isEVA;
    public List<Part> parts = new List<Part>();
    public static int Modified;
    public static void CrewWasModified(Vessel source, Vessel destination) { Modified++; }
}
public static class HighLogic { public static bool LoadedSceneIsFlight = true; }
public static class FlightGlobals { public static Vessel ActiveVessel; }
public static class GameEvents
{
    public sealed class HostedFromToAction<T, P>
    {
        public T Crew;
        public P Source;
        public P Destination;
        public HostedFromToAction(T crew, P source, P destination) { Crew = crew; Source = source; Destination = destination; }
    }
    public sealed class CrewEvent
    {
        public List<HostedFromToAction<ProtoCrewMember, Part>> Fired = new List<HostedFromToAction<ProtoCrewMember, Part>>();
        public void Fire(HostedFromToAction<ProtoCrewMember, Part> action) { Fired.Add(action); }
    }
    public static CrewEvent onCrewTransferred = new CrewEvent();
}
namespace Contracts
{
    public class ContractSystem
    {
        public static ContractSystem Instance;
        public List<FinePrint.Contracts.TourismContract> Active = new List<FinePrint.Contracts.TourismContract>();
        public T[] GetCurrentActiveContracts<T>() { return Active.OfType<T>().ToArray(); }
    }
}
namespace FinePrint.Contracts
{
    public class TourismContract
    {
        public List<string> Tourists = new List<string>();
        public Guid ContractGuid = Guid.NewGuid();
        public string Title = "Same title";
    }
}
internal sealed class NonStockContract : FinePrint.Contracts.TourismContract { }

internal static class AdapterTests
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
            var vessel = new Vessel();
            FlightGlobals.ActiveVessel = vessel;
            var source = new Part { vessel = vessel };
            var target = new Part { vessel = vessel };
            vessel.parts.AddRange(new[] { source, target });
            var a = new ProtoCrewMember { name = "A", type = ProtoCrewMember.KerbalType.Tourist };
            var b = new ProtoCrewMember { name = "B", type = ProtoCrewMember.KerbalType.Tourist };
            var pilot = new ProtoCrewMember { name = "Pilot", type = ProtoCrewMember.KerbalType.Crew };
            source.protoModuleCrew.AddRange(new[] { a, b, pilot });
            Contracts.ContractSystem.Instance = new Contracts.ContractSystem();
            var contract = new FinePrint.Contracts.TourismContract();
            contract.Tourists.AddRange(new[] { "A", "Pilot", "Elsewhere" });
            var second = new FinePrint.Contracts.TourismContract();
            second.Tourists.Add("B");
            Contracts.ContractSystem.Instance.Active.AddRange(new[] { contract, second });
            var groups = StockContracts.Read(source);
            Check(groups.Count == 2 && groups[0].Crew.SequenceEqual(new[] { a }), "mapping limits groups to actual tourist occupants");
            Check(groups[0].Title == groups[1].Title && groups[0].Id != groups[1].Id, "identical contract titles keep distinct GUIDs");
            contract.Tourists.Clear();
            contract.Tourists.Add("a");
            Check(StockContracts.Read(source).Count == 1, "roster name matching is exact and case sensitive");
            contract.Tourists.Add("A");
            var nonStock = new NonStockContract();
            nonStock.Tourists.Add("A");
            Contracts.ContractSystem.Instance.Active.Add(nonStock);
            Check(StockContracts.Read(source).Count == 2, "non-stock subclasses are excluded");
            var selection = new HashSet<string> { groups[0].Key(a), groups[1].Key(b) };
            var context = new KspTransferContext(source, target, selection);
            Check(context.IsEligible(a) && !context.IsEligible(pilot), "adapter only permits selected active stock tourists");
            Contracts.ContractSystem.Instance.Active.Remove(contract);
            Check(!context.IsEligible(a), "expired/completed/removed contract invalidates selection");
            var replacement = new FinePrint.Contracts.TourismContract();
            replacement.Tourists.Add("A");
            Contracts.ContractSystem.Instance.Active.Add(replacement);
            Check(!context.IsEligible(a), "new contract for same tourist does not inherit old selection");
            Contracts.ContractSystem.Instance.Active.Remove(replacement);
            Contracts.ContractSystem.Instance.Active.Add(contract);
            Check(!KspTransferContext.ValidPair(source, source), "same-part transfer rejected");
            var another = new Vessel();
            target.vessel = another;
            Check(!KspTransferContext.ValidPair(source, target), "undocked destination rejected");
            target.vessel = vessel;
            target.crewTransferAvailable = false;
            Check(!context.IsEligible(a), "destination transfer restriction honored");
            target.crewTransferAvailable = true;
            source.crewTransferAvailable = false;
            Check(!context.IsEligible(a), "source transfer restriction honored");
            source.crewTransferAvailable = true;
            vessel.parts.Remove(target);
            Check(!context.IsEligible(a), "removed destination rejected");
            vessel.parts.Add(target);
            vessel.packed = true;
            Check(!context.IsEligible(a), "packed vessel rejected");
            vessel.packed = false;
            FlightGlobals.ActiveVessel = another;
            Check(!context.IsEligible(a), "vessel switch rejected");
            FlightGlobals.ActiveVessel = vessel;
            target.CrewCapacity = 1;
            var result = TransferBatch.Run(new[] { a, b }, context);
            Check(result.Moved == 1 && source.protoModuleCrew.Contains(b) && target.protoModuleCrew.Contains(a), "real adapter and transaction cooperate on partial transfer");
            Check(GameEvents.onCrewTransferred.Fired.Count == 1 && GameEvents.onCrewTransferred.Fired[0].Crew == a
                && GameEvents.onCrewTransferred.Fired[0].Source == source && GameEvents.onCrewTransferred.Fired[0].Destination == target
                && Vessel.Modified == 1, "notification contains correct crew and endpoints");
            Contracts.ContractSystem.Instance = null;
            Check(StockContracts.Read(source).Count == 0 && !context.IsEligible(b), "missing contract system fails closed");
            Console.WriteLine(passed + " adapter tests passed (simulated KSP models).");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}

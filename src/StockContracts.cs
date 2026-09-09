using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Contracts;
using FinePrint.Contracts;

namespace TouristTransfer
{
    internal sealed class TouristGroup
    {
        internal string Id;
        internal string Title;
        internal List<ProtoCrewMember> Crew;
        internal bool IsCompleted;
        internal string Key(ProtoCrewMember crew) { return Id + ":" + crew.name; }
    }

    internal static class StockContracts
    {
        internal static List<TouristGroup> Read(Part source)
        {
            var groups = new List<TouristGroup>();
            if (source == null || ContractSystem.Instance == null) return groups;
            var seen = new HashSet<Guid>();
            foreach (var contract in ContractSystem.Instance.GetCurrentContracts<TourismContract>())
            {
                seen.Add(contract.ContractGuid);
                var names = new HashSet<string>(contract.Tourists ?? new List<string>(), StringComparer.Ordinal);
                var crew = source.protoModuleCrew.Where(c => c.type == ProtoCrewMember.KerbalType.Tourist && names.Contains(c.name)).ToList();
                if (crew.Count > 0)
                    groups.Add(new TouristGroup { Id = contract.ContractGuid.ToString(), Title = contract.Title, Crew = crew, IsCompleted = false });
            }
            foreach (var contract in ContractSystem.Instance.GetCompletedContracts<TourismContract>())
            {
                if (!seen.Add(contract.ContractGuid)) continue;
                var names = new HashSet<string>(contract.Tourists ?? new List<string>(), StringComparer.Ordinal);
                var crew = source.protoModuleCrew.Where(c => c.type == ProtoCrewMember.KerbalType.Tourist && names.Contains(c.name)).ToList();
                if (crew.Count > 0)
                    groups.Add(new TouristGroup { Id = contract.ContractGuid.ToString(), Title = contract.Title + " (completed)", Crew = crew, IsCompleted = true });
            }
            ReadContractConfigurator(source, groups);
            ResolveDuplicateMembership(groups);
            return groups;
        }

        private static void ResolveDuplicateMembership(List<TouristGroup> groups)
        {
            // A roster entry has one identity even if stale Contract Configurator
            // passenger records name it in several contracts. Current contracts win.
            // When all candidates are completed, the later contract returned by KSP
            // wins; completed contracts are maintained in chronological order.
            var winners = new Dictionary<string, TouristGroup>(StringComparer.Ordinal);
            foreach (var group in groups)
                foreach (var crew in group.Crew)
                {
                    TouristGroup winner;
                    if (!winners.TryGetValue(crew.name, out winner)
                        || (!group.IsCompleted && winner.IsCompleted)
                        || group.IsCompleted == winner.IsCompleted)
                        winners[crew.name] = group;
                }
            foreach (var group in groups)
                group.Crew = group.Crew.Where(c => winners[c.name] == group).ToList();
            groups.RemoveAll(g => g.Crew.Count == 0);
        }

        private static void ReadContractConfigurator(Part source, List<TouristGroup> groups)
        {
            // Optional support: avoid a hard dependency so stock-only installations still load.
            var type = Type.GetType("ContractConfigurator.ConfiguredContract, ContractConfigurator");
            if (type == null) return;
            foreach (string propertyName in new[] { "ActiveContracts", "CompletedContracts" })
            {
                var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
                var contracts = property == null ? null : property.GetValue(null, null) as System.Collections.IEnumerable;
                if (contracts == null) continue;
                foreach (object contract in contracts)
                {
                  try
                  {
                    var nameSet = new HashSet<string>(StringComparer.Ordinal);
                    var contractType = contract.GetType();
                    var namesMethod = contractType.GetMethod("KerbalNames", BindingFlags.Public | BindingFlags.Instance);
                    var names = namesMethod == null ? null : namesMethod.Invoke(contract, null) as System.Collections.IEnumerable;
                    if (names != null) foreach (object name in names) if (name != null) nameSet.Add(name.ToString());
                    // KerbalNames can omit passengers until a behaviour has finished loading.
                    // SpawnPassengers keeps the authoritative ProtoCrewMember dictionary.
                    var behavioursProperty = contractType.GetProperty("Behaviours", BindingFlags.Public | BindingFlags.Instance);
                    var behaviours = behavioursProperty == null ? null : behavioursProperty.GetValue(contract, null) as System.Collections.IEnumerable;
                    if (behaviours != null)
                        foreach (object behaviour in behaviours)
                        {
                            if (behaviour == null || behaviour.GetType().FullName.IndexOf("SpawnPassengers", StringComparison.OrdinalIgnoreCase) < 0) continue;
                            var passengers = behaviour.GetType().GetField("passengers", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                            var dictionary = passengers == null ? null : passengers.GetValue(behaviour) as System.Collections.IDictionary;
                            if (dictionary == null) continue;
                            foreach (System.Collections.DictionaryEntry entry in dictionary)
                            {
                                var crewMember = entry.Key as ProtoCrewMember;
                                if (crewMember != null) nameSet.Add(crewMember.name);
                            }
                        }
                    if (nameSet.Count == 0) continue;
                    var crew = source.protoModuleCrew.Where(c => c.type == ProtoCrewMember.KerbalType.Tourist && nameSet.Contains(c.name)).ToList();
                    if (crew.Count == 0) continue;
                    var hashMethod = contractType.GetMethod("GetHashString", BindingFlags.Public | BindingFlags.Instance);
                    var id = hashMethod == null ? contract.GetHashCode().ToString() : Convert.ToString(hashMethod.Invoke(contract, null));
                    var title = ReadDisplayTitle(contract, contractType, id);
                    var isCompleted = propertyName == "CompletedContracts";
                    if (isCompleted) title += " (completed)";
                    groups.Add(new TouristGroup { Id = "CC:" + id, Title = title, Crew = crew, IsCompleted = isCompleted });
                  }
                  catch (Exception) { /* Optional adapter must fail closed if CC changes its API. */ }
                }
            }
        }

        private static string ReadDisplayTitle(object contract, Type contractType, string id)
        {
            // KSP exposes the user-facing title as the inherited Title property.
            var titleProperty = contractType.GetProperty("Title", BindingFlags.Public | BindingFlags.Instance);
            var title = titleProperty == null ? null : Convert.ToString(titleProperty.GetValue(contract, null));
            if (!String.IsNullOrEmpty(title)) return title;
            var titleMethod = contractType.GetMethod("GetTitle", BindingFlags.Public | BindingFlags.Instance);
            title = titleMethod == null ? null : Convert.ToString(titleMethod.Invoke(contract, null));
            if (!String.IsNullOrEmpty(title)) return title;
            var subtype = contractType.GetProperty("subType", BindingFlags.Public | BindingFlags.Instance);
            title = subtype == null ? null : Convert.ToString(subtype.GetValue(contract, null));
            return String.IsNullOrEmpty(title) ? "Contract Configurator contract " + id : title;
        }
    }
}

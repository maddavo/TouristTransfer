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
        internal string Key(ProtoCrewMember crew) { return Id + ":" + crew.name; }
    }

    internal static class StockContracts
    {
        internal static List<TouristGroup> Read(Part source)
        {
            var groups = new List<TouristGroup>();
            if (source == null || ContractSystem.Instance == null) return groups;
            foreach (var contract in ContractSystem.Instance.GetCurrentActiveContracts<TourismContract>())
            {
                var names = new HashSet<string>(contract.Tourists ?? new List<string>(), StringComparer.Ordinal);
                var crew = source.protoModuleCrew.Where(c => c.type == ProtoCrewMember.KerbalType.Tourist && names.Contains(c.name)).ToList();
                if (crew.Count > 0)
                    groups.Add(new TouristGroup { Id = contract.ContractGuid.ToString(), Title = contract.Title, Crew = crew });
            }
            ReadContractConfigurator(source, groups);
            return groups;
        }

        private static void ReadContractConfigurator(Part source, List<TouristGroup> groups)
        {
            // Optional support: avoid a hard dependency so stock-only installations still load.
            var type = Type.GetType("ContractConfigurator.ConfiguredContract, ContractConfigurator");
            if (type == null) return;
            var active = type.GetProperty("ActiveContracts", BindingFlags.Public | BindingFlags.Static);
            if (active == null) return;
            var contracts = active.GetValue(null, null) as System.Collections.IEnumerable;
            if (contracts == null) return;
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
                    var titleMethod = contractType.GetMethod("GetTitle", BindingFlags.Public | BindingFlags.Instance);
                    var hashMethod = contractType.GetMethod("GetHashString", BindingFlags.Public | BindingFlags.Instance);
                    var id = hashMethod == null ? contract.GetHashCode().ToString() : Convert.ToString(hashMethod.Invoke(contract, null));
                    var title = titleMethod == null ? null : Convert.ToString(titleMethod.Invoke(contract, null));
                    if (String.IsNullOrEmpty(title)) title = "Contract Configurator contract " + id;
                    groups.Add(new TouristGroup { Id = "CC:" + id, Title = title, Crew = crew });
                }
                catch (Exception) { /* Optional adapter must fail closed if CC changes its API. */ }
            }
        }
    }
}

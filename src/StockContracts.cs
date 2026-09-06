using System;
using System.Collections.Generic;
using System.Linq;
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
                // Only stock contracts are supported; do not infer membership from titles.
                if (contract.GetType() != typeof(TourismContract)) continue;
                var names = new HashSet<string>(contract.Tourists ?? new List<string>(), StringComparer.Ordinal);
                var crew = source.protoModuleCrew.Where(c => c.type == ProtoCrewMember.KerbalType.Tourist && names.Contains(c.name)).ToList();
                if (crew.Count > 0)
                    groups.Add(new TouristGroup { Id = contract.ContractGuid.ToString(), Title = contract.Title, Crew = crew });
            }
            return groups;
        }
    }
}

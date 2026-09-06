using System;
using System.Collections.Generic;
using System.Linq;

namespace TouristTransfer
{
    internal sealed class KspTransferContext : ITransferContext<ProtoCrewMember>
    {
        private readonly Part source;
        private readonly Part destination;
        private readonly HashSet<string> selection;

        internal KspTransferContext(Part source, Part destination, HashSet<string> selection)
        {
            this.source = source;
            this.destination = destination;
            this.selection = new HashSet<string>(selection, StringComparer.Ordinal);
        }

        internal static bool ValidSource(Part part)
        {
            return HighLogic.LoadedSceneIsFlight && part != null && part.vessel != null
                && part.vessel == FlightGlobals.ActiveVessel && part.vessel.loaded && !part.vessel.packed
                && !part.vessel.isEVA && part.vessel.parts.Contains(part)
                && part.CrewCapacity > 0 && part.crewTransferAvailable;
        }

        internal static bool ValidPair(Part source, Part destination)
        {
            return ValidSource(source) && destination != source && ValidSource(destination)
                && source.vessel == destination.vessel;
        }

        public bool IsEligible(ProtoCrewMember crew)
        {
            return ValidPair(source, destination) && crew != null
                && crew.type == ProtoCrewMember.KerbalType.Tourist
                && StockContracts.Read(source).Any(g => g.Crew.Contains(crew) && selection.Contains(g.Key(crew)));
        }

        public bool HasSpace { get { return ValidPair(source, destination) && destination.protoModuleCrew.Count < destination.CrewCapacity; } }
        public bool AtSource(ProtoCrewMember crew) { return source != null && source.protoModuleCrew.Contains(crew); }
        public bool AtDestination(ProtoCrewMember crew) { return destination != null && destination.protoModuleCrew.Contains(crew); }
        public void RemoveSource(ProtoCrewMember crew) { source.RemoveCrewmember(crew); }
        public bool AddDestination(ProtoCrewMember crew) { return destination.AddCrewmember(crew); }
        public void RemoveDestination(ProtoCrewMember crew) { destination.RemoveCrewmember(crew); }
        public bool RestoreSource(ProtoCrewMember crew) { return source.AddCrewmember(crew); }
        public void Notify(ProtoCrewMember crew)
        {
            try
            {
                GameEvents.onCrewTransferred.Fire(new GameEvents.HostedFromToAction<ProtoCrewMember, Part>(crew, source, destination));
            }
            finally
            {
                Vessel.CrewWasModified(source.vessel, destination.vessel);
            }
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TouristTransfer
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed class TouristTransferWindow : MonoBehaviour
    {
        internal static TouristTransferWindow Instance;
        private const string LockName = "TouristTransfer.Window";
        private Part source;
        private Part destination;
        private Vessel openedVessel;
        private Vessel refreshVessel;
        private List<TouristGroup> groups = new List<TouristGroup>();
        private List<Part> destinations = new List<Part>();
        private List<ProtoCrewMember> otherCrew = new List<ProtoCrewMember>();
        private readonly HashSet<string> selected = new HashSet<string>(StringComparer.Ordinal);
        private Rect window = new Rect(100, 100, 460, 720);
        private GUISkin windowSkin;
        private Vector2 crewScroll;
        private Vector2 destinationScroll;
        private float nextRefresh;
        private string message = "Choose a contract or individual tourists, then a destination.";
        private bool requested;
        private bool busy;
        private bool hidden;
        private bool pickDestination;

        public void Awake()
        {
            Instance = this;
            windowSkin = Instantiate(HighLogic.Skin);
            ReduceFont(windowSkin.label);
            ReduceFont(windowSkin.button);
            ReduceFont(windowSkin.toggle);
            ReduceFont(windowSkin.window);
            ReduceFont(windowSkin.box);
            foreach (GUIStyle style in windowSkin.customStyles) ReduceFont(style);
            GameEvents.onHideUI.Add(Hide);
            GameEvents.onShowUI.Add(Show);
            GameEvents.onPartActionUIShown.Add(OnPartActionUIShown);
        }

        internal void Open(Part part)
        {
            if (busy || !KspTransferContext.ValidSource(part))
            {
                ScreenMessages.PostScreenMessage("Tourist Transfer requires an unpacked active vessel and a transfer-enabled compartment.", 5f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }
            source = part;
            openedVessel = part.vessel;
            destination = null;
            selected.Clear();
            requested = false;
            message = "Choose a contract or individual tourists, then a destination.";
            nextRefresh = 0;
            Refresh();
        }

        private void Hide() { hidden = true; Unlock(); }
        private void Show() { hidden = false; }
        private void Unlock() { InputLockManager.RemoveControlLock(LockName); }
        private void Close()
        {
            source = null;
            destination = null;
            openedVessel = null;
            selected.Clear();
            requested = false;
            Unlock();
        }

        public void OnDisable()
        {
            Close();
            // If teardown interrupts the deferred refresh, do not leave crew despawned.
            if (refreshVessel != null && refreshVessel.loaded) refreshVessel.SpawnCrew();
            refreshVessel = null;
        }

        public void OnDestroy()
        {
            GameEvents.onHideUI.Remove(Hide);
            GameEvents.onShowUI.Remove(Show);
            GameEvents.onPartActionUIShown.Remove(OnPartActionUIShown);
            Unlock();
            if (windowSkin != null) Destroy(windowSkin);
            if (Instance == this) Instance = null;
        }

        private static void ReduceFont(GUIStyle style)
        {
            if (style != null && style.fontSize > 0) style.fontSize = Math.Max(10, style.fontSize - 2);
        }

        private void OnPartActionUIShown(UIPartActionWindow ui, Part clickedPart)
        {
            if (!pickDestination || source == null || !KspTransferContext.ValidPair(source, clickedPart)) return;
            destination = clickedPart;
            pickDestination = false;
            message = "Destination selected from the ship view: " + PartLabel(destination);
        }

        public void Update()
        {
            if (source == null) { Unlock(); return; }
            if (!KspTransferContext.ValidSource(source) || source.vessel != openedVessel)
            {
                Close();
                return;
            }
            if (requested && !busy)
            {
                requested = false;
                Execute();
            }
            if (Time.unscaledTime >= nextRefresh && !busy)
            {
                Refresh();
                nextRefresh = Time.unscaledTime + 0.5f;
            }
            var mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            if (!hidden && window.Contains(mouse))
                InputLockManager.SetControlLock(ControlTypes.ALL_SHIP_CONTROLS | ControlTypes.CAMERACONTROLS | ControlTypes.TWEAKABLES, LockName);
            else Unlock();
        }

        private void Refresh()
        {
            groups = StockContracts.Read(source);
            selected.IntersectWith(groups.SelectMany(g => g.Crew.Select(g.Key)));
            var assigned = new HashSet<ProtoCrewMember>(groups.SelectMany(g => g.Crew));
            otherCrew = source.protoModuleCrew.Where(c => !assigned.Contains(c)).ToList();
            destinations = source.vessel.parts.Where(p => KspTransferContext.ValidPair(source, p)).ToList();
            if (!destinations.Contains(destination)) destination = null;
        }

        private static string PartLabel(Part part)
        {
            return (part.partInfo == null ? part.name : part.partInfo.title) + " [" + part.flightID + "]";
        }

        public void OnGUI()
        {
            if (source == null || hidden) return;
            var previousSkin = GUI.skin;
            GUI.skin = windowSkin == null ? HighLogic.Skin : windowSkin;
            window.width = Mathf.Min(460, Screen.width);
            window.height = Mathf.Min(720, Screen.height);
            window.x = Mathf.Clamp(window.x, 0, Mathf.Max(0, Screen.width - window.width));
            window.y = Mathf.Clamp(window.y, 0, Mathf.Max(0, Screen.height - window.height));
            window = GUILayout.Window(GetInstanceID(), window, DrawWindow, "Tourist Transfer - stock contracts");
            GUI.skin = previousSkin;
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Source: " + PartLabel(source));
            bool close = GUILayout.Button("Close", GUILayout.Width(65));
            GUILayout.EndHorizontal();
            GUILayout.Label("Select tourists by active or completed contract:");
            crewScroll = GUILayout.BeginScrollView(crewScroll, GUILayout.Height(390));
            foreach (TouristGroup group in groups)
            {
                int count = group.Crew.Count(c => selected.Contains(group.Key(c)));
                bool all = count == group.Crew.Count;
                bool toggle = GUILayout.Toggle(all, group.Title + " (" + count + "/" + group.Crew.Count + " selected)");
                if (toggle != all)
                    foreach (ProtoCrewMember crew in group.Crew)
                        if (toggle) selected.Add(group.Key(crew)); else selected.Remove(group.Key(crew));
                foreach (ProtoCrewMember crew in group.Crew)
                {
                    string key = group.Key(crew);
                    if (GUILayout.Toggle(selected.Contains(key), "    " + crew.name)) selected.Add(key); else selected.Remove(key);
                }
            }
            if (groups.Count == 0) GUILayout.Label("No tourists from active or completed tourism contracts in this compartment.");
            if (otherCrew.Count > 0) GUILayout.Label("Other crew / no active tourism contract (not selectable):");
            foreach (ProtoCrewMember crew in otherCrew) GUILayout.Label("    " + crew.name + " - " + crew.trait);
            GUILayout.EndScrollView();
            GUILayout.Label("Destination (module IDs distinguish identical parts):");
            if (GUILayout.Button(pickDestination ? "Pick a destination in the ship view..." : "Pick destination from ship view"))
            {
                pickDestination = true;
                message = "Right-click the destination part in the ship view, then its row will be selected here.";
            }
            destinationScroll = GUILayout.BeginScrollView(destinationScroll, GUILayout.Height(180));
            foreach (Part part in destinations)
            {
                int free = Math.Max(0, part.CrewCapacity - part.protoModuleCrew.Count);
                if (GUILayout.Toggle(destination == part, PartLabel(part) + " - " + free + " free seats")) destination = part;
            }
            if (destinations.Count == 0) GUILayout.Label("No other transfer-enabled compartment on this vessel.");
            GUILayout.EndScrollView();
            int selectedCount = groups.SelectMany(g => g.Crew.Where(c => selected.Contains(g.Key(c)))).Distinct().Count();
            int seats = destination == null ? 0 : Math.Max(0, destination.CrewCapacity - destination.protoModuleCrew.Count);
            GUILayout.Label(selectedCount + " selected; " + seats + " free seats; up to " + Math.Min(selectedCount, seats) + " will move.");
            GUILayout.Label(message);
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && !busy && !requested && selectedCount > 0 && seats > 0
                && KspTransferContext.ValidPair(source, destination) && PartItemTransfer.Instance == null;
            if (GUILayout.Button("Transfer selected")) requested = true;
            GUI.enabled = previousEnabled;
            if (PartItemTransfer.Instance != null) GUILayout.Label("Finish or cancel the stock transfer first.");
            GUI.DragWindow(new Rect(0, 0, window.width, 22));
            if (close) Close();
        }

        private void Execute()
        {
            if (!KspTransferContext.ValidPair(source, destination) || PartItemTransfer.Instance != null)
            {
                message = "Transfer cancelled: vessel, destination or stock transfer state changed.";
                return;
            }
            busy = true;
            try
            {
                var result = TransferBatch.Run(source.protoModuleCrew.Where(c =>
                    groups.Any(g => g.Crew.Contains(c) && selected.Contains(g.Key(c)))).ToArray(),
                    new KspTransferContext(source, destination, selected));
                message = "Moved " + result.Moved + "; " + result.Remaining + " selected tourists not moved.";
                if (result.Error != null)
                {
                    message += " " + result.Error;
                    Debug.LogError("[TouristTransfer] " + message);
                }
                else Debug.Log("[TouristTransfer] " + message);
                // Also refresh after a rejected transfer: rollback may have changed seat indices.
                refreshVessel = openedVessel;
                refreshVessel.DespawnCrew();
                StartCoroutine(RefreshPortraits());
            }
            catch (Exception error)
            {
                message = "Transfer stopped: " + error.Message;
                Debug.LogException(error);
                if (refreshVessel != null) refreshVessel.SpawnCrew();
                refreshVessel = null;
                busy = false;
            }
        }

        private IEnumerator RefreshPortraits()
        {
            yield return null;
            try
            {
                if (refreshVessel != null && refreshVessel.loaded) refreshVessel.SpawnCrew();
            }
            finally
            {
                refreshVessel = null;
                busy = false;
            }
        }
    }
}

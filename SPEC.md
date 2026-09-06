# TouristTransfer specification

## Initial scope

KSP 1.12.5 flight scene. A part-action-window button opens a stock-contract-aware
crew transfer window for one source part on the active vessel. ModuleManager
adds the button module to all parts with positive CrewCapacity, including parts
on existing craft after game reload.

## Contract membership and selection

- Query `ContractSystem.Instance.GetCurrentActiveContracts<TourismContract>()`.
- Accept exact stock TourismContract instances; match `Tourists` by exact roster
  name and require `ProtoCrewMember.KerbalType.Tourist`.
- Use contract GUID plus roster name as selection identity; titles are display
  text, never membership keys. Duplicate titles remain separate contracts.
- Display only source occupants in contract groups. Display all other source
  crew in a non-selectable section. Select/deselect a whole group or individuals.
- A tourist selected under multiple contracts is moved at most once.
- Refresh displayed membership/capacity every 0.5 seconds. Remove stale selection
  keys, and independently revalidate each tourist at execution time.

## Transfer rules

1. Require two different, extant, transfer-enabled crew parts on the same active,
   loaded, unpacked, non-EVA vessel. Docked vessels naturally share a Vessel.
2. Do not start while a stock PartItemTransfer is active.
3. Snapshot selected source occupants in source crew-list order. Recheck active
   contract membership, vessel/part validity and free seats before every move.
4. Stop at capacity; no swaps or displacement. Skip stale/ineligible occupants.
5. Remove from source, add to destination and verify presence. On rejection or
   exception restore the affected tourist to source and stop the batch. If even
   restoration fails, stop and explicitly instruct the player to reload their
   pre-transfer save. Do not claim success for that tourist.
6. Count each committed move and fire onCrewTransferred plus CrewWasModified.
   A notification failure stops subsequent moves without undoing or recounting
   the already committed move.
7. Refresh portraits using the stock despawn/next-frame-spawn sequence, including
   after a rollback. Prevent repeated execution while that refresh is pending.
8. Report moved/not-moved counts and any error. Keep remaining eligible selection.

## Lifecycle and UI

One flight window; draggable, with separate scrolling for destinations and crew.
Show free seats, selection count and maximum possible move count. Identify same
model compartments by flight ID. Disable transfer without seats/selection.
Close on vessel switch, source invalidation or scene teardown. Hide on F2.
Release input locks on hide, close, pointer exit, disable and destruction.

## Acceptance scenarios

- Two stock contracts, ordinary crew, and an unassigned tourist in one source:
  only selected stock tourists move; non-tourists remain untouched.
- Enough seats, exactly enough seats, partial capacity, full target, empty
  selection, and multiple selected contracts all behave as specified.
- Identical destination titles are distinguishable by IDs.
- Expired/completed contract, moved tourist, changed capacity, undocking,
  destroyed destination or switched vessel cannot cause a stale transfer.
- No duplication/loss after rejected destination add; callback failures produce
  accurate counts and stop further moves.
- PAW, F2, input lock, portraits, IVA seats, roster status and contract progress
  remain correct. Save/reload retains transferred occupants without duplicates.
- Existing craft receive the PAW button after ModuleManager patching.

Automated transaction coverage and outstanding game-only checks are recorded in
DEVELOPMENT.md. Compilation alone does not satisfy game acceptance.

## Exclusions

Contract Configurator, title parsing, undocked/EVA transfers, ordinary crew,
Connected Living Space routing, third-party selection hooks, automated balancing,
toolbar integration and contract editing are outside this initial scope.

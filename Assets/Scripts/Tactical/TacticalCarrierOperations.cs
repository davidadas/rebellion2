using System;
using System.Collections.Generic;
using System.Numerics;
using Rebellion.Game.Combat;
using Rebellion.Util.Random;

namespace Rebellion.Tactical
{
    /// <summary>
    /// Owns carrier launch requests, clearance-spaced departures, and recovery on the battle clock.
    /// </summary>
    public sealed class TacticalCarrierOperations
    {
        private readonly ActiveBattle _battle;
        private readonly IRandomNumberProvider _random;
        private readonly Dictionary<SpaceCombatUnit, Carrier> _carriers =
            new Dictionary<SpaceCombatUnit, Carrier>();
        private readonly Dictionary<SpaceCombatUnit, Carrier> _assignments =
            new Dictionary<SpaceCombatUnit, Carrier>();

        public event Action<SpaceCombatUnit> SquadronLaunched;
        public int Revision { get; private set; }

        /// <summary>
        /// Indexes the battle's existing carrier assignments and model-measured local bay routes once.
        /// </summary>
        /// <param name="battle">The saved battle owning every flight state and launch request.</param>
        /// <param name="paths">Carrier-local paths ordered from the bay interior toward clear space.</param>
        /// <param name="random">An optional deterministic random source for controlled simulations.</param>
        public TacticalCarrierOperations(
            ActiveBattle battle,
            IReadOnlyDictionary<SpaceCombatUnit, Vector3[][]> paths,
            IRandomNumberProvider random = null
        )
        {
            _battle = battle ?? throw new ArgumentNullException(nameof(battle));
            if (paths == null)
                throw new ArgumentNullException(nameof(paths));
            _random =
                random
                ?? new SystemRandomProvider(battle.FlightRandomSeed, battle.FlightRandomIndex);
            Dictionary<string, Carrier> index = new Dictionary<string, Carrier>(
                StringComparer.Ordinal
            );
            foreach (BattleParticipant participant in battle.GetParticipants())
            foreach (SpaceCombatUnit unit in participant.GetCombatants())
                if (unit is { Kind: SpaceCombatUnitKind.CapitalShip })
                {
                    Carrier carrier = new Carrier(
                        unit,
                        paths.TryGetValue(unit, out Vector3[][] routes)
                            ? routes
                            : Array.Empty<Vector3[]>()
                    );
                    _carriers.Add(unit, carrier);
                    index.Add(unit.SourceUnitInstanceID, carrier);
                }
            foreach (BattleParticipant participant in battle.GetParticipants())
            foreach (SpaceCombatUnit fighter in participant.GetCombatants())
            {
                if (
                    fighter is not { Kind: SpaceCombatUnitKind.Starfighter }
                    || string.IsNullOrEmpty(fighter.Flight.CarrierSourceUnitInstanceID)
                )
                    continue;
                if (
                    !index.TryGetValue(
                        fighter.Flight.CarrierSourceUnitInstanceID,
                        out Carrier carrier
                    )
                    || carrier.Unit.OwnerInstanceID != fighter.OwnerInstanceID
                )
                    throw new ArgumentException(
                        "A carried fighter requires its own faction's carrier in the battle.",
                        nameof(battle)
                    );
                carrier.Cargo.Add(fighter);
                _assignments.Add(fighter, carrier);
                if (
                    carrier.Bays.Length > 0
                    && (
                        fighter.Flight.FlightBayIndex < 0
                        || fighter.Flight.FlightBayIndex >= carrier.Bays.Length
                    )
                )
                    throw new ArgumentException(
                        "A carried fighter references an unavailable bay.",
                        nameof(battle)
                    );
                carrier.Squadrons.Add(fighter.SourceUnitInstanceID, fighter);
                if (fighter.Flight.LaunchFormationOffset == null && carrier.Bays.Length > 0)
                    fighter.Flight.FlightBayIndex = (carrier.Cargo.Count - 1) % carrier.Bays.Length;
                fighter.Flight.LaunchFormationOffset ??= FromVector(
                    ToLocal(
                        carrier.Unit,
                        ToVector(fighter.Position) - ToVector(carrier.Unit.Position)
                    )
                );
                if (fighter.Flight.Phase == FighterFlightState.Docked && carrier.Bays.Length > 0)
                    SetVector(
                        fighter.Position,
                        ToWorld(carrier.Unit, carrier.Bays[fighter.Flight.FlightBayIndex].Path[0])
                    );
            }
        }

        /// <summary>
        /// Returns the stable manifest of a carrier without searching the scene graph.
        /// </summary>
        /// <param name="carrier">The inspected battle unit.</param>
        /// <returns>The assigned fighters, or an empty manifest for another kind of unit.</returns>
        public IReadOnlyList<SpaceCombatUnit> GetManifest(SpaceCombatUnit carrier) =>
            _carriers.TryGetValue(carrier, out Carrier state)
                ? state.Manifest
                : Array.Empty<SpaceCombatUnit>();

        /// <summary>
        /// Reports whether every selected squadron can recover aboard one visible friendly carrier.
        /// </summary>
        /// <param name="selection">The selected battle units.</param>
        /// <param name="target">The carrier under the pointer.</param>
        /// <param name="owner">The ordering faction.</param>
        /// <returns>Whether the complete selection can accept the targeted recovery order.</returns>
        public bool CanDockAt(
            IReadOnlyList<SpaceCombatUnit> selection,
            SpaceCombatUnit target,
            string owner
        )
        {
            if (
                selection == null
                || selection.Count == 0
                || target == null
                || !_carriers.TryGetValue(target, out Carrier carrier)
                || carrier.Bays.Length == 0
                || !IsControlled(target, owner)
            )
                return false;

            int requiredSlots = 0;
            foreach (SpaceCombatUnit unit in selection)
            {
                if (
                    unit is not { Kind: SpaceCombatUnitKind.Starfighter }
                    || !IsControlled(unit, owner)
                    || unit.Flight.Phase != FighterFlightState.Deployed
                )
                    return false;
                if (!_assignments.TryGetValue(unit, out Carrier assigned) || assigned != carrier)
                    requiredSlots++;
            }

            return requiredSlots <= GetAvailableCapacity(carrier);
        }

        /// <summary>
        /// Reassigns the selected deployed squadrons to one targeted carrier and begins recovery.
        /// </summary>
        /// <param name="selection">The selected fighter squadrons.</param>
        /// <param name="target">The friendly carrier receiving them.</param>
        /// <param name="owner">The ordering faction.</param>
        /// <returns>Whether the complete docking order was accepted.</returns>
        public bool DockAt(
            IReadOnlyList<SpaceCombatUnit> selection,
            SpaceCombatUnit target,
            string owner
        )
        {
            if (!CanDockAt(selection, target, owner))
                return false;

            Carrier targetCarrier = _carriers[target];
            foreach (SpaceCombatUnit fighter in selection)
            {
                if (!_assignments.TryGetValue(fighter, out Carrier assigned))
                    assigned = null;
                if (assigned != targetCarrier)
                    Reassign(fighter, assigned, targetCarrier);
                RecallFighter(targetCarrier, fighter);
            }
            return true;
        }

        /// <summary>
        /// Reports whether the selection contains a controlled, docked squadron with an authored bay.
        /// </summary>
        /// <param name="selection">The live player selection.</param>
        /// <param name="owner">The ordering faction.</param>
        /// <returns>Whether a launch command has an eligible recipient.</returns>
        public bool CanLaunch(IReadOnlyList<SpaceCombatUnit> selection, string owner)
        {
            foreach (SpaceCombatUnit unit in selection)
            {
                if (!IsControlled(unit, owner))
                    continue;
                if (
                    _assignments.TryGetValue(unit, out Carrier assigned)
                    && assigned.Bays.Length > 0
                    && IsAlive(assigned.Unit)
                    && unit.Flight.Phase == FighterFlightState.Docked
                    && unit.GetSublightSpeed() > 0f
                )
                    return true;
                if (_carriers.TryGetValue(unit, out Carrier carrier) && carrier.Bays.Length > 0)
                    foreach (SpaceCombatUnit fighter in carrier.Cargo)
                        if (
                            IsAlive(fighter)
                            && fighter.Flight.Phase == FighterFlightState.Docked
                            && fighter.GetSublightSpeed() > 0f
                        )
                            return true;
            }
            return false;
        }

        /// <summary>
        /// Queues selected squadrons, or all docked squadrons of selected carriers, without duplicates.
        /// </summary>
        /// <param name="selection">The carriers or carried squadrons receiving the order.</param>
        /// <param name="owner">The ordering faction.</param>
        public void Launch(IReadOnlyList<SpaceCombatUnit> selection, string owner)
        {
            foreach (SpaceCombatUnit unit in selection)
            {
                if (!IsControlled(unit, owner))
                    continue;
                if (_carriers.TryGetValue(unit, out Carrier carrier))
                    foreach (SpaceCombatUnit squadron in carrier.Cargo)
                        QueueSquadron(carrier, squadron);
                else if (_assignments.TryGetValue(unit, out Carrier assigned))
                    QueueSquadron(assigned, unit);
            }
        }

        /// <summary>
        /// Reports whether selected squadrons or carriers have fighters that can be recalled.
        /// </summary>
        /// <param name="selection">The inspected selection.</param>
        /// <param name="owner">The ordering faction.</param>
        /// <returns>Whether a recovery command has an eligible recipient.</returns>
        public bool CanRecall(IReadOnlyList<SpaceCombatUnit> selection, string owner)
        {
            foreach (SpaceCombatUnit unit in selection)
            {
                if (!IsControlled(unit, owner))
                    continue;
                if (
                    _assignments.TryGetValue(unit, out Carrier assigned)
                    && assigned.Bays.Length > 0
                    && IsAlive(assigned.Unit)
                    && unit.Flight.Phase != FighterFlightState.Docked
                    && unit.Flight.Phase != FighterFlightState.Returning
                    && unit.Flight.Phase != FighterFlightState.Docking
                    && !unit.Flight.RecallRequested
                )
                    return true;
                if (_carriers.TryGetValue(unit, out Carrier carrier) && carrier.Bays.Length > 0)
                    foreach (SpaceCombatUnit fighter in carrier.Cargo)
                        if (
                            IsAlive(fighter)
                            && !fighter.Flight.RecallRequested
                            && (
                                fighter.Flight.Phase == FighterFlightState.Deployed
                                || fighter.Flight.Phase == FighterFlightState.Queued
                                || fighter.Flight.Phase == FighterFlightState.Launching
                            )
                        )
                            return true;
            }
            return false;
        }

        /// <summary>
        /// Recalls assigned fighters and cancels queued launches without teleporting deployed units.
        /// </summary>
        /// <param name="selection">The selected carriers or fighters.</param>
        /// <param name="owner">The ordering faction.</param>
        public void Recall(IReadOnlyList<SpaceCombatUnit> selection, string owner)
        {
            foreach (SpaceCombatUnit unit in selection)
            {
                if (!IsControlled(unit, owner))
                    continue;
                if (_carriers.TryGetValue(unit, out Carrier carrier))
                    foreach (SpaceCombatUnit fighter in carrier.Cargo)
                        RecallFighter(carrier, fighter);
                else if (_assignments.TryGetValue(unit, out Carrier assigned))
                    RecallFighter(assigned, unit);
            }
        }

        /// <summary>
        /// Cancels active recovery for controlled squadrons so another movement or stop order owns them.
        /// </summary>
        /// <param name="selection">The selected battle units.</param>
        /// <param name="owner">The ordering faction.</param>
        /// <returns>Whether any recovery state changed.</returns>
        public bool CancelRecovery(IReadOnlyList<SpaceCombatUnit> selection, string owner)
        {
            if (selection == null)
                throw new ArgumentNullException(nameof(selection));
            bool changed = false;
            foreach (SpaceCombatUnit fighter in selection)
            {
                if (
                    !IsControlled(fighter, owner)
                    || fighter is not { Kind: SpaceCombatUnitKind.Starfighter }
                    || fighter.Flight.Phase
                        is not (FighterFlightState.Returning or FighterFlightState.Docking)
                )
                    continue;
                fighter.Flight.Phase = FighterFlightState.Deployed;
                fighter.Flight.RecallRequested = false;
                fighter.Flight.FlightWaypointIndex = 0;
                fighter.Flight.FlightSegmentDistance = 0f;
                fighter.Flight.FlightDelay = 0f;
                fighter.MoveOrder.Destination = null;
                fighter.MoveOrder.SpeedLimit = null;
                changed = true;
            }
            if (changed)
                Revision++;
            return changed;
        }

        /// <summary>
        /// Advances indexed carrier queues and bay traffic once per unpaused tactical frame.
        /// </summary>
        /// <param name="deltaTime">Elapsed unpaused battle seconds.</param>
        public void Advance(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (deltaTime == 0f)
                return;
            foreach (Carrier carrier in _carriers.Values)
            {
                if (!IsAlive(carrier.Unit) || carrier.Bays.Length == 0)
                    continue;
                foreach (Bay bay in carrier.Bays)
                {
                    bay.Returning.Clear();
                    bay.Outbound = 0;
                    bay.Inbound = false;
                }
                foreach (SpaceCombatUnit fighter in carrier.Cargo)
                {
                    if (!IsAlive(fighter))
                        continue;
                    Bay bay = carrier.Bays[fighter.Flight.FlightBayIndex];
                    if (fighter.Flight.Phase == FighterFlightState.Launching)
                    {
                        AdvancePath(carrier, fighter, bay.Path, deltaTime, false);
                        if (fighter.Flight.Phase == FighterFlightState.Launching)
                            bay.Outbound++;
                    }
                    else if (fighter.Flight.Phase == FighterFlightState.Docking)
                    {
                        AdvancePath(carrier, fighter, bay.Path, deltaTime, true);
                        bay.Inbound |= fighter.Flight.Phase == FighterFlightState.Docking;
                    }
                    if (fighter.Flight.Phase == FighterFlightState.Returning)
                        bay.Returning.Add(fighter);
                    else if (
                        fighter.Flight.Phase == FighterFlightState.Docked
                        || fighter.Flight.Phase == FighterFlightState.Queued
                    )
                        SetVector(fighter.Position, ToWorld(carrier.Unit, bay.Path[0]));
                }
                foreach (Bay bay in carrier.Bays)
                    AdvanceRecovery(carrier, bay, deltaTime);
                if (carrier.Unit.Carrier.LaunchQueue.Count == 0)
                    continue;
                carrier.Unit.Carrier.LaunchCountdown -= deltaTime;
                if (carrier.Unit.Carrier.LaunchCountdown > 0f)
                    continue;
                bool recovering = false;
                foreach (Bay bay in carrier.Bays)
                    recovering |= bay.Inbound;
                if (recovering)
                    continue;
                ReleaseSquadron(carrier);
            }
        }

        /// <summary>
        /// Adds one live docked squadron to a carrier's persistent launch queue.
        /// </summary>
        /// <param name="carrier">The assigned carrier.</param>
        /// <param name="squadron">The squadron's single battle unit.</param>
        private void QueueSquadron(Carrier carrier, SpaceCombatUnit squadron)
        {
            if (
                carrier.Bays.Length == 0
                || !IsAlive(carrier.Unit)
                || carrier.Unit.Carrier.LaunchQueue.Contains(squadron.SourceUnitInstanceID)
                || !IsAlive(squadron)
                || squadron.Flight.Phase != FighterFlightState.Docked
                || squadron.GetSublightSpeed() <= 0f
            )
                return;
            squadron.Flight.Phase = FighterFlightState.Queued;
            squadron.Flight.RecallRequested = false;
            if (carrier.Unit.Carrier.LaunchQueue.Count == 0)
                carrier.Unit.Carrier.LaunchCountdown = NextLaunchDelay();
            carrier.Unit.Carrier.LaunchQueue.Add(squadron.SourceUnitInstanceID);
            Revision++;
        }

        /// <summary>
        /// Releases one squadron through clearance-spaced bay lanes and emits one audible release cue.
        /// </summary>
        /// <param name="carrier">The carrier whose queue is due.</param>
        private void ReleaseSquadron(Carrier carrier)
        {
            foreach (SpaceCombatUnit departing in carrier.Cargo)
                if (
                    IsAlive(departing)
                    && departing.Flight.Phase == FighterFlightState.Launching
                    && departing.GetSublightSpeed() <= 0f
                )
                    return;
            string id = carrier.Unit.Carrier.LaunchQueue[0];
            carrier.Unit.Carrier.LaunchQueue.RemoveAt(0);
            if (
                carrier.Squadrons.TryGetValue(id, out SpaceCombatUnit fighter)
                && IsAlive(fighter)
                && fighter.Flight.Phase == FighterFlightState.Queued
            )
            {
                int bayIndex = fighter.Flight.FlightBayIndex;
                Bay bay = carrier.Bays[bayIndex];
                float delay = 0f;
                float followingSpeed = fighter.GetSublightSpeed();
                if (followingSpeed <= 0f)
                {
                    fighter.Flight.Phase = FighterFlightState.Docked;
                }
                else
                {
                    foreach (SpaceCombatUnit preceding in carrier.Cargo)
                        if (
                            preceding != fighter
                            && IsAlive(preceding)
                            && preceding.Flight.Phase == FighterFlightState.Launching
                            && preceding.Flight.FlightBayIndex == bayIndex
                        )
                        {
                            float speed = preceding.GetSublightSpeed();
                            float spacing = GetRadius(preceding) + GetRadius(fighter);
                            float progress = GetPathDistance(preceding, bay.Path);
                            delay = Math.Max(
                                delay,
                                preceding.Flight.FlightDelay + (spacing - progress) / speed
                            );
                            delay = Math.Max(
                                delay,
                                preceding.Flight.FlightDelay
                                    + (bay.Length - progress) / speed
                                    - Math.Max(0f, bay.Length - spacing) / followingSpeed
                            );
                        }
                    fighter.Flight.FlightBayIndex = bayIndex;
                    fighter.Flight.FlightWaypointIndex = 0;
                    fighter.Flight.FlightSegmentDistance = 0f;
                    fighter.Flight.FlightDelay = delay;
                    fighter.Flight.Phase = FighterFlightState.Launching;
                    fighter.MoveOrder.Destination = null;
                    fighter.MoveOrder.SpeedLimit = null;
                    SetVector(fighter.Position, ToWorld(carrier.Unit, bay.Path[0]));
                    SetVector(
                        fighter.Forward,
                        ToDirection(carrier.Unit, Vector3.Normalize(bay.Path[1] - bay.Path[0]))
                    );
                    SquadronLaunched?.Invoke(carrier.Unit);
                }
            }
            Revision++;
            if (carrier.Unit.Carrier.LaunchQueue.Count > 0)
                carrier.Unit.Carrier.LaunchCountdown = NextLaunchDelay();
        }

        /// <summary>
        /// Sends a deployed fighter home, or cancels its departure while it is still aboard.
        /// </summary>
        /// <param name="carrier">The assigned carrier.</param>
        /// <param name="fighter">The fighter receiving recall.</param>
        private void RecallFighter(Carrier carrier, SpaceCombatUnit fighter)
        {
            if (!IsAlive(carrier.Unit) || !IsAlive(fighter) || carrier.Bays.Length == 0)
                return;
            if (
                fighter.Flight.Phase == FighterFlightState.Queued
                || (
                    fighter.Flight.Phase == FighterFlightState.Launching
                    && fighter.Flight.FlightDelay > 0f
                )
            )
                fighter.Flight.Phase = FighterFlightState.Docked;
            else if (fighter.Flight.Phase == FighterFlightState.Launching)
                fighter.Flight.RecallRequested = true;
            else if (fighter.Flight.Phase == FighterFlightState.Deployed)
                fighter.Flight.Phase = FighterFlightState.Returning;
            else
                return;
            fighter.MoveOrder.Destination = null;
            fighter.MoveOrder.SpeedLimit = null;
            if (fighter.Flight.Phase == FighterFlightState.Docked)
            {
                fighter.Flight.RecallRequested = false;
                fighter.Flight.FlightDelay = 0f;
                carrier.Unit.Carrier.LaunchQueue.Remove(fighter.SourceUnitInstanceID);
            }
            Revision++;
        }

        /// <summary>
        /// Moves one deployed squadron between the indexed carrier manifests without scene-graph scans.
        /// </summary>
        /// <param name="fighter">The deployed squadron changing carriers.</param>
        /// <param name="source">Its previous carrier, when assigned.</param>
        /// <param name="destination">The carrier receiving the squadron.</param>
        private void Reassign(SpaceCombatUnit fighter, Carrier source, Carrier destination)
        {
            if (source != null)
            {
                source.Cargo.Remove(fighter);
                source.Squadrons.Remove(fighter.SourceUnitInstanceID);
                source.Unit.Carrier.LaunchQueue.Remove(fighter.SourceUnitInstanceID);
            }

            fighter.Flight.CarrierSourceUnitInstanceID = destination.Unit.SourceUnitInstanceID;
            fighter.Flight.FlightBayIndex = destination.Cargo.Count % destination.Bays.Length;
            fighter.Flight.LaunchFormationOffset = FromVector(
                ToLocal(
                    destination.Unit,
                    ToVector(fighter.Position) - ToVector(destination.Unit.Position)
                )
            );
            destination.Cargo.Add(fighter);
            destination.Squadrons[fighter.SourceUnitInstanceID] = fighter;
            _assignments[fighter] = destination;
        }

        /// <summary>
        /// Counts unoccupied strategic fighter berths for one indexed carrier.
        /// </summary>
        /// <param name="carrier">The inspected carrier state.</param>
        /// <returns>The non-negative number of available squadron berths.</returns>
        private static int GetAvailableCapacity(Carrier carrier)
        {
            int occupied = 0;
            foreach (SpaceCombatUnit fighter in carrier.Cargo)
                if (IsAlive(fighter))
                    occupied++;
            return Math.Max(0, carrier.Unit.GetStarfighterCapacity() - occupied);
        }

        /// <summary>
        /// Holds returning fighters in separated approach positions and admits one fighter per free bay.
        /// </summary>
        /// <param name="carrier">The moving reference frame for the bay.</param>
        /// <param name="bay">The bay's indexed incoming traffic.</param>
        /// <param name="deltaTime">Elapsed battle seconds.</param>
        private void AdvanceRecovery(Carrier carrier, Bay bay, float deltaTime)
        {
            Vector3 approach = bay.Path[bay.Path.Length - 1];
            Vector3 outward = Vector3.Normalize(approach - bay.Path[bay.Path.Length - 2]);
            float offset = 0f;
            for (int index = 0; index < bay.Returning.Count; index++)
            {
                SpaceCombatUnit fighter = bay.Returning[index];
                if (index > 0)
                    offset += GetRadius(bay.Returning[index - 1]) + GetRadius(fighter);
                Vector3 target = ToWorld(carrier.Unit, approach + outward * offset);
                fighter.MoveOrder.Destination ??= new BattleVector3();
                fighter.MoveOrder.SpeedLimit = null;
                SetVector(fighter.MoveOrder.Destination, target);
                TacticalMovement.Advance(fighter, deltaTime);
                if (
                    index != 0
                    || bay.Outbound > 0
                    || bay.Inbound
                    || fighter.MoveOrder.Destination != null
                )
                    continue;
                fighter.Flight.Phase = FighterFlightState.Docking;
                fighter.Flight.FlightWaypointIndex = bay.Path.Length - 1;
                fighter.Flight.FlightSegmentDistance = 0f;
                fighter.Flight.FlightDelay = 0f;
                fighter.Flight.RecallRequested = false;
                bay.Inbound = true;
                Revision++;
            }
        }

        /// <summary>
        /// Traverses an authored route at sublight speed while following the carrier's current pose.
        /// </summary>
        /// <param name="carrier">The bay's moving reference frame.</param>
        /// <param name="fighter">The squadron's single representation in controlled bay flight.</param>
        /// <param name="path">The carrier-local route.</param>
        /// <param name="deltaTime">Elapsed battle seconds.</param>
        /// <param name="inbound">Whether to traverse the route in reverse.</param>
        private void AdvancePath(
            Carrier carrier,
            SpaceCombatUnit fighter,
            Vector3[] path,
            float deltaTime,
            bool inbound
        )
        {
            if (fighter.Flight.FlightDelay > 0f)
            {
                float waiting = Math.Min(deltaTime, fighter.Flight.FlightDelay);
                fighter.Flight.FlightDelay -= waiting;
                deltaTime -= waiting;
            }
            float travel = fighter.GetSublightSpeed() * deltaTime;
            int step = inbound ? -1 : 1;
            while (true)
            {
                int next = fighter.Flight.FlightWaypointIndex + step;
                Vector3 segment = path[next] - path[fighter.Flight.FlightWaypointIndex];
                float length = segment.Length();
                float remaining = length - fighter.Flight.FlightSegmentDistance;
                if (travel < remaining)
                {
                    fighter.Flight.FlightSegmentDistance += travel;
                    SetVector(
                        fighter.Position,
                        ToWorld(
                            carrier.Unit,
                            path[fighter.Flight.FlightWaypointIndex]
                                + segment * (fighter.Flight.FlightSegmentDistance / length)
                        )
                    );
                    SetVector(fighter.Forward, ToDirection(carrier.Unit, segment / length));
                    return;
                }
                travel -= remaining;
                fighter.Flight.FlightWaypointIndex = next;
                fighter.Flight.FlightSegmentDistance = 0f;
                SetVector(fighter.Position, ToWorld(carrier.Unit, path[next]));
                SetVector(fighter.Forward, ToDirection(carrier.Unit, segment / length));
                if (next != (inbound ? 0 : path.Length - 1))
                    continue;
                fighter.Flight.Phase =
                    inbound ? FighterFlightState.Docked
                    : fighter.Flight.RecallRequested ? FighterFlightState.Returning
                    : FighterFlightState.Deployed;
                fighter.MoveOrder.Destination =
                    inbound || fighter.Flight.RecallRequested
                        ? null
                        : FromVector(
                            ToWorld(carrier.Unit, ToVector(fighter.Flight.LaunchFormationOffset))
                        );
                fighter.MoveOrder.SpeedLimit = null;
                Revision++;
                return;
            }
        }

        /// <summary>
        /// Draws a squadron release interval and persists the existing random provider's position.
        /// </summary>
        /// <returns>The next interval in unpaused battle seconds.</returns>
        private float NextLaunchDelay()
        {
            int delay = 5 + _random.NextInt(0, 9);
            if (_random is SystemRandomProvider provider)
                _battle.FlightRandomIndex = provider.CallCount;
            return delay;
        }

        /// <summary>
        /// Calculates how far a departing fighter has travelled along its bay lane.
        /// </summary>
        /// <param name="fighter">The departing fighter.</param>
        /// <param name="path">Its carrier-local route.</param>
        /// <returns>The distance from the start of the route.</returns>
        private static float GetPathDistance(SpaceCombatUnit fighter, Vector3[] path)
        {
            float distance = fighter.Flight.FlightSegmentDistance;
            for (int index = 0; index < fighter.Flight.FlightWaypointIndex; index++)
                distance += Vector3.Distance(path[index], path[index + 1]);
            return distance;
        }

        /// <summary>
        /// Returns a conservative hull radius for spacing fighters through bay lanes.
        /// </summary>
        /// <param name="fighter">The measured fighter.</param>
        /// <returns>The radius of its enclosing sphere.</returns>
        private static float GetRadius(SpaceCombatUnit fighter)
        {
            var size = fighter.ModelSize;
            return new Vector3(size.Width, size.Height, size.Depth).Length() * 0.5f;
        }

        /// <summary>
        /// Checks owner authority and remaining tactical unit strength.
        /// </summary>
        /// <param name="unit">The requested unit.</param>
        /// <param name="owner">The ordering faction.</param>
        /// <returns>Whether this player controls a living unit.</returns>
        private static bool IsControlled(SpaceCombatUnit unit, string owner) =>
            !string.IsNullOrEmpty(owner) && unit.OwnerInstanceID == owner && IsAlive(unit);

        /// <summary>
        /// Checks withdrawal and recorded fighter or hull losses.
        /// </summary>
        /// <param name="unit">The battle unit.</param>
        /// <returns>Whether the unit remains available for carrier operations.</returns>
        private static bool IsAlive(SpaceCombatUnit unit) => !unit.HasRetreated && unit.IsAlive;

        /// <summary>
        /// Rotates a carrier-local direction into the battle's world axes without introducing roll.
        /// </summary>
        /// <param name="carrier">The carrier's saved pose.</param>
        /// <param name="local">The carrier-local direction.</param>
        /// <returns>The world-space direction.</returns>
        private static Vector3 ToDirection(SpaceCombatUnit carrier, Vector3 local)
        {
            Vector3 forward = Vector3.Normalize(ToVector(carrier.Forward));
            Vector3 right = Vector3.Cross(Vector3.UnitY, forward);
            if (right.LengthSquared() == 0f)
                right = Vector3.Cross(Vector3.UnitX, forward);
            right = Vector3.Normalize(right);
            Vector3 up = Vector3.Cross(forward, right);
            return right * local.X + up * local.Y + forward * local.Z;
        }

        /// <summary>
        /// Converts a world-axis displacement to the carrier's unrolled local frame.
        /// </summary>
        /// <param name="carrier">The carrier's saved pose.</param>
        /// <param name="world">The world-axis displacement.</param>
        /// <returns>The carrier-local displacement.</returns>
        private static Vector3 ToLocal(SpaceCombatUnit carrier, Vector3 world) =>
            new Vector3(
                Vector3.Dot(world, ToDirection(carrier, Vector3.UnitX)),
                Vector3.Dot(world, ToDirection(carrier, Vector3.UnitY)),
                Vector3.Dot(world, ToDirection(carrier, Vector3.UnitZ))
            );

        /// <summary>
        /// Transforms a carrier-local point into a saved battle position.
        /// </summary>
        /// <param name="carrier">The carrier's saved pose.</param>
        /// <param name="local">The carrier-local point.</param>
        /// <returns>The world-space point.</returns>
        private static Vector3 ToWorld(SpaceCombatUnit carrier, Vector3 local) =>
            ToVector(carrier.Position) + ToDirection(carrier, local);

        /// <summary>
        /// Reads persistent battle coordinates without allocating.
        /// </summary>
        /// <param name="value">The saved coordinates.</param>
        /// <returns>The numerical vector.</returns>
        private static Vector3 ToVector(BattleVector3 value) =>
            new Vector3(value.X, value.Y, value.Z);

        /// <summary>
        /// Creates independently owned persistent battle coordinates.
        /// </summary>
        /// <param name="value">The numerical vector.</param>
        /// <returns>The saved coordinates.</returns>
        private static BattleVector3 FromVector(Vector3 value) =>
            new BattleVector3
            {
                X = value.X,
                Y = value.Y,
                Z = value.Z,
            };

        /// <summary>
        /// Updates persistent coordinates in place during flight.
        /// </summary>
        /// <param name="target">The saved coordinates.</param>
        /// <param name="value">The numerical vector.</param>
        private static void SetVector(BattleVector3 target, Vector3 value)
        {
            target.X = value.X;
            target.Y = value.Y;
            target.Z = value.Z;
        }

        private sealed class Carrier
        {
            public readonly SpaceCombatUnit Unit;
            public readonly List<SpaceCombatUnit> Cargo = new List<SpaceCombatUnit>();
            public readonly IReadOnlyList<SpaceCombatUnit> Manifest;
            public readonly Dictionary<string, SpaceCombatUnit> Squadrons = new Dictionary<
                string,
                SpaceCombatUnit
            >(StringComparer.Ordinal);
            public readonly Bay[] Bays;

            /// <summary>
            /// Creates the indexed carrier manifest and validates authored numerical flight paths.
            /// </summary>
            /// <param name="unit">The carrier combatant.</param>
            /// <param name="paths">Its measured carrier-local bay routes.</param>
            public Carrier(SpaceCombatUnit unit, Vector3[][] paths)
            {
                Unit = unit;
                Manifest = Cargo.AsReadOnly();
                Bays = new Bay[paths.Length];
                for (int index = 0; index < paths.Length; index++)
                    Bays[index] = new Bay(paths[index]);
            }
        }

        private sealed class Bay
        {
            public readonly Vector3[] Path;
            public readonly float Length;
            public readonly List<SpaceCombatUnit> Returning = new List<SpaceCombatUnit>();
            public int Outbound;
            public bool Inbound;

            /// <summary>
            /// Validates and owns one model-measured bay route.
            /// </summary>
            /// <param name="path">The carrier-local route ordered from inside the bay toward clear space.</param>
            public Bay(Vector3[] path)
            {
                if (path == null || path.Length < 2)
                    throw new ArgumentException(
                        "Bay flight paths require at least two distinct points.",
                        nameof(path)
                    );
                for (int index = 0; index < path.Length; index++)
                    if (
                        !float.IsFinite(path[index].X)
                        || !float.IsFinite(path[index].Y)
                        || !float.IsFinite(path[index].Z)
                        || (index > 0 && path[index] == path[index - 1])
                    )
                        throw new ArgumentException(
                            "Bay flight paths require finite, distinct consecutive points.",
                            nameof(path)
                        );
                Path = (Vector3[])path.Clone();
                for (int index = 1; index < path.Length; index++)
                    Length += Vector3.Distance(path[index - 1], path[index]);
            }
        }
    }
}

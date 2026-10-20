using System;
using System.Collections.Generic;
using System.Numerics;
using Rebellion.Game.Combat;
using Rebellion.Game.Units;
using Rebellion.Util.Random;

namespace Rebellion.Simulation.Tactical
{
    /// <summary>
    /// Owns carrier launch requests, clearance-spaced departures, and recovery on the battle clock.
    /// </summary>
    public sealed class TacticalCarrierOperations
    {
        private readonly ActiveBattle _battle;
        private readonly IRandomNumberProvider _random;
        private readonly Dictionary<CombatUnit, Carrier> _carriers =
            new Dictionary<CombatUnit, Carrier>();
        private readonly Dictionary<CombatUnit, Carrier> _assignments =
            new Dictionary<CombatUnit, Carrier>();

        public event Action<CombatUnit> SquadronLaunched;
        public int Revision { get; private set; }

        /// <summary>
        /// Indexes the battle's existing carrier assignments and model-measured local bay routes once.
        /// </summary>
        /// <param name="battle">The saved battle owning every flight state and launch request.</param>
        /// <param name="paths">Carrier-local paths ordered from the bay interior toward clear space.</param>
        /// <param name="random">An optional deterministic random source for controlled simulations.</param>
        public TacticalCarrierOperations(
            ActiveBattle battle,
            IReadOnlyDictionary<CombatUnit, Vector3[][]> paths,
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
            foreach (CombatUnit unit in participant.GetCombatants())
                if (unit.GetUnit() is CapitalShip)
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
            foreach (CombatUnit fighter in participant.GetCombatants())
            {
                if (
                    fighter.GetUnit() is not Starfighter
                    || string.IsNullOrEmpty(fighter.CarrierSourceUnitInstanceID)
                )
                    continue;
                if (
                    !index.TryGetValue(fighter.CarrierSourceUnitInstanceID, out Carrier carrier)
                    || carrier.Unit.GetUnit().OwnerInstanceID != fighter.GetUnit().OwnerInstanceID
                )
                    throw new ArgumentException(
                        "A carried fighter requires its own faction's carrier in the battle.",
                        nameof(battle)
                    );
                carrier.Cargo.Add(fighter);
                _assignments.Add(fighter, carrier);
                if (
                    carrier.Bays.Length > 0
                    && (fighter.FlightBayIndex < 0 || fighter.FlightBayIndex >= carrier.Bays.Length)
                )
                    throw new ArgumentException(
                        "A carried fighter references an unavailable bay.",
                        nameof(battle)
                    );
                carrier.Squadrons.Add(fighter.SourceUnitInstanceID, fighter);
                if (fighter.LaunchFormationOffset == null && carrier.Bays.Length > 0)
                    fighter.FlightBayIndex = (carrier.Cargo.Count - 1) % carrier.Bays.Length;
                fighter.LaunchFormationOffset ??= FromVector(
                    ToLocal(
                        carrier.Unit,
                        ToVector(fighter.Position) - ToVector(carrier.Unit.Position)
                    )
                );
                if (fighter.FlightState == FighterFlightState.Docked && carrier.Bays.Length > 0)
                    SetVector(
                        fighter.Position,
                        ToWorld(carrier.Unit, carrier.Bays[fighter.FlightBayIndex].Path[0])
                    );
            }
        }

        /// <summary>
        /// Returns the stable manifest of a carrier without searching the scene graph.
        /// </summary>
        /// <param name="carrier">The inspected battle unit.</param>
        /// <returns>The assigned fighters, or an empty manifest for another kind of unit.</returns>
        public IReadOnlyList<CombatUnit> GetManifest(CombatUnit carrier) =>
            _carriers.TryGetValue(carrier, out Carrier state)
                ? state.Manifest
                : Array.Empty<CombatUnit>();

        /// <summary>
        /// Reports whether the selection contains a controlled, docked squadron with an authored bay.
        /// </summary>
        /// <param name="selection">The live player selection.</param>
        /// <param name="owner">The ordering faction.</param>
        /// <returns>Whether a launch command has an eligible recipient.</returns>
        public bool CanLaunch(IReadOnlyList<CombatUnit> selection, string owner)
        {
            foreach (CombatUnit unit in selection)
            {
                if (!IsControlled(unit, owner))
                    continue;
                if (
                    _assignments.TryGetValue(unit, out Carrier assigned)
                    && assigned.Bays.Length > 0
                    && IsAlive(assigned.Unit)
                    && unit.FlightState == FighterFlightState.Docked
                    && ((Starfighter)unit.GetUnit()).GetSublightSpeed() > 0f
                )
                    return true;
                if (_carriers.TryGetValue(unit, out Carrier carrier) && carrier.Bays.Length > 0)
                    foreach (CombatUnit fighter in carrier.Cargo)
                        if (
                            IsAlive(fighter)
                            && fighter.FlightState == FighterFlightState.Docked
                            && ((Starfighter)fighter.GetUnit()).GetSublightSpeed() > 0f
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
        public void Launch(IReadOnlyList<CombatUnit> selection, string owner)
        {
            foreach (CombatUnit unit in selection)
            {
                if (!IsControlled(unit, owner))
                    continue;
                if (_carriers.TryGetValue(unit, out Carrier carrier))
                    foreach (CombatUnit squadron in carrier.Cargo)
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
        public bool CanRecall(IReadOnlyList<CombatUnit> selection, string owner)
        {
            foreach (CombatUnit unit in selection)
            {
                if (!IsControlled(unit, owner))
                    continue;
                if (
                    _assignments.TryGetValue(unit, out Carrier assigned)
                    && assigned.Bays.Length > 0
                    && IsAlive(assigned.Unit)
                    && unit.FlightState != FighterFlightState.Docked
                    && unit.FlightState != FighterFlightState.Returning
                    && unit.FlightState != FighterFlightState.Docking
                    && !unit.RecallRequested
                )
                    return true;
                if (_carriers.TryGetValue(unit, out Carrier carrier) && carrier.Bays.Length > 0)
                    foreach (CombatUnit fighter in carrier.Cargo)
                        if (
                            IsAlive(fighter)
                            && !fighter.RecallRequested
                            && (
                                fighter.FlightState == FighterFlightState.Deployed
                                || fighter.FlightState == FighterFlightState.Queued
                                || fighter.FlightState == FighterFlightState.Launching
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
        public void Recall(IReadOnlyList<CombatUnit> selection, string owner)
        {
            foreach (CombatUnit unit in selection)
            {
                if (!IsControlled(unit, owner))
                    continue;
                if (_carriers.TryGetValue(unit, out Carrier carrier))
                    foreach (CombatUnit fighter in carrier.Cargo)
                        RecallFighter(carrier, fighter);
                else if (_assignments.TryGetValue(unit, out Carrier assigned))
                    RecallFighter(assigned, unit);
            }
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
                foreach (CombatUnit fighter in carrier.Cargo)
                {
                    if (!IsAlive(fighter))
                        continue;
                    Bay bay = carrier.Bays[fighter.FlightBayIndex];
                    if (fighter.FlightState == FighterFlightState.Launching)
                    {
                        AdvancePath(carrier, fighter, bay.Path, deltaTime, false);
                        if (fighter.FlightState == FighterFlightState.Launching)
                            bay.Outbound++;
                    }
                    else if (fighter.FlightState == FighterFlightState.Docking)
                    {
                        AdvancePath(carrier, fighter, bay.Path, deltaTime, true);
                        bay.Inbound |= fighter.FlightState == FighterFlightState.Docking;
                    }
                    if (fighter.FlightState == FighterFlightState.Returning)
                        bay.Returning.Add(fighter);
                    else if (
                        fighter.FlightState == FighterFlightState.Docked
                        || fighter.FlightState == FighterFlightState.Queued
                    )
                        SetVector(fighter.Position, ToWorld(carrier.Unit, bay.Path[0]));
                }
                foreach (Bay bay in carrier.Bays)
                    AdvanceRecovery(carrier, bay, deltaTime);
                if (carrier.Unit.LaunchQueue.Count == 0)
                    continue;
                carrier.Unit.LaunchCountdown -= deltaTime;
                if (carrier.Unit.LaunchCountdown > 0f)
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
        private void QueueSquadron(Carrier carrier, CombatUnit squadron)
        {
            if (
                carrier.Bays.Length == 0
                || !IsAlive(carrier.Unit)
                || carrier.Unit.LaunchQueue.Contains(squadron.SourceUnitInstanceID)
                || !IsAlive(squadron)
                || squadron.FlightState != FighterFlightState.Docked
                || ((Starfighter)squadron.GetUnit()).GetSublightSpeed() <= 0f
            )
                return;
            squadron.FlightState = FighterFlightState.Queued;
            squadron.RecallRequested = false;
            if (carrier.Unit.LaunchQueue.Count == 0)
                carrier.Unit.LaunchCountdown = NextLaunchDelay();
            carrier.Unit.LaunchQueue.Add(squadron.SourceUnitInstanceID);
            Revision++;
        }

        /// <summary>
        /// Releases one squadron through clearance-spaced bay lanes and emits one audible release cue.
        /// </summary>
        /// <param name="carrier">The carrier whose queue is due.</param>
        private void ReleaseSquadron(Carrier carrier)
        {
            foreach (CombatUnit departing in carrier.Cargo)
                if (
                    IsAlive(departing)
                    && departing.FlightState == FighterFlightState.Launching
                    && ((Starfighter)departing.GetUnit()).GetSublightSpeed() <= 0f
                )
                    return;
            string id = carrier.Unit.LaunchQueue[0];
            carrier.Unit.LaunchQueue.RemoveAt(0);
            if (
                carrier.Squadrons.TryGetValue(id, out CombatUnit fighter)
                && IsAlive(fighter)
                && fighter.FlightState == FighterFlightState.Queued
            )
            {
                int bayIndex = fighter.FlightBayIndex;
                Bay bay = carrier.Bays[bayIndex];
                float delay = 0f;
                float followingSpeed = ((Starfighter)fighter.GetUnit()).GetSublightSpeed();
                if (followingSpeed <= 0f)
                {
                    fighter.FlightState = FighterFlightState.Docked;
                }
                else
                {
                    foreach (CombatUnit preceding in carrier.Cargo)
                        if (
                            preceding != fighter
                            && IsAlive(preceding)
                            && preceding.FlightState == FighterFlightState.Launching
                            && preceding.FlightBayIndex == bayIndex
                        )
                        {
                            float speed = ((Starfighter)preceding.GetUnit()).GetSublightSpeed();
                            float spacing = GetRadius(preceding) + GetRadius(fighter);
                            float progress = GetPathDistance(preceding, bay.Path);
                            delay = Math.Max(
                                delay,
                                preceding.FlightDelay + (spacing - progress) / speed
                            );
                            delay = Math.Max(
                                delay,
                                preceding.FlightDelay
                                    + (bay.Length - progress) / speed
                                    - Math.Max(0f, bay.Length - spacing) / followingSpeed
                            );
                        }
                    fighter.FlightBayIndex = bayIndex;
                    fighter.FlightWaypointIndex = 0;
                    fighter.FlightSegmentDistance = 0f;
                    fighter.FlightDelay = delay;
                    fighter.FlightState = FighterFlightState.Launching;
                    fighter.MoveDestination = null;
                    SetVector(fighter.Position, ToWorld(carrier.Unit, bay.Path[0]));
                    SetVector(
                        fighter.Forward,
                        ToDirection(carrier.Unit, Vector3.Normalize(bay.Path[1] - bay.Path[0]))
                    );
                    SquadronLaunched?.Invoke(carrier.Unit);
                }
            }
            Revision++;
            if (carrier.Unit.LaunchQueue.Count > 0)
                carrier.Unit.LaunchCountdown = NextLaunchDelay();
        }

        /// <summary>
        /// Sends a deployed fighter home, or cancels its departure while it is still aboard.
        /// </summary>
        /// <param name="carrier">The assigned carrier.</param>
        /// <param name="fighter">The fighter receiving recall.</param>
        private void RecallFighter(Carrier carrier, CombatUnit fighter)
        {
            if (!IsAlive(carrier.Unit) || !IsAlive(fighter) || carrier.Bays.Length == 0)
                return;
            if (
                fighter.FlightState == FighterFlightState.Queued
                || (fighter.FlightState == FighterFlightState.Launching && fighter.FlightDelay > 0f)
            )
                fighter.FlightState = FighterFlightState.Docked;
            else if (fighter.FlightState == FighterFlightState.Launching)
                fighter.RecallRequested = true;
            else if (fighter.FlightState == FighterFlightState.Deployed)
                fighter.FlightState = FighterFlightState.Returning;
            else
                return;
            fighter.MoveDestination = null;
            if (fighter.FlightState == FighterFlightState.Docked)
            {
                fighter.RecallRequested = false;
                fighter.FlightDelay = 0f;
                carrier.Unit.LaunchQueue.Remove(fighter.SourceUnitInstanceID);
            }
            Revision++;
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
                CombatUnit fighter = bay.Returning[index];
                if (index > 0)
                    offset += GetRadius(bay.Returning[index - 1]) + GetRadius(fighter);
                Vector3 target = ToWorld(carrier.Unit, approach + outward * offset);
                fighter.MoveDestination ??= new BattleVector3();
                SetVector(fighter.MoveDestination, target);
                TacticalMovement.Advance(fighter, deltaTime);
                if (
                    index != 0
                    || bay.Outbound > 0
                    || bay.Inbound
                    || fighter.MoveDestination != null
                )
                    continue;
                fighter.FlightState = FighterFlightState.Docking;
                fighter.FlightWaypointIndex = bay.Path.Length - 1;
                fighter.FlightSegmentDistance = 0f;
                fighter.FlightDelay = 0f;
                fighter.RecallRequested = false;
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
            CombatUnit fighter,
            Vector3[] path,
            float deltaTime,
            bool inbound
        )
        {
            if (fighter.FlightDelay > 0f)
            {
                float waiting = Math.Min(deltaTime, fighter.FlightDelay);
                fighter.FlightDelay -= waiting;
                deltaTime -= waiting;
            }
            float travel = ((Starfighter)fighter.GetUnit()).GetSublightSpeed() * deltaTime;
            int step = inbound ? -1 : 1;
            while (true)
            {
                int next = fighter.FlightWaypointIndex + step;
                Vector3 segment = path[next] - path[fighter.FlightWaypointIndex];
                float length = segment.Length();
                float remaining = length - fighter.FlightSegmentDistance;
                if (travel < remaining)
                {
                    fighter.FlightSegmentDistance += travel;
                    SetVector(
                        fighter.Position,
                        ToWorld(
                            carrier.Unit,
                            path[fighter.FlightWaypointIndex]
                                + segment * (fighter.FlightSegmentDistance / length)
                        )
                    );
                    SetVector(fighter.Forward, ToDirection(carrier.Unit, segment / length));
                    return;
                }
                travel -= remaining;
                fighter.FlightWaypointIndex = next;
                fighter.FlightSegmentDistance = 0f;
                SetVector(fighter.Position, ToWorld(carrier.Unit, path[next]));
                SetVector(fighter.Forward, ToDirection(carrier.Unit, segment / length));
                if (next != (inbound ? 0 : path.Length - 1))
                    continue;
                fighter.FlightState =
                    inbound ? FighterFlightState.Docked
                    : fighter.RecallRequested ? FighterFlightState.Returning
                    : FighterFlightState.Deployed;
                fighter.MoveDestination =
                    inbound || fighter.RecallRequested
                        ? null
                        : FromVector(
                            ToWorld(carrier.Unit, ToVector(fighter.LaunchFormationOffset))
                        );
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
        private static float GetPathDistance(CombatUnit fighter, Vector3[] path)
        {
            float distance = fighter.FlightSegmentDistance;
            for (int index = 0; index < fighter.FlightWaypointIndex; index++)
                distance += Vector3.Distance(path[index], path[index + 1]);
            return distance;
        }

        /// <summary>
        /// Returns a conservative hull radius for spacing fighters through bay lanes.
        /// </summary>
        /// <param name="fighter">The measured fighter.</param>
        /// <returns>The radius of its enclosing sphere.</returns>
        private static float GetRadius(CombatUnit fighter)
        {
            var size = fighter.GetUnit().ModelSize;
            return new Vector3(size.Width, size.Height, size.Depth).Length() * 0.5f;
        }

        /// <summary>
        /// Checks owner authority and remaining tactical unit strength.
        /// </summary>
        /// <param name="unit">The requested unit.</param>
        /// <param name="owner">The ordering faction.</param>
        /// <returns>Whether this player controls a living unit.</returns>
        private static bool IsControlled(CombatUnit unit, string owner) =>
            !string.IsNullOrEmpty(owner)
            && unit.GetUnit().OwnerInstanceID == owner
            && IsAlive(unit);

        /// <summary>
        /// Checks withdrawal and recorded fighter or hull losses.
        /// </summary>
        /// <param name="unit">The battle unit.</param>
        /// <returns>Whether the unit remains available for carrier operations.</returns>
        private static bool IsAlive(CombatUnit unit) =>
            !unit.HasRetreated
            && unit.GetUnit() switch
            {
                CapitalShip ship => ship.MaxHullStrength <= 0 || ship.CurrentHullStrength > 0,
                Starfighter fighter => fighter.CurrentSquadronSize > 0
                    && (fighter.MaxHullStrength <= 0 || fighter.CurrentHullStrength > 0),
                _ => false,
            };

        /// <summary>
        /// Rotates a carrier-local direction into the battle's world axes without introducing roll.
        /// </summary>
        /// <param name="carrier">The carrier's saved pose.</param>
        /// <param name="local">The carrier-local direction.</param>
        /// <returns>The world-space direction.</returns>
        private static Vector3 ToDirection(CombatUnit carrier, Vector3 local)
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
        private static Vector3 ToLocal(CombatUnit carrier, Vector3 world) =>
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
        private static Vector3 ToWorld(CombatUnit carrier, Vector3 local) =>
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
            public readonly CombatUnit Unit;
            public readonly List<CombatUnit> Cargo = new List<CombatUnit>();
            public readonly IReadOnlyList<CombatUnit> Manifest;
            public readonly Dictionary<string, CombatUnit> Squadrons = new Dictionary<
                string,
                CombatUnit
            >(StringComparer.Ordinal);
            public readonly Bay[] Bays;

            /// <summary>
            /// Creates the indexed carrier manifest and validates authored numerical flight paths.
            /// </summary>
            /// <param name="unit">The carrier combatant.</param>
            /// <param name="paths">Its measured carrier-local bay routes.</param>
            public Carrier(CombatUnit unit, Vector3[][] paths)
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
            public readonly List<CombatUnit> Returning = new List<CombatUnit>();
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

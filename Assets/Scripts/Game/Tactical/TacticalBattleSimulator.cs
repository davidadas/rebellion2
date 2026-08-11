using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Rebellion.Game.Units;
using Rebellion.Util.Common;

namespace Rebellion.Game.Tactical
{
    /// <summary>
    /// Advances tactical movement, target engagement, firing arcs, and withdrawals.
    /// </summary>
    internal sealed class TacticalBattleSimulator
    {
        internal const float BattlefieldScale = 100f;
        private const float _formationSpacing = 8f;
        private const float _navigationArrivalDistance = 1f;
        private const float _tacticalApproachDistance = 20f;
        private static readonly Vector3[] SurroundDirections =
        {
            new Vector3(1f, -1f, 0f),
            Vector3.UnitZ,
            -Vector3.UnitZ,
            Vector3.UnitX,
            -Vector3.UnitX,
            Vector3.UnitY,
            -Vector3.UnitY,
            new Vector3(1f, 1f, 1f),
            new Vector3(-1f, -1f, -1f),
            new Vector3(-1f, 1f, 1f),
            new Vector3(1f, -1f, -1f),
            new Vector3(1f, -1f, 1f),
            new Vector3(-1f, 1f, -1f),
            new Vector3(-1f, -1f, 1f),
            new Vector3(1f, 1f, -1f),
            new Vector3(0f, -1f, -1f),
            new Vector3(0f, -1f, 1f),
            new Vector3(-1f, 1f, 0f),
            new Vector3(1f, 1f, 0f),
            new Vector3(0f, -1f, -1f),
            new Vector3(0f, 1f, 1f),
            new Vector3(-1f, -1f, 0f),
        };
        private readonly List<TacticalCombatEvent> events = new List<TacticalCombatEvent>();
        private readonly TacticalDeathStarAttackResolver deathStarAttackResolver;
        private readonly TacticalFighterDeploymentSystem fighterDeploymentSystem;
        private readonly TacticalSuperlaserSystem superlaserSystem;
        private readonly TacticalTractorBeamSystem tractorBeamSystem;
        private readonly HashSet<TacticalShipGroup> resolvedDeathStarAttackGroups =
            new HashSet<TacticalShipGroup>();
        private readonly IReadOnlyList<TacticalShipGroup> groups;
        private readonly IReadOnlyDictionary<TacticalBattleSide, float> fighterCommandBudgets;
        private readonly Dictionary<TacticalUnitState, TacticalUnitState> targets =
            new Dictionary<TacticalUnitState, TacticalUnitState>();
        private readonly IRandomNumberProvider random;
        private readonly IReadOnlyList<TacticalUnitState> units;
        private readonly Dictionary<TacticalUnitState, WithdrawalMotion> withdrawals =
            new Dictionary<TacticalUnitState, WithdrawalMotion>();

        private sealed class WithdrawalMotion
        {
            /// <summary>
            /// Gets the position from which the unit begins leaving the battlefield.
            /// </summary>
            public Vector3 Origin { get; }

            /// <summary>
            /// Gets the fixed direction in which the unit leaves its side of the battlefield.
            /// </summary>
            public Vector3 Direction { get; }

            /// <summary>
            /// Gets the distance covered by this unit's exit curve.
            /// </summary>
            public int Lane { get; }

            /// <summary>
            /// Gets or sets the elapsed time along the exit curve.
            /// </summary>
            public float ElapsedTime { get; set; }

            /// <summary>
            /// Initializes one unit's fixed tactical withdrawal route.
            /// </summary>
            /// <param name="origin">The position at which withdrawal begins.</param>
            /// <param name="direction">The normalized exit direction.</param>
            /// <param name="lane">The unit's stable flight-curve lane.</param>
            public WithdrawalMotion(Vector3 origin, Vector3 direction, int lane)
            {
                Origin = origin;
                Direction = direction;
                Lane = lane;
            }
        }

        private readonly struct PendingAttack
        {
            /// <summary>
            /// Gets the unit producing the attack.
            /// </summary>
            public TacticalUnitState Source { get; }

            /// <summary>
            /// Gets the unit receiving the attack.
            /// </summary>
            public TacticalUnitState Target { get; }

            /// <summary>
            /// Gets the weapon-family attack to resolve.
            /// </summary>
            public TacticalAttack Attack { get; }

            /// <summary>
            /// Initializes an attack that resolves after every unit has acted.
            /// </summary>
            /// <param name="source">The unit producing the attack.</param>
            /// <param name="target">The unit receiving the attack.</param>
            /// <param name="attack">The weapon-family attack.</param>
            public PendingAttack(
                TacticalUnitState source,
                TacticalUnitState target,
                TacticalAttack attack
            )
            {
                Source = source;
                Target = target;
                Attack = attack;
            }
        }

        /// <summary>
        /// Initializes the simulator and places both sides into opposing formations.
        /// </summary>
        /// <param name="units">The battle's tactical units.</param>
        /// <param name="groups">The battle's mutable command groups.</param>
        /// <param name="fighterCommandBudgets">The normalized fighter-command contribution for each side.</param>
        /// <param name="random">The battle's deterministic random source.</param>
        public TacticalBattleSimulator(
            IReadOnlyList<TacticalUnitState> units,
            IReadOnlyList<TacticalShipGroup> groups,
            IReadOnlyDictionary<TacticalBattleSide, float> fighterCommandBudgets,
            IRandomNumberProvider random
        )
        {
            this.units = units ?? throw new ArgumentNullException(nameof(units));
            this.groups = groups ?? throw new ArgumentNullException(nameof(groups));
            this.fighterCommandBudgets =
                fighterCommandBudgets
                ?? throw new ArgumentNullException(nameof(fighterCommandBudgets));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            deathStarAttackResolver = new TacticalDeathStarAttackResolver(random);
            fighterDeploymentSystem = new TacticalFighterDeploymentSystem(units, random);
            superlaserSystem = new TacticalSuperlaserSystem(units);
            tractorBeamSystem = new TacticalTractorBeamSystem();
            PlaceFormation(TacticalBattleSide.Attacker, -BattlefieldScale / 2f, Vector3.UnitZ);
            PlaceFormation(TacticalBattleSide.Defender, BattlefieldScale / 2f, -Vector3.UnitZ);
            PlaceGroupMarkers();
        }

        /// <summary>
        /// Advances all active objects through one tactical time interval.
        /// </summary>
        /// <param name="elapsedTime">The elapsed tactical time.</param>
        public void Advance(float elapsedTime)
        {
            superlaserSystem.Advance(elapsedTime);
            foreach (TacticalUnitState target in superlaserSystem.DrainResolvedTargets())
            {
                target.Hull = 0;
                events.Add(
                    TacticalCombatEvent.UnitLifecycle(TacticalCombatEventKind.UnitDestroyed, target)
                );
            }
            fighterDeploymentSystem.ResolveCarrierStateChanges();
            fighterDeploymentSystem.Advance(elapsedTime);
            events.AddRange(fighterDeploymentSystem.DrainEvents());
            UpdateTractorLocks();
            events.AddRange(tractorBeamSystem.DrainEvents());
            List<PendingAttack> attacks = new List<PendingAttack>();
            foreach (TacticalUnitState unit in units.Where(unit => unit.IsActive).ToArray())
                attacks.AddRange(AdvanceUnit(unit, elapsedTime));

            foreach (PendingAttack attack in attacks)
            {
                bool targetWasActive = attack.Target.IsActive;
                int shieldsBeforeImpact = attack.Target.Shields;
                attack.Target.ApplyDamage(attack.Attack, random);
                TacticalImpactState impactState =
                    !attack.Target.IsActive ? TacticalImpactState.Destroyed
                    : attack.Attack.Strength > shieldsBeforeImpact ? TacticalImpactState.Hull
                    : TacticalImpactState.Shield;
                events.Add(
                    TacticalCombatEvent.WeaponImpact(
                        attack.Source,
                        attack.Target,
                        attack.Attack.WeaponType,
                        impactState
                    )
                );
                if (targetWasActive && !attack.Target.IsActive)
                {
                    events.Add(
                        TacticalCombatEvent.UnitLifecycle(
                            TacticalCombatEventKind.UnitDestroyed,
                            attack.Target
                        )
                    );
                }
            }

            fighterDeploymentSystem.ResolveCarrierStateChanges();
            tractorBeamSystem.ReleaseInvalidLocks();
            events.AddRange(tractorBeamSystem.DrainEvents());
        }

        /// <summary>
        /// Reconciles each active tractor source with its current command target.
        /// </summary>
        private void UpdateTractorLocks()
        {
            tractorBeamSystem.ReleaseInvalidLocks();
            foreach (TacticalUnitState unit in units.Where(unit => unit.IsActive))
            {
                TacticalShipGroup group = groups.LastOrDefault(candidate =>
                    candidate.Units.Contains(unit)
                );
                TacticalBehavior behavior = group?.Behavior ?? TacticalBehavior.PrimaryTarget;
                TacticalUnitState target =
                    behavior == TacticalBehavior.Withdraw
                    || behavior == TacticalBehavior.Recover
                    || behavior == TacticalBehavior.AttackDeathStar
                        ? null
                        : GetTarget(unit, group, behavior);
                tractorBeamSystem.UpdateLock(unit, target);
            }
        }

        /// <summary>
        /// Gets one participating Death Star's current superlaser charge percentage.
        /// </summary>
        /// <param name="deathStar">The Death Star whose charge is requested.</param>
        /// <returns>The current charge from zero through one hundred.</returns>
        public float GetSuperlaserCharge(TacticalUnitState deathStar)
        {
            return superlaserSystem.GetCharge(deathStar);
        }

        /// <summary>
        /// Fires one charged Death Star at an active opposing tactical object.
        /// </summary>
        /// <param name="deathStar">The firing Death Star.</param>
        /// <param name="target">The selected opposing target.</param>
        /// <returns>True when the shot fires.</returns>
        public bool TryFireSuperlaser(TacticalUnitState deathStar, TacticalUnitState target)
        {
            if (!superlaserSystem.TryFire(deathStar, target))
                return false;

            events.Add(TacticalCombatEvent.SuperlaserFired(deathStar, target));
            return true;
        }

        /// <summary>
        /// Removes and returns every presentation event produced since the previous drain.
        /// </summary>
        /// <returns>The events in simulation order.</returns>
        public IReadOnlyList<TacticalCombatEvent> DrainEvents()
        {
            TacticalCombatEvent[] result = events.ToArray();
            events.Clear();
            return result;
        }

        /// <summary>
        /// Advances one unit's command, movement, and attack decision.
        /// </summary>
        /// <param name="unit">The unit to advance.</param>
        /// <param name="elapsedTime">The elapsed tactical time.</param>
        /// <returns>The attacks to resolve after every unit has acted.</returns>
        private IReadOnlyList<PendingAttack> AdvanceUnit(TacticalUnitState unit, float elapsedTime)
        {
            TacticalShipGroup group = groups.LastOrDefault(candidate =>
                candidate.Units.Contains(unit)
            );
            TacticalBehavior behavior = group?.Behavior ?? TacticalBehavior.PrimaryTarget;
            if (behavior == TacticalBehavior.Withdraw)
            {
                AdvanceWithdrawal(unit, elapsedTime);
                return Array.Empty<PendingAttack>();
            }
            if (unit.Kind == TacticalUnitKind.Fighters && behavior == TacticalBehavior.Recover)
            {
                AdvanceRecovery(unit, group, elapsedTime);
                return Array.Empty<PendingAttack>();
            }
            if (behavior == TacticalBehavior.AttackDeathStar)
            {
                AdvanceDeathStarAttack(unit, group, elapsedTime);
                return Array.Empty<PendingAttack>();
            }
            if (TryAdvanceNavigation(unit, group, elapsedTime))
                return Array.Empty<PendingAttack>();

            TacticalUnitState target = GetTarget(unit, group, behavior);
            if (target == null)
                return Array.Empty<PendingAttack>();

            IReadOnlyList<PendingAttack> attacks = FireStrongestArc(unit, target);
            if (behavior == TacticalBehavior.Hold)
                return attacks;

            Vector3 destination = GetApproachPosition(
                unit,
                target,
                group,
                behavior,
                out Vector3 markerPosition
            );
            group?.SetMarkerPosition(markerPosition);
            MoveTowards(unit, destination, elapsedTime);
            return attacks;
        }

        /// <summary>
        /// Advances a fighter group through its dedicated Death Star attack run.
        /// </summary>
        /// <param name="unit">The group member currently being advanced.</param>
        /// <param name="group">The fighter group performing the attack.</param>
        /// <param name="elapsedTime">The elapsed tactical time.</param>
        private void AdvanceDeathStarAttack(
            TacticalUnitState unit,
            TacticalShipGroup group,
            float elapsedTime
        )
        {
            if (
                unit.Kind != TacticalUnitKind.Fighters
                || group == null
                || resolvedDeathStarAttackGroups.Contains(group)
            )
            {
                return;
            }

            TacticalUnitState deathStar = GetTarget(unit, group, TacticalBehavior.AttackDeathStar);
            if (deathStar == null)
                return;

            if (Vector3.Distance(unit.Position, deathStar.Position) > _tacticalApproachDistance)
            {
                Vector3 destination = GetApproachPosition(
                    unit,
                    deathStar,
                    group,
                    TacticalBehavior.AttackDeathStar,
                    out Vector3 markerPosition
                );
                group.SetMarkerPosition(markerPosition);
                MoveTowards(unit, destination, elapsedTime);
                return;
            }

            resolvedDeathStarAttackGroups.Add(group);
            TacticalUnitState[] participants = group
                .Units.Where(candidate => candidate.IsActive)
                .ToArray();
            Dictionary<TacticalUnitState, bool> activeBefore = participants.ToDictionary(
                candidate => candidate,
                candidate => candidate.IsActive
            );
            bool succeeded = deathStarAttackResolver.Resolve(
                participants,
                fighterCommandBudgets.TryGetValue(group.Side, out float commandBudget)
                    ? commandBudget
                    : 1f
            );
            foreach (TacticalUnitState participant in participants)
            {
                if (activeBefore[participant] && !participant.IsActive)
                {
                    events.Add(
                        TacticalCombatEvent.UnitLifecycle(
                            TacticalCombatEventKind.UnitDestroyed,
                            participant
                        )
                    );
                }
            }

            if (!succeeded)
                return;

            deathStar.Hull = 0;
            events.Add(
                TacticalCombatEvent.UnitLifecycle(TacticalCombatEventKind.UnitDestroyed, deathStar)
            );
        }

        /// <summary>
        /// Retains the unit's current engagement or acquires the highest-priority eligible object.
        /// </summary>
        /// <param name="unit">The acting unit.</param>
        /// <param name="group">The unit's controlling group, if assigned.</param>
        /// <param name="behavior">The group's active behavior.</param>
        /// <returns>The selected opposing target, or null when none is eligible.</returns>
        private TacticalUnitState GetTarget(
            TacticalUnitState unit,
            TacticalShipGroup group,
            TacticalBehavior behavior
        )
        {
            if (
                behavior == TacticalBehavior.AttackDeathStar
                && unit.Kind != TacticalUnitKind.Fighters
            )
            {
                targets.Remove(unit);
                return null;
            }

            IEnumerable<TacticalUnitState> candidates =
                group?.Targets.Count > 0
                    ? group.Targets
                    : units.Where(candidate => candidate.Side != unit.Side);
            candidates = candidates.Where(candidate => candidate.IsActive);
            if (behavior == TacticalBehavior.AttackFighters)
                candidates = candidates.Where(candidate =>
                    candidate.Kind == TacticalUnitKind.Fighters
                );
            else if (behavior == TacticalBehavior.AttackCapitalShips)
                candidates = candidates.Where(candidate =>
                    candidate.Kind == TacticalUnitKind.CapitalShip
                );
            else if (behavior == TacticalBehavior.AttackDeathStar)
                candidates = candidates.Where(candidate =>
                    candidate.Unit is CapitalShip { IsDeathStar: true }
                );

            TacticalUnitState[] eligibleTargets = candidates.ToArray();
            if (
                targets.TryGetValue(unit, out TacticalUnitState currentTarget)
                && eligibleTargets.Contains(currentTarget)
            )
            {
                return currentTarget;
            }

            TacticalUnitState selectedTarget =
                group?.Targets.Count > 0
                    ? eligibleTargets.FirstOrDefault()
                    : eligibleTargets
                        .OrderBy(candidate =>
                            Vector3.DistanceSquared(unit.Position, candidate.Position)
                        )
                        .FirstOrDefault();
            if (selectedTarget == null)
                targets.Remove(unit);
            else
                targets[unit] = selectedTarget;

            return selectedTarget;
        }

        /// <summary>
        /// Selects and discharges the strongest charged arc that can reach an eligible target.
        /// </summary>
        /// <param name="attacker">The acting unit.</param>
        /// <param name="target">The engaged opposing target.</param>
        /// <returns>The pending attack, if an arc can fire.</returns>
        private static IReadOnlyList<PendingAttack> FireStrongestArc(
            TacticalUnitState attacker,
            TacticalUnitState target
        )
        {
            float distance = Vector3.Distance(attacker.Position, target.Position);
            TacticalWeaponArc arc = GetFiringArc(attacker, target.Position);
            if (attacker.GetAvailableAttackStrength(arc, distance) <= 0)
                return Array.Empty<PendingAttack>();

            List<PendingAttack> attacks = attacker
                .FireArc(arc, distance)
                .Select(attack => new PendingAttack(attacker, target, attack))
                .ToList();
            if (
                attacker.Kind == TacticalUnitKind.Fighters
                && target.Kind == TacticalUnitKind.CapitalShip
                && target.Shields == 0
                && distance <= attacker.TorpedoRange
                && attacks.Any(attack => attack.Attack.WeaponType == TacticalWeaponType.LaserCannon)
            )
            {
                int torpedoStrength = attacker.GetTorpedoAttackStrength();
                if (torpedoStrength > 0)
                {
                    attacks.Add(
                        new PendingAttack(
                            attacker,
                            target,
                            new TacticalAttack(TacticalWeaponType.Torpedo, torpedoStrength)
                        )
                    );
                }
            }

            return attacks;
        }

        /// <summary>
        /// Classifies a target into the attacker's fore, aft, port, or starboard arc.
        /// </summary>
        /// <param name="attacker">The acting unit.</param>
        /// <param name="targetPosition">The target's tactical position.</param>
        /// <returns>The firing arc containing the target.</returns>
        private static TacticalWeaponArc GetFiringArc(
            TacticalUnitState attacker,
            Vector3 targetPosition
        )
        {
            Vector3 direction = NormalizeOrDefault(
                targetPosition - attacker.Position,
                attacker.Forward
            );
            Vector3 forward = NormalizeOrDefault(attacker.Forward, Vector3.UnitZ);
            Vector3 right = NormalizeOrDefault(
                Vector3.Cross(Vector3.UnitY, forward),
                Vector3.UnitX
            );
            float forwardAmount = Vector3.Dot(direction, forward);
            float rightAmount = Vector3.Dot(direction, right);
            if (Math.Abs(forwardAmount) >= Math.Abs(rightAmount))
                return forwardAmount >= 0f ? TacticalWeaponArc.Fore : TacticalWeaponArc.Aft;

            return rightAmount >= 0f ? TacticalWeaponArc.Starboard : TacticalWeaponArc.Port;
        }

        /// <summary>
        /// Produces the command-specific approach point around a target.
        /// </summary>
        /// <param name="unit">The unit approaching the target.</param>
        /// <param name="target">The target being approached.</param>
        /// <param name="group">The unit's controlling group.</param>
        /// <param name="behavior">The active approach behavior.</param>
        /// <param name="markerPosition">The resulting center of the commanded formation.</param>
        /// <returns>The desired tactical position.</returns>
        private static Vector3 GetApproachPosition(
            TacticalUnitState unit,
            TacticalUnitState target,
            TacticalShipGroup group,
            TacticalBehavior behavior,
            out Vector3 markerPosition
        )
        {
            Vector3 approachDirection = NormalizeOrDefault(
                target.Position - unit.Position,
                unit.Forward
            );
            Vector3 right = NormalizeOrDefault(
                Vector3.Cross(Vector3.UnitY, approachDirection),
                Vector3.UnitX
            );
            Vector3 maneuverOffset = behavior switch
            {
                TacticalBehavior.LeftHook => -right * _tacticalApproachDistance,
                TacticalBehavior.RightHook => right * _tacticalApproachDistance,
                TacticalBehavior.Hammer => -Vector3.UnitY * _tacticalApproachDistance,
                TacticalBehavior.Anvil => Vector3.UnitY * _tacticalApproachDistance,
                _ => -approachDirection * _tacticalApproachDistance,
            };
            markerPosition = target.Position + maneuverOffset;
            return markerPosition + GetFormationOffset(unit, group, approachDirection, right);
        }

        /// <summary>
        /// Computes a source-ordered member offset for the group's active formation.
        /// </summary>
        /// <param name="unit">The group member being positioned.</param>
        /// <param name="group">The controlling group.</param>
        /// <param name="forward">The group's approach direction.</param>
        /// <param name="right">The group's horizontal right direction.</param>
        /// <returns>The member's offset from the group center.</returns>
        private static Vector3 GetFormationOffset(
            TacticalUnitState unit,
            TacticalShipGroup group,
            Vector3 forward,
            Vector3 right
        )
        {
            if (group == null)
                return Vector3.Zero;

            int index = -1;
            for (int i = 0; i < group.Units.Count; i++)
            {
                if (ReferenceEquals(group.Units[i], unit))
                {
                    index = i;
                    break;
                }
            }
            if (index < 0)
                return Vector3.Zero;
            if (group.Formation == TacticalFormation.StandOff)
            {
                float centeredIndex = index - (group.Units.Count - 1) / 2f;
                return right * centeredIndex * _formationSpacing;
            }

            Vector3 localDirection = Vector3.Normalize(
                SurroundDirections[index % SurroundDirections.Length]
            );
            int shell = index / SurroundDirections.Length;
            float radius = _formationSpacing * (shell + 1);
            Vector3 up = Vector3.UnitY;
            return (right * localDirection.X + up * localDirection.Y + forward * localDirection.Z)
                * radius;
        }

        /// <summary>
        /// Turns and advances one unit toward a tactical position.
        /// </summary>
        /// <param name="unit">The moving unit.</param>
        /// <param name="destination">The desired tactical position.</param>
        /// <param name="elapsedTime">The elapsed tactical time.</param>
        private void MoveTowards(TacticalUnitState unit, Vector3 destination, float elapsedTime)
        {
            Vector3 displacement = destination - unit.Position;
            float distance = displacement.Length();
            float movementSpeed = tractorBeamSystem.GetMovementSpeed(unit);
            if (distance <= _navigationArrivalDistance || movementSpeed <= 0f)
                return;

            Vector3 desiredForward = displacement / distance;
            float turnAmount = Math.Min(1f, unit.Maneuverability * elapsedTime);
            unit.Forward = NormalizeOrDefault(
                Vector3.Lerp(unit.Forward, desiredForward, turnAmount),
                desiredForward
            );
            float movement = Math.Min(distance, movementSpeed * elapsedTime);
            Vector3 candidatePosition = unit.Position + unit.Forward * movement;
            if (!TryFindCollision(unit, candidatePosition, out TacticalUnitState obstacle))
            {
                unit.Position = candidatePosition;
                return;
            }

            float clearance = unit.VerticalExtent + obstacle.VerticalExtent;
            Vector3 upperPosition = new Vector3(
                candidatePosition.X,
                obstacle.Position.Y + clearance,
                candidatePosition.Z
            );
            Vector3 lowerPosition = new Vector3(
                candidatePosition.X,
                obstacle.Position.Y - clearance,
                candidatePosition.Z
            );
            Vector3 firstClearance =
                Math.Abs(upperPosition.Y - candidatePosition.Y)
                <= Math.Abs(lowerPosition.Y - candidatePosition.Y)
                    ? upperPosition
                    : lowerPosition;
            Vector3 secondClearance =
                firstClearance == upperPosition ? lowerPosition : upperPosition;
            if (!TryFindCollision(unit, firstClearance, out _))
                unit.Position = firstClearance;
            else if (!TryFindCollision(unit, secondClearance, out _))
                unit.Position = secondClearance;
        }

        /// <summary>
        /// Finds an active tactical object overlapping a unit at a prospective position.
        /// </summary>
        /// <param name="unit">The moving unit.</param>
        /// <param name="position">The prospective center position.</param>
        /// <param name="obstacle">The first overlapping object, when found.</param>
        /// <returns>True when the prospective position overlaps another active object.</returns>
        private bool TryFindCollision(
            TacticalUnitState unit,
            Vector3 position,
            out TacticalUnitState obstacle
        )
        {
            obstacle = null;
            if (unit.HorizontalExtent <= 0f || unit.VerticalExtent <= 0f)
                return false;

            foreach (TacticalUnitState candidate in units)
            {
                if (
                    ReferenceEquals(candidate, unit)
                    || !candidate.IsActive
                    || candidate.HorizontalExtent <= 0f
                    || candidate.VerticalExtent <= 0f
                )
                    continue;

                float verticalDistance = Math.Abs(position.Y - candidate.Position.Y);
                if (verticalDistance >= unit.VerticalExtent + candidate.VerticalExtent)
                    continue;

                float x = position.X - candidate.Position.X;
                float z = position.Z - candidate.Position.Z;
                float horizontalClearance = unit.HorizontalExtent + candidate.HorizontalExtent;
                if (x * x + z * z >= horizontalClearance * horizontalClearance)
                    continue;

                obstacle = candidate;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Normalizes a vector while preserving a usable fallback for a zero-length value.
        /// </summary>
        /// <param name="value">The vector to normalize.</param>
        /// <param name="defaultValue">The fallback direction.</param>
        /// <returns>The normalized vector or fallback.</returns>
        private static Vector3 NormalizeOrDefault(Vector3 value, Vector3 defaultValue)
        {
            return value.LengthSquared() > 0f ? Vector3.Normalize(value) : defaultValue;
        }

        /// <summary>
        /// Advances the first waypoint assigned to a unit's command group.
        /// </summary>
        /// <param name="unit">The moving unit.</param>
        /// <param name="group">The controlling group.</param>
        /// <param name="elapsedTime">The elapsed tactical time.</param>
        /// <returns>True when navigation consumes the unit's action.</returns>
        private bool TryAdvanceNavigation(
            TacticalUnitState unit,
            TacticalShipGroup group,
            float elapsedTime
        )
        {
            if (group == null || group.NavigationPoints.Count == 0)
                return false;

            TacticalNavPoint point = group.NavigationPoints[0];
            Vector3 destination = new Vector3(point.X, point.Y, point.Z);
            group.SetMarkerPosition(destination);
            if (Vector3.Distance(unit.Position, destination) <= _navigationArrivalDistance)
                group.RemoveNavigationPoint(point);
            else
                MoveTowards(unit, destination, elapsedTime);

            return true;
        }

        /// <summary>
        /// Returns a fighter group to its deploying capital ship or formation marker.
        /// </summary>
        /// <param name="unit">The recovering fighter unit.</param>
        /// <param name="group">The fighter's command group.</param>
        /// <param name="elapsedTime">The elapsed tactical time.</param>
        private void AdvanceRecovery(
            TacticalUnitState unit,
            TacticalShipGroup group,
            float elapsedTime
        )
        {
            TacticalUnitState carrier = unit.RecoveryTarget;
            if (carrier?.IsActive != true)
            {
                if (group == null)
                    return;

                Vector3 forward = NormalizeOrDefault(unit.Forward, Vector3.UnitZ);
                Vector3 right = NormalizeOrDefault(
                    Vector3.Cross(Vector3.UnitY, forward),
                    Vector3.UnitX
                );
                Vector3 destination =
                    group.MarkerPosition + GetFormationOffset(unit, group, forward, right);
                MoveTowards(unit, destination, elapsedTime);
                return;
            }
            if (Vector3.Distance(unit.Position, carrier.Position) <= _navigationArrivalDistance)
            {
                unit.BeginWithdrawal();
                unit.CompleteWithdrawal();
                events.Add(
                    TacticalCombatEvent.UnitLifecycle(
                        TacticalCombatEventKind.FightersRecovered,
                        unit
                    )
                );
                return;
            }

            MoveTowards(unit, carrier.Position, elapsedTime);
        }

        /// <summary>
        /// Advances a unit along its side's tactical withdrawal route.
        /// </summary>
        /// <param name="unit">The withdrawing unit.</param>
        /// <param name="elapsedTime">The elapsed tactical time.</param>
        private void AdvanceWithdrawal(TacticalUnitState unit, float elapsedTime)
        {
            if (!unit.CanWithdraw)
                return;

            float movementSpeed = tractorBeamSystem.GetMovementSpeed(unit);
            if (movementSpeed <= 0f)
                return;

            if (!withdrawals.TryGetValue(unit, out WithdrawalMotion withdrawal))
            {
                int unitIndex = GetUnitIndex(unit);
                Vector3 direction =
                    unit.Side == TacticalBattleSide.Attacker ? -Vector3.UnitZ : Vector3.UnitZ;
                withdrawal = new WithdrawalMotion(unit.Position, direction, unitIndex);
                withdrawals.Add(unit, withdrawal);
                unit.BeginWithdrawal();
            }

            unit.Forward = withdrawal.Direction;
            withdrawal.ElapsedTime = Math.Min(
                TacticalFlightCurve.WithdrawalDuration,
                withdrawal.ElapsedTime + elapsedTime
            );
            unit.Position =
                withdrawal.Origin
                + withdrawal.Direction
                    * TacticalFlightCurve.GetWithdrawalDistance(
                        withdrawal.Lane,
                        withdrawal.ElapsedTime
                    );
            if (withdrawal.ElapsedTime >= TacticalFlightCurve.WithdrawalDuration)
            {
                unit.CompleteWithdrawal();
                events.Add(
                    TacticalCombatEvent.UnitLifecycle(TacticalCombatEventKind.UnitWithdrawn, unit)
                );
                withdrawals.Remove(unit);
            }
        }

        /// <summary>
        /// Gets the stable battle-local index used to select a unit's presentation curve.
        /// </summary>
        /// <param name="unit">The tactical unit to locate.</param>
        /// <returns>The unit's index in battle order.</returns>
        private int GetUnitIndex(TacticalUnitState unit)
        {
            for (int index = 0; index < units.Count; index++)
            {
                if (ReferenceEquals(units[index], unit))
                    return index;
            }

            throw new InvalidOperationException(
                "The tactical unit does not belong to this battle."
            );
        }

        /// <summary>
        /// Places one side into a centered line facing the opposing formation.
        /// </summary>
        /// <param name="side">The side to place.</param>
        /// <param name="z">The formation's depth coordinate.</param>
        /// <param name="forward">The formation's facing direction.</param>
        private void PlaceFormation(TacticalBattleSide side, float z, Vector3 forward)
        {
            TacticalUnitState[] sideUnits = units.Where(unit => unit.Side == side).ToArray();
            for (int i = 0; i < sideUnits.Length; i++)
            {
                float centeredIndex = i - (sideUnits.Length - 1) / 2f;
                sideUnits[i].Position = new Vector3(centeredIndex * _formationSpacing, 0f, z);
                sideUnits[i].Forward = forward;
            }
        }

        /// <summary>
        /// Centers each command marker on the units initially assigned to its group.
        /// </summary>
        private void PlaceGroupMarkers()
        {
            foreach (TacticalShipGroup group in groups)
            {
                if (group.Units.Count == 0)
                    continue;

                Vector3 total = Vector3.Zero;
                foreach (TacticalUnitState unit in group.Units)
                    total += unit.Position;

                group.SetMarkerPosition(total / group.Units.Count);
            }
        }
    }
}

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
        private const float _capitalFormationDepth = BattlefieldScale / 2f;
        private const float _fighterFormationDepth = BattlefieldScale * 0.65f;
        private const float _fighterRecoveryDistance = 2f;
        private const float _formationSpacing = 8f;
        private const float _maneuverAngle = (float)(Math.PI / 8d);
        private const float _maneuverDistanceScale = 0.75f;
        private const float _navigationArrivalDistance = 1f;
        private const int _stableMarkerRefreshesForDetour = 5;
        private const float _temporaryDetourTimeout = 12f;
        private static readonly Vector3[] TemporaryDetourOffsets =
        {
            new Vector3(20f, 0f, 0f),
            new Vector3(-20f, 10f, 5f),
            new Vector3(5f, 0f, 20f),
            new Vector3(0f, -10f, -20f),
        };
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
        private readonly TacticalDeathStarAttackSystem deathStarAttackSystem;
        private readonly TacticalFighterDeploymentSystem fighterDeploymentSystem;
        private readonly TacticalSuperlaserSystem superlaserSystem;
        private readonly TacticalTractorBeamSystem tractorBeamSystem;
        private readonly Func<TacticalShipGroup, bool> isDeathStarAttackOrderValid;
        private readonly IReadOnlyList<TacticalShipGroup> groups;
        private readonly IReadOnlyDictionary<TacticalBattleSide, float> fighterCommandBudgets;
        private readonly IReadOnlyDictionary<TacticalBattleSide, float> capitalCommandBudgets;
        private readonly Dictionary<TacticalShipGroup, ManeuverOrder> maneuverOrders =
            new Dictionary<TacticalShipGroup, ManeuverOrder>();
        private readonly TacticalNavigationGrid navigationGrid;
        private readonly Dictionary<TacticalUnitState, TacticalUnitState> targets =
            new Dictionary<TacticalUnitState, TacticalUnitState>();
        private readonly Dictionary<TacticalUnitState, CollisionAvoidanceState> collisionAvoidance =
            new Dictionary<TacticalUnitState, CollisionAvoidanceState>();
        private readonly Dictionary<TacticalShipGroup, MarkerStabilityState> markerStability =
            new Dictionary<TacticalShipGroup, MarkerStabilityState>();
        private readonly IRandomNumberProvider random;
        private readonly IReadOnlyList<TacticalUnitState> units;
        private float tacticalTime;
        private readonly Dictionary<TacticalUnitState, WithdrawalMotion> withdrawals =
            new Dictionary<TacticalUnitState, WithdrawalMotion>();

        private sealed class ManeuverOrder
        {
            /// <summary>
            /// Gets the group command revision from which this marker was calculated.
            /// </summary>
            public int CommandRevision { get; }

            /// <summary>
            /// Gets the opposing tactical object around which the maneuver was calculated.
            /// </summary>
            public TacticalUnitState Target { get; }

            /// <summary>
            /// Gets the navigation anchor selected for the maneuver.
            /// </summary>
            public Vector3 Marker { get; }

            /// <summary>
            /// Gets the group center from which the maneuver was calculated.
            /// </summary>
            public Vector3 Origin { get; }

            /// <summary>
            /// Initializes one resolved group maneuver.
            /// </summary>
            /// <param name="commandRevision">The command revision being resolved.</param>
            /// <param name="target">The maneuver's opposing target.</param>
            /// <param name="origin">The group center when the command was issued.</param>
            /// <param name="marker">The selected navigation anchor.</param>
            public ManeuverOrder(
                int commandRevision,
                TacticalUnitState target,
                Vector3 origin,
                Vector3 marker
            )
            {
                CommandRevision = commandRevision;
                Target = target;
                Origin = origin;
                Marker = marker;
            }
        }

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

        private sealed class CollisionAvoidanceState
        {
            /// <summary>
            /// Gets or sets whether vertical clearance failed during the previous update.
            /// </summary>
            public bool VerticalClearanceBlocked { get; set; }

            /// <summary>
            /// Gets or sets the active temporary destination offset.
            /// </summary>
            public Vector3? TemporaryOffset { get; set; }

            /// <summary>
            /// Gets or sets the next offset phase used by this unit.
            /// </summary>
            public int Phase { get; set; }

            /// <summary>
            /// Gets or sets the tactical time at which the temporary offset last changed.
            /// </summary>
            public float LastChangeTime { get; set; }
        }

        private sealed class MarkerStabilityState
        {
            /// <summary>
            /// Gets or sets the marker position observed during the previous update.
            /// </summary>
            public Vector3 Position { get; set; }

            /// <summary>
            /// Gets or sets the number of consecutive refreshes at the same position.
            /// </summary>
            public int RefreshCount { get; set; }
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

        private sealed class CapitalArcAttackPlan
        {
            private readonly Dictionary<TacticalWeaponType, WeaponTarget> targets =
                new Dictionary<TacticalWeaponType, WeaponTarget>();

            /// <summary>
            /// Gets the firing arc represented by this plan.
            /// </summary>
            public TacticalWeaponArc Arc { get; }

            /// <summary>
            /// Gets the combined strength of the best target selected for each weapon family.
            /// </summary>
            public int Strength => targets.Values.Sum(target => target.Strength);

            /// <summary>
            /// Gets the weapon families assigned to targets in this arc.
            /// </summary>
            public IReadOnlyCollection<TacticalWeaponType> WeaponTypes => targets.Keys;

            /// <summary>
            /// Initializes an empty attack plan for one firing arc.
            /// </summary>
            /// <param name="arc">The firing arc represented by the plan.</param>
            public CapitalArcAttackPlan(TacticalWeaponArc arc)
            {
                Arc = arc;
            }

            /// <summary>
            /// Retains a target when it is stronger than the current target for one weapon family.
            /// </summary>
            /// <param name="weaponType">The weapon family being assigned.</param>
            /// <param name="target">The prospective target.</param>
            /// <param name="strength">The prospective attack strength.</param>
            public void Consider(
                TacticalWeaponType weaponType,
                TacticalUnitState target,
                int strength
            )
            {
                if (
                    strength <= 0
                    || targets.TryGetValue(weaponType, out WeaponTarget current)
                        && current.Strength >= strength
                )
                {
                    return;
                }

                targets[weaponType] = new WeaponTarget(target, strength);
            }

            /// <summary>
            /// Gets the target selected for one weapon family.
            /// </summary>
            /// <param name="weaponType">The weapon family whose target is requested.</param>
            /// <returns>The selected tactical target.</returns>
            public TacticalUnitState GetTarget(TacticalWeaponType weaponType)
            {
                return targets[weaponType].Target;
            }
        }

        private readonly struct WeaponTarget
        {
            /// <summary>
            /// Gets the selected tactical target.
            /// </summary>
            public TacticalUnitState Target { get; }

            /// <summary>
            /// Gets the target score for its assigned weapon family.
            /// </summary>
            public int Strength { get; }

            /// <summary>
            /// Initializes one weapon-family target selection.
            /// </summary>
            /// <param name="target">The selected tactical target.</param>
            /// <param name="strength">The target's weapon-family score.</param>
            public WeaponTarget(TacticalUnitState target, int strength)
            {
                Target = target;
                Strength = strength;
            }
        }

        /// <summary>
        /// Initializes the simulator and places both sides into opposing formations.
        /// </summary>
        /// <param name="units">The battle's tactical units.</param>
        /// <param name="groups">The battle's mutable command groups.</param>
        /// <param name="navigationGrid">The battle's fixed navigation-anchor lattice.</param>
        /// <param name="fighterCommandBudgets">The normalized fighter-command contribution for each side.</param>
        /// <param name="capitalCommandBudgets">The normalized capital-command contribution for each side.</param>
        /// <param name="isDeathStarAttackOrderValid">Tests whether an assigned Death Star attack remains valid.</param>
        /// <param name="random">The battle's deterministic random source.</param>
        public TacticalBattleSimulator(
            IReadOnlyList<TacticalUnitState> units,
            IReadOnlyList<TacticalShipGroup> groups,
            TacticalNavigationGrid navigationGrid,
            IReadOnlyDictionary<TacticalBattleSide, float> fighterCommandBudgets,
            IReadOnlyDictionary<TacticalBattleSide, float> capitalCommandBudgets,
            Func<TacticalShipGroup, bool> isDeathStarAttackOrderValid,
            IRandomNumberProvider random
        )
        {
            this.units = units ?? throw new ArgumentNullException(nameof(units));
            this.groups = groups ?? throw new ArgumentNullException(nameof(groups));
            this.navigationGrid =
                navigationGrid ?? throw new ArgumentNullException(nameof(navigationGrid));
            this.fighterCommandBudgets =
                fighterCommandBudgets
                ?? throw new ArgumentNullException(nameof(fighterCommandBudgets));
            this.capitalCommandBudgets =
                capitalCommandBudgets
                ?? throw new ArgumentNullException(nameof(capitalCommandBudgets));
            this.isDeathStarAttackOrderValid =
                isDeathStarAttackOrderValid
                ?? throw new ArgumentNullException(nameof(isDeathStarAttackOrderValid));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            deathStarAttackSystem = new TacticalDeathStarAttackSystem(
                new TacticalDeathStarAttackResolver(random),
                fighterCommandBudgets
            );
            fighterDeploymentSystem = new TacticalFighterDeploymentSystem(units, random);
            superlaserSystem = new TacticalSuperlaserSystem(units);
            tractorBeamSystem = new TacticalTractorBeamSystem();
            PlaceFormation(TacticalBattleSide.Attacker, -1f, Vector3.UnitZ);
            PlaceFormation(TacticalBattleSide.Defender, 1f, -Vector3.UnitZ);
            PlaceGroupMarkers();
        }

        /// <summary>
        /// Advances all active objects through one tactical time interval.
        /// </summary>
        /// <param name="elapsedTime">The elapsed tactical time.</param>
        public void Advance(float elapsedTime)
        {
            tacticalTime += elapsedTime;
            RefreshCollisionAvoidance();
            deathStarAttackSystem.Advance(elapsedTime);
            events.AddRange(deathStarAttackSystem.DrainEvents());
            superlaserSystem.Advance(elapsedTime);
            foreach (
                TacticalSuperlaserSystem.ResolvedShot shot in superlaserSystem.DrainResolvedShots()
            )
            {
                shot.Target.Hull = 0;
                events.Add(TacticalCombatEvent.UnitDestroyed(shot.Source, shot.Target));
            }
            foreach (TacticalUnitState deathStar in superlaserSystem.DrainReadyDeathStars())
            {
                events.Add(
                    TacticalCombatEvent.UnitLifecycle(
                        TacticalCombatEventKind.SuperlaserReady,
                        deathStar
                    )
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
                TacticalAttack resolvedAttack = AdjustAttackForFighterEvasion(attack);
                attack.Target.ApplyDamage(resolvedAttack, random);
                TacticalImpactState impactState =
                    !attack.Target.IsActive ? TacticalImpactState.Destroyed
                    : resolvedAttack.Strength > shieldsBeforeImpact ? TacticalImpactState.Hull
                    : TacticalImpactState.Shield;
                events.Add(
                    TacticalCombatEvent.WeaponImpact(
                        attack.Source,
                        attack.Target,
                        attack.Attack.WeaponType,
                        impactState,
                        resolvedAttack.Strength
                    )
                );
                if (targetWasActive && !attack.Target.IsActive)
                {
                    events.Add(TacticalCombatEvent.UnitDestroyed(attack.Source, attack.Target));
                }
            }

            fighterDeploymentSystem.ResolveCarrierStateChanges();
            tractorBeamSystem.ReleaseInvalidLocks();
            events.AddRange(tractorBeamSystem.DrainEvents());
        }

        /// <summary>
        /// Scales ordinary weapon damage by the target fighter's turn rate relative to its attacker.
        /// </summary>
        /// <param name="pendingAttack">The unresolved attack and its participating units.</param>
        /// <returns>The attack strength after fighter evasion is applied.</returns>
        private static TacticalAttack AdjustAttackForFighterEvasion(PendingAttack pendingAttack)
        {
            TacticalAttack attack = pendingAttack.Attack;
            if (
                pendingAttack.Target.Kind != TacticalUnitKind.Fighters
                || attack.WeaponType == TacticalWeaponType.Torpedo
                || attack.Strength == 0
            )
            {
                return attack;
            }

            float attackerTurnRate = Math.Max(0.1f, pendingAttack.Source.Maneuverability);
            float targetTurnRate = Math.Max(0.1f, pendingAttack.Target.Maneuverability);
            float relativeTurnRate = Math.Max(0.1f, targetTurnRate / attackerTurnRate);
            int adjustedStrength = Math.Max(1, (int)(attack.Strength / relativeTurnRate));
            return new TacticalAttack(attack.WeaponType, adjustedStrength);
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
                TacticalBehavior behavior = group?.Behavior ?? TacticalBehavior.None;
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
        /// Gets whether a fighter group has begun or completed its one Death Star attack run.
        /// </summary>
        /// <param name="group">The fighter group to inspect.</param>
        /// <returns>True when the group cannot make another run.</returns>
        public bool IsDeathStarAttackCommitted(TacticalShipGroup group)
        {
            return deathStarAttackSystem.IsCommitted(group);
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
            TacticalBehavior behavior = group?.Behavior ?? TacticalBehavior.None;
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
                AdvanceDeathStarAttack(unit, group);
                return Array.Empty<PendingAttack>();
            }
            if (TryAdvanceNavigation(unit, group, elapsedTime))
                return Array.Empty<PendingAttack>();

            if (behavior == TacticalBehavior.Escort)
                return AdvanceEscort(unit, group, elapsedTime);

            TacticalUnitState target = GetTarget(unit, group, behavior);
            if (target == null)
                return Array.Empty<PendingAttack>();

            IReadOnlyList<PendingAttack> attacks = ShouldSelectCapitalArc(unit, group, behavior)
                ? FireStrongestCapitalArc(unit, GetEligibleTargets(unit, group, behavior))
                : FireTargetArc(unit, target);
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
        /// Keeps one group with its friendly escort target while allowing defensive fire.
        /// </summary>
        /// <param name="unit">The escorting unit.</param>
        /// <param name="group">The escorting unit's command group.</param>
        /// <param name="elapsedTime">The elapsed tactical time.</param>
        /// <returns>The defensive attacks to resolve after every unit has acted.</returns>
        private IReadOnlyList<PendingAttack> AdvanceEscort(
            TacticalUnitState unit,
            TacticalShipGroup group,
            float elapsedTime
        )
        {
            TacticalUnitState escortTarget = group?.EscortTarget;
            if (escortTarget?.IsActive != true)
                return Array.Empty<PendingAttack>();

            TacticalUnitState attackTarget = GetTarget(unit, null, TacticalBehavior.None);
            IReadOnlyList<PendingAttack> attacks =
                unit.Kind == TacticalUnitKind.CapitalShip
                    ? FireStrongestCapitalArc(
                        unit,
                        GetEligibleTargets(unit, null, TacticalBehavior.None)
                    )
                    : FireTargetArc(unit, attackTarget);
            Vector3 approachDirection = NormalizeOrDefault(
                escortTarget.Position - unit.Position,
                unit.Forward
            );
            Vector3 right = NormalizeOrDefault(
                Vector3.Cross(Vector3.UnitY, approachDirection),
                Vector3.UnitX
            );
            group.SetMarkerPosition(escortTarget.Position);
            Vector3 destination =
                escortTarget.Position + GetFormationOffset(unit, group, approachDirection, right);
            MoveTowards(unit, destination, elapsedTime);
            return attacks;
        }

        /// <summary>
        /// Advances a fighter group through its dedicated Death Star attack run.
        /// </summary>
        /// <param name="unit">The group member currently being advanced.</param>
        /// <param name="group">The fighter group performing the attack.</param>
        private void AdvanceDeathStarAttack(TacticalUnitState unit, TacticalShipGroup group)
        {
            if (
                unit.Kind != TacticalUnitKind.Fighters
                || group == null
                || !isDeathStarAttackOrderValid(group)
                || deathStarAttackSystem.IsCommitted(group)
            )
            {
                return;
            }

            TacticalUnitState deathStar = GetTarget(unit, group, TacticalBehavior.AttackDeathStar);
            if (deathStar == null)
                return;

            deathStarAttackSystem.TryBegin(group, deathStar);
            events.AddRange(deathStarAttackSystem.DrainEvents());
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

            TacticalUnitState[] eligibleTargets = GetEligibleTargets(unit, group, behavior)
                .ToArray();
            if (
                targets.TryGetValue(unit, out TacticalUnitState currentTarget)
                && eligibleTargets.Contains(currentTarget)
            )
            {
                return currentTarget;
            }

            TacticalUnitState selectedTarget;
            if (group?.Targets.Count > 0)
            {
                selectedTarget = eligibleTargets.FirstOrDefault();
            }
            else if (
                behavior is TacticalBehavior.AttackFighters or TacticalBehavior.AttackCapitalShips
            )
            {
                selectedTarget = eligibleTargets.LastOrDefault();
            }
            else
            {
                selectedTarget = eligibleTargets.FirstOrDefault();
            }
            if (selectedTarget == null)
                targets.Remove(unit);
            else
                targets[unit] = selectedTarget;

            return selectedTarget;
        }

        /// <summary>
        /// Enumerates active opposing objects allowed by one tactical behavior.
        /// </summary>
        /// <param name="unit">The acting unit.</param>
        /// <param name="group">The unit's controlling group, if assigned.</param>
        /// <param name="behavior">The group's active behavior.</param>
        /// <returns>The eligible targets in tactical object order.</returns>
        private IEnumerable<TacticalUnitState> GetEligibleTargets(
            TacticalUnitState unit,
            TacticalShipGroup group,
            TacticalBehavior behavior
        )
        {
            IEnumerable<TacticalUnitState> candidates =
                group?.Targets.Count > 0
                    ? group.Targets
                    : units.Where(candidate => candidate.Side != unit.Side);
            candidates = candidates.Where(candidate => candidate.IsActive);
            if (behavior == TacticalBehavior.AttackFighters)
            {
                return candidates.Where(candidate => candidate.Kind == TacticalUnitKind.Fighters);
            }
            if (behavior == TacticalBehavior.AttackCapitalShips)
            {
                return candidates.Where(candidate =>
                    candidate.Kind == TacticalUnitKind.CapitalShip
                );
            }
            if (behavior == TacticalBehavior.AttackDeathStar)
            {
                return candidates.Where(candidate =>
                    candidate.Unit is CapitalShip { IsDeathStar: true }
                );
            }

            return candidates;
        }

        /// <summary>
        /// Determines whether a capital ship may independently select targets for its weapon arcs.
        /// </summary>
        /// <param name="attacker">The acting unit.</param>
        /// <param name="group">The unit's controlling command group.</param>
        /// <param name="behavior">The unit's active behavior.</param>
        /// <returns>True when the ship should use opportunistic arc targeting.</returns>
        private static bool ShouldSelectCapitalArc(
            TacticalUnitState attacker,
            TacticalShipGroup group,
            TacticalBehavior behavior
        )
        {
            return attacker.Kind == TacticalUnitKind.CapitalShip
                && group?.Targets.Count is not > 0
                && behavior
                    is not TacticalBehavior.AttackFighters
                        and not TacticalBehavior.AttackCapitalShips;
        }

        /// <summary>
        /// Selects the strongest charged arc and independently targets each weapon family in it.
        /// </summary>
        /// <param name="attacker">The capital ship selecting its firing arc.</param>
        /// <param name="candidates">The active opposing targets to consider.</param>
        /// <returns>The attacks produced by the selected arc.</returns>
        private static IReadOnlyList<PendingAttack> FireStrongestCapitalArc(
            TacticalUnitState attacker,
            IEnumerable<TacticalUnitState> candidates
        )
        {
            CapitalArcAttackPlan[] plans = Enum.GetValues(typeof(TacticalWeaponArc))
                .Cast<TacticalWeaponArc>()
                .Select(arc => new CapitalArcAttackPlan(arc))
                .ToArray();

            foreach (TacticalUnitState candidate in candidates)
            {
                float distance = Vector3.Distance(attacker.Position, candidate.Position);
                TacticalWeaponArc arc = GetFiringArc(attacker, candidate.Position);
                CapitalArcAttackPlan plan = plans[(int)arc];
                foreach (TacticalWeaponBattery battery in attacker.WeaponBatteries)
                {
                    int strength = attacker.GetAvailableAttackStrength(
                        arc,
                        distance,
                        battery.WeaponType,
                        candidate.Kind
                    );
                    plan.Consider(battery.WeaponType, candidate, strength);
                }
            }

            CapitalArcAttackPlan selectedPlan = plans
                .OrderByDescending(plan => plan.Strength)
                .First();
            if (selectedPlan.Strength <= 0)
                return Array.Empty<PendingAttack>();

            return attacker
                .FireArc(selectedPlan.Arc, selectedPlan.WeaponTypes)
                .Select(attack => new PendingAttack(
                    attacker,
                    selectedPlan.GetTarget(attack.WeaponType),
                    attack
                ))
                .ToArray();
        }

        /// <summary>
        /// Discharges the charged arc occupied by an eligible target.
        /// </summary>
        /// <param name="attacker">The acting unit.</param>
        /// <param name="target">The opposing target to fire upon.</param>
        /// <returns>The pending attacks, if the target's arc can fire.</returns>
        private static IReadOnlyList<PendingAttack> FireTargetArc(
            TacticalUnitState attacker,
            TacticalUnitState target
        )
        {
            if (target == null)
                return Array.Empty<PendingAttack>();

            float distance = Vector3.Distance(attacker.Position, target.Position);
            TacticalWeaponArc arc = GetFiringArc(attacker, target.Position);
            if (attacker.GetAvailableAttackStrength(arc, distance, target.Kind) <= 0)
                return Array.Empty<PendingAttack>();

            List<PendingAttack> attacks = attacker
                .FireArc(arc, distance, target.Kind)
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
        private Vector3 GetApproachPosition(
            TacticalUnitState unit,
            TacticalUnitState target,
            TacticalShipGroup group,
            TacticalBehavior behavior,
            out Vector3 markerPosition
        )
        {
            if (group != null && IsManeuverBehavior(behavior))
            {
                ManeuverOrder order = ResolveManeuverOrder(group, target, behavior);
                markerPosition = order.Marker;
                Vector3 maneuverDirection = NormalizeOrDefault(
                    target.Position - order.Origin,
                    unit.Forward
                );
                Vector3 maneuverRight = NormalizeOrDefault(
                    Vector3.Cross(Vector3.UnitY, maneuverDirection),
                    Vector3.UnitX
                );
                return markerPosition
                    + GetFormationOffset(unit, group, maneuverDirection, maneuverRight);
            }

            Vector3 approachDirection = NormalizeOrDefault(
                target.Position - unit.Position,
                unit.Forward
            );
            Vector3 right = NormalizeOrDefault(
                Vector3.Cross(Vector3.UnitY, approachDirection),
                Vector3.UnitX
            );
            markerPosition = target.Position;
            return markerPosition + GetFormationOffset(unit, group, approachDirection, right);
        }

        /// <summary>
        /// Resolves one maneuver command to the navigation anchor used for its lifetime.
        /// </summary>
        /// <param name="group">The group performing the maneuver.</param>
        /// <param name="target">The opposing tactical object around which to maneuver.</param>
        /// <param name="behavior">The selected maneuver direction.</param>
        /// <returns>The resolved group-level maneuver order.</returns>
        private ManeuverOrder ResolveManeuverOrder(
            TacticalShipGroup group,
            TacticalUnitState target,
            TacticalBehavior behavior
        )
        {
            if (
                maneuverOrders.TryGetValue(group, out ManeuverOrder order)
                && order.CommandRevision == group.CommandRevision
                && ReferenceEquals(order.Target, target)
            )
            {
                return order;
            }

            Vector3 origin = GetActiveGroupCenter(group);
            Vector3 targetVector = target.Position - origin;
            Vector3 maneuverVector = RotateManeuverVector(targetVector, behavior);
            Vector3 desiredMarker = origin + maneuverVector * _maneuverDistanceScale;
            Vector3 marker = FindClosestNavigationAnchor(desiredMarker);
            order = new ManeuverOrder(group.CommandRevision, target, origin, marker);
            maneuverOrders[group] = order;
            return order;
        }

        /// <summary>
        /// Gets the center of the active units participating in one command group.
        /// </summary>
        /// <param name="group">The group whose center is requested.</param>
        /// <returns>The active-unit center, or the existing command marker when none remain.</returns>
        private static Vector3 GetActiveGroupCenter(TacticalShipGroup group)
        {
            Vector3 total = Vector3.Zero;
            int count = 0;
            foreach (TacticalUnitState unit in group.Units)
            {
                if (!unit.IsActive)
                    continue;

                total += unit.Position;
                count++;
            }

            return count == 0 ? group.MarkerPosition : total / count;
        }

        /// <summary>
        /// Rotates a group-to-target vector into the selected maneuver plane.
        /// </summary>
        /// <param name="targetVector">The vector from the group center to its target.</param>
        /// <param name="behavior">The selected maneuver direction.</param>
        /// <returns>The rotated maneuver vector.</returns>
        private static Vector3 RotateManeuverVector(Vector3 targetVector, TacticalBehavior behavior)
        {
            float angle = behavior is TacticalBehavior.RightHook or TacticalBehavior.Hammer
                ? -_maneuverAngle
                : _maneuverAngle;
            float cosine = (float)Math.Cos(angle);
            float sine = (float)Math.Sin(angle);
            if (behavior is TacticalBehavior.LeftHook or TacticalBehavior.RightHook)
            {
                return new Vector3(
                    cosine * targetVector.X - sine * targetVector.Z,
                    targetVector.Y,
                    sine * targetVector.X + cosine * targetVector.Z
                );
            }

            return new Vector3(
                targetVector.X,
                cosine * targetVector.Y + sine * targetVector.Z,
                cosine * targetVector.Z - sine * targetVector.Y
            );
        }

        /// <summary>
        /// Finds the fixed tactical navigation anchor nearest a desired position.
        /// </summary>
        /// <param name="position">The desired tactical position.</param>
        /// <returns>The nearest navigation anchor.</returns>
        private Vector3 FindClosestNavigationAnchor(Vector3 position)
        {
            Vector3 closest = Vector3.Zero;
            float closestDistance = float.MaxValue;
            for (int setIndex = 0; setIndex < navigationGrid.SetCount; setIndex++)
            {
                foreach (TacticalNavPoint point in navigationGrid.GetPoints(setIndex))
                {
                    Vector3 candidate = new Vector3(point.X, point.Y, point.Z);
                    float distance = Vector3.DistanceSquared(position, candidate);
                    if (distance >= closestDistance)
                        continue;

                    closest = candidate;
                    closestDistance = distance;
                }
            }

            return closest;
        }

        /// <summary>
        /// Gets whether a behavior uses a directional navigation-anchor maneuver.
        /// </summary>
        /// <param name="behavior">The behavior to inspect.</param>
        /// <returns>True for the four directional maneuver orders.</returns>
        private static bool IsManeuverBehavior(TacticalBehavior behavior)
        {
            return behavior
                is TacticalBehavior.LeftHook
                    or TacticalBehavior.RightHook
                    or TacticalBehavior.Hammer
                    or TacticalBehavior.Anvil;
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
                if (index == 0)
                    return Vector3.Zero;

                int lane = (index + 1) / 2;
                float direction = index % 2 == 0 ? 1f : -1f;
                float spacing = Math.Max(
                    _formationSpacing,
                    group.Units.Max(member => member.HorizontalExtent * 2f)
                );
                return right * direction * lane * spacing;
            }

            Vector3 localDirection = Vector3.Normalize(
                SurroundDirections[(index + 1) % SurroundDirections.Length]
            );
            int shell = (index + 1) / SurroundDirections.Length;
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
            CollisionAvoidanceState avoidance = GetCollisionAvoidanceState(unit);
            if (avoidance.TemporaryOffset.HasValue)
                destination += avoidance.TemporaryOffset.Value;

            Vector3 displacement = destination - unit.Position;
            float distance = displacement.Length();
            float movementSpeed = tractorBeamSystem.GetMovementSpeed(unit, GetCommandBudget(unit));
            if (distance <= _navigationArrivalDistance || movementSpeed <= 0f)
                return;

            Vector3 desiredForward = displacement / distance;
            float turnBudget = unit.Maneuverability;
            if (unit.Kind == TacticalUnitKind.CapitalShip)
                turnBudget += GetCommandBudget(unit);
            float turnAmount = Math.Min(1f, turnBudget * elapsedTime);
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
            else if (unit.Kind == TacticalUnitKind.CapitalShip)
                avoidance.VerticalClearanceBlocked = true;
        }

        /// <summary>
        /// Updates delayed temporary detours and resets per-update clearance results.
        /// </summary>
        private void RefreshCollisionAvoidance()
        {
            foreach (TacticalShipGroup group in groups)
            {
                MarkerStabilityState stability = GetMarkerStabilityState(group);
                if (stability.Position == group.MarkerPosition)
                    stability.RefreshCount++;
                else
                {
                    stability.Position = group.MarkerPosition;
                    stability.RefreshCount = 0;
                }

                for (int index = 0; index < group.Units.Count; index++)
                {
                    TacticalUnitState unit = group.Units[index];
                    if (unit.Kind != TacticalUnitKind.CapitalShip)
                        continue;

                    CollisionAvoidanceState avoidance = GetCollisionAvoidanceState(unit);
                    bool timeoutElapsed =
                        tacticalTime - avoidance.LastChangeTime > _temporaryDetourTimeout;
                    if (
                        avoidance.VerticalClearanceBlocked
                        && avoidance.TemporaryOffset == null
                        && stability.RefreshCount >= _stableMarkerRefreshesForDetour
                        && timeoutElapsed
                    )
                    {
                        avoidance.Phase = (avoidance.Phase + index + 1) & 3;
                        avoidance.TemporaryOffset = TemporaryDetourOffsets[avoidance.Phase];
                        avoidance.LastChangeTime = tacticalTime;
                    }
                    else if (
                        !avoidance.VerticalClearanceBlocked
                        && avoidance.TemporaryOffset != null
                        && timeoutElapsed
                    )
                    {
                        avoidance.TemporaryOffset = null;
                    }

                    avoidance.VerticalClearanceBlocked = false;
                }
            }
        }

        /// <summary>
        /// Gets the persistent collision-avoidance state for one tactical unit.
        /// </summary>
        /// <param name="unit">The unit whose state is requested.</param>
        /// <returns>The unit's collision-avoidance state.</returns>
        private CollisionAvoidanceState GetCollisionAvoidanceState(TacticalUnitState unit)
        {
            if (!collisionAvoidance.TryGetValue(unit, out CollisionAvoidanceState state))
            {
                state = new CollisionAvoidanceState();
                collisionAvoidance.Add(unit, state);
            }

            return state;
        }

        /// <summary>
        /// Gets the persistent marker-stability state for one tactical group.
        /// </summary>
        /// <param name="group">The group whose marker is tracked.</param>
        /// <returns>The group's marker-stability state.</returns>
        private MarkerStabilityState GetMarkerStabilityState(TacticalShipGroup group)
        {
            if (!markerStability.TryGetValue(group, out MarkerStabilityState state))
            {
                state = new MarkerStabilityState { Position = group.MarkerPosition };
                markerStability.Add(group, state);
            }

            return state;
        }

        /// <summary>
        /// Gets the active tactical commander's movement contribution for one unit.
        /// </summary>
        /// <param name="unit">The unit receiving command support.</param>
        /// <returns>The command contribution for the unit's kind and side.</returns>
        private float GetCommandBudget(TacticalUnitState unit)
        {
            IReadOnlyDictionary<TacticalBattleSide, float> budgets =
                unit.Kind == TacticalUnitKind.Fighters
                    ? fighterCommandBudgets
                    : capitalCommandBudgets;
            return budgets.TryGetValue(unit.Side, out float budget) ? budget : 1f;
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
            if (Vector3.Distance(unit.Position, carrier.Position) < _fighterRecoveryDistance)
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
            {
                unit.Hull = 0;
                events.Add(
                    TacticalCombatEvent.UnitLifecycle(TacticalCombatEventKind.UnitDestroyed, unit)
                );
                return;
            }

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
        /// Places one side into separate, mirrored capital-ship and fighter ranks.
        /// </summary>
        /// <param name="side">The side to place.</param>
        /// <param name="depthDirection">The sign of the side's battlefield depth.</param>
        /// <param name="forward">The formation's facing direction.</param>
        private void PlaceFormation(TacticalBattleSide side, float depthDirection, Vector3 forward)
        {
            PlaceFormationRank(
                side,
                TacticalUnitKind.CapitalShip,
                depthDirection * _capitalFormationDepth,
                forward
            );
            PlaceFormationRank(
                side,
                TacticalUnitKind.Fighters,
                depthDirection * _fighterFormationDepth,
                forward
            );
        }

        /// <summary>
        /// Places one unit family from the center outward in alternating lateral lanes.
        /// </summary>
        /// <param name="side">The side whose units are placed.</param>
        /// <param name="kind">The unit family occupying the rank.</param>
        /// <param name="depth">The rank's battlefield depth.</param>
        /// <param name="forward">The rank's facing direction.</param>
        private void PlaceFormationRank(
            TacticalBattleSide side,
            TacticalUnitKind kind,
            float depth,
            Vector3 forward
        )
        {
            TacticalUnitState[] rank = units
                .Where(unit => unit.Side == side && unit.Kind == kind)
                .ToArray();
            for (int index = 0; index < rank.Length; index++)
            {
                rank[index].Position = new Vector3(
                    GetAlternatingLane(index) * _formationSpacing,
                    0f,
                    depth
                );
                rank[index].Forward = forward;
            }
        }

        /// <summary>
        /// Converts a source-order index into center, right, left, and progressively wider lanes.
        /// </summary>
        /// <param name="index">The zero-based unit index within its rank.</param>
        /// <returns>The signed lateral lane.</returns>
        private static int GetAlternatingLane(int index)
        {
            if (index == 0)
                return 0;

            int magnitude = (index + 1) / 2;
            return index % 2 == 1 ? magnitude : -magnitude;
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

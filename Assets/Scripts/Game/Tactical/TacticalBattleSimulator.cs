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
        private const float _initialSeparation = 100f;
        private const float _formationSpacing = 8f;
        private const float _navigationArrivalDistance = 1f;
        private const float _withdrawalDistance = _initialSeparation * 2f;
        private readonly IReadOnlyList<TacticalShipGroup> groups;
        private readonly Dictionary<TacticalUnitState, TacticalUnitState> targets =
            new Dictionary<TacticalUnitState, TacticalUnitState>();
        private readonly IRandomNumberProvider random;
        private readonly IReadOnlyList<TacticalUnitState> units;

        private readonly struct PendingAttack
        {
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
            /// <param name="target">The unit receiving the attack.</param>
            /// <param name="attack">The weapon-family attack.</param>
            public PendingAttack(TacticalUnitState target, TacticalAttack attack)
            {
                Target = target;
                Attack = attack;
            }
        }

        /// <summary>
        /// Initializes the simulator and places both sides into opposing formations.
        /// </summary>
        /// <param name="units">The battle's tactical units.</param>
        /// <param name="groups">The battle's mutable command groups.</param>
        /// <param name="random">The battle's deterministic random source.</param>
        public TacticalBattleSimulator(
            IReadOnlyList<TacticalUnitState> units,
            IReadOnlyList<TacticalShipGroup> groups,
            IRandomNumberProvider random
        )
        {
            this.units = units ?? throw new ArgumentNullException(nameof(units));
            this.groups = groups ?? throw new ArgumentNullException(nameof(groups));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            PlaceFormation(TacticalBattleSide.Attacker, -_initialSeparation / 2f, Vector3.UnitZ);
            PlaceFormation(TacticalBattleSide.Defender, _initialSeparation / 2f, -Vector3.UnitZ);
        }

        /// <summary>
        /// Advances all active objects through one tactical time interval.
        /// </summary>
        /// <param name="elapsedTime">The elapsed tactical time.</param>
        public void Advance(float elapsedTime)
        {
            List<PendingAttack> attacks = new List<PendingAttack>();
            foreach (TacticalUnitState unit in units.Where(unit => unit.IsActive).ToArray())
                attacks.AddRange(AdvanceUnit(unit, elapsedTime));

            foreach (PendingAttack attack in attacks)
                attack.Target.ApplyDamage(attack.Attack, random);
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
            if (behavior == TacticalBehavior.Hold)
                return Array.Empty<PendingAttack>();
            if (behavior == TacticalBehavior.Withdraw)
            {
                AdvanceWithdrawal(unit, elapsedTime);
                return Array.Empty<PendingAttack>();
            }
            if (unit.Kind == TacticalUnitKind.Fighters && behavior == TacticalBehavior.Recover)
            {
                AdvanceRecovery(unit, elapsedTime);
                return Array.Empty<PendingAttack>();
            }
            if (TryAdvanceNavigation(unit, group, elapsedTime))
                return Array.Empty<PendingAttack>();

            TacticalUnitState target = GetTarget(unit, group, behavior);
            if (target == null)
                return Array.Empty<PendingAttack>();

            IReadOnlyList<PendingAttack> attacks = FireStrongestArc(unit, target);
            Vector3 destination = GetApproachPosition(target, behavior);
            MoveTowards(unit, destination, elapsedTime);
            return attacks;
        }

        /// <summary>
        /// Retains the unit's current target or acquires the last eligible opposing object.
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

            TacticalUnitState selectedTarget = eligibleTargets.LastOrDefault();
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

            return attacker
                .FireArc(arc, distance)
                .Select(attack => new PendingAttack(target, attack))
                .ToArray();
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
        /// <param name="target">The target being approached.</param>
        /// <param name="behavior">The active approach behavior.</param>
        /// <returns>The desired tactical position.</returns>
        private static Vector3 GetApproachPosition(
            TacticalUnitState target,
            TacticalBehavior behavior
        )
        {
            const float approachOffset = 20f;
            return behavior switch
            {
                TacticalBehavior.LeftHook => target.Position - Vector3.UnitX * approachOffset,
                TacticalBehavior.RightHook => target.Position + Vector3.UnitX * approachOffset,
                TacticalBehavior.Hammer => target.Position - Vector3.UnitY * approachOffset,
                TacticalBehavior.Anvil => target.Position + Vector3.UnitY * approachOffset,
                _ => target.Position,
            };
        }

        /// <summary>
        /// Turns and advances one unit toward a tactical position.
        /// </summary>
        /// <param name="unit">The moving unit.</param>
        /// <param name="destination">The desired tactical position.</param>
        /// <param name="elapsedTime">The elapsed tactical time.</param>
        private static void MoveTowards(
            TacticalUnitState unit,
            Vector3 destination,
            float elapsedTime
        )
        {
            Vector3 displacement = destination - unit.Position;
            float distance = displacement.Length();
            if (distance <= _navigationArrivalDistance || unit.EffectiveSublightSpeed <= 0f)
                return;

            Vector3 desiredForward = displacement / distance;
            float turnAmount = Math.Min(1f, unit.Maneuverability * elapsedTime);
            unit.Forward = NormalizeOrDefault(
                Vector3.Lerp(unit.Forward, desiredForward, turnAmount),
                desiredForward
            );
            float movement = Math.Min(distance, unit.EffectiveSublightSpeed * elapsedTime);
            unit.Position += unit.Forward * movement;
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
            if (Vector3.Distance(unit.Position, destination) <= _navigationArrivalDistance)
                group.RemoveNavigationPoint(point);
            else
                MoveTowards(unit, destination, elapsedTime);

            return true;
        }

        /// <summary>
        /// Returns a fighter group to its closest active capital ship.
        /// </summary>
        /// <param name="unit">The recovering fighter unit.</param>
        /// <param name="elapsedTime">The elapsed tactical time.</param>
        private void AdvanceRecovery(TacticalUnitState unit, float elapsedTime)
        {
            TacticalUnitState carrier = unit.RecoveryTarget;
            if (carrier == null)
                return;
            if (!carrier.IsActive)
                return;
            if (Vector3.Distance(unit.Position, carrier.Position) <= _navigationArrivalDistance)
            {
                unit.BeginWithdrawal();
                unit.CompleteWithdrawal();
                return;
            }

            MoveTowards(unit, carrier.Position, elapsedTime);
        }

        /// <summary>
        /// Advances a unit along its side's tactical withdrawal route.
        /// </summary>
        /// <param name="unit">The withdrawing unit.</param>
        /// <param name="elapsedTime">The elapsed tactical time.</param>
        private static void AdvanceWithdrawal(TacticalUnitState unit, float elapsedTime)
        {
            if (!unit.CanWithdraw)
                return;

            unit.BeginWithdrawal();
            float direction = unit.Side == TacticalBattleSide.Attacker ? -1f : 1f;
            unit.Forward = new Vector3(0f, 0f, direction);
            MoveTowards(
                unit,
                new Vector3(unit.Position.X, unit.Position.Y, direction * _withdrawalDistance),
                elapsedTime
            );
            if (Math.Abs(unit.Position.Z) >= _withdrawalDistance - _navigationArrivalDistance)
                unit.CompleteWithdrawal();
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
    }
}

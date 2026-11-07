using System;
using System.Collections.Generic;
using System.Numerics;
using Rebellion.Game.Results;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Owns the orders, navigation points, target lists, and formation of a tactical group.
    /// </summary>
    public sealed class TacticalCombatGroup
    {
        private readonly List<TacticalCombatUnit> _targets = new();
        private readonly List<TacticalCombatUnit> _firingTargets = new();
        private readonly List<Vector3> _waypoints = new();
        private readonly List<TacticalCombatUnit> _requestedTargets = new();
        private readonly List<Vector3> _requestedWaypoints = new();
        private float _orbitPhase;
        public int ID { get; }
        public CombatSide Side { get; }
        public Vector3 SideLane { get; }
        public TacticalCombatFormation Formation { get; }
        public TacticalCombatEngagement Engagement { get; } = new();
        public TacticalCombatAttackRun AttackRun { get; internal set; }
        public TacticalCombatOrder Order { get; private set; }
        public TacticalCombatOrder ActiveOrder { get; private set; }
        public event Action<TacticalCombatGroup, TacticalCombatOrder> OrderRequested;
        public bool CloseAttack { get; private set; }
        public bool ActiveCloseAttack { get; private set; }
        public event Action<TacticalCombatGroup, bool> AttackModeRequested;
        public bool IsFighterGroup { get; }
        public bool IsAutomated { get; internal set; }
        public bool HasWaypoints => _waypoints.Count != 0;
        public TacticalCombatUnit PrimaryTarget => _targets.Count > 0 ? _targets[0] : null;
        public IReadOnlyList<TacticalCombatUnit> ComputedTargets { get; }
        public IReadOnlyList<TacticalCombatUnit> Targets { get; }
        public IReadOnlyList<TacticalCombatUnit> RequestedTargets { get; }
        public IReadOnlyList<Vector3> RequestedWaypoints { get; }
        public IReadOnlyList<TacticalCombatUnit> Members => Formation.Members;
        public event Action<TacticalCombatUnit> HyperspaceRequested;
        public event Action<TacticalCombatGroup, TacticalCombatUnit> TargetRemoved;
        public event Action<TacticalCombatGroup> NavigationCompleted;

        /// <summary>
        /// Creates a capital-ship or fighter group from an ordered deployment roster.
        /// </summary>
        /// <param name="id">The battle-local group identifier.</param>
        /// <param name="units">The initial members.</param>
        /// <param name="sideLane">The side's forward direction.</param>
        public TacticalCombatGroup(
            int id,
            IReadOnlyList<TacticalCombatUnit> units,
            Vector3 sideLane
        )
        {
            if (units == null || units.Count == 0)
                throw new ArgumentException(
                    "A tactical group requires at least one unit.",
                    nameof(units)
                );
            ID = id;
            Side = units[0].Side;
            SideLane = sideLane;
            IsFighterGroup = units[0].IsFighter;
            for (int index = 0; index < units.Count; index++)
            {
                if (units[index].Side != Side || units[index].IsFighter != IsFighterGroup)
                    throw new ArgumentException(
                        "A tactical group must contain one side and unit category.",
                        nameof(units)
                    );
                for (int previous = 0; previous < index; previous++)
                    if (ReferenceEquals(units[previous], units[index]))
                        throw new ArgumentException(
                            "A tactical group cannot contain duplicate units.",
                            nameof(units)
                        );
            }
            foreach (TacticalCombatUnit unit in units)
                unit.GroupID = id;
            Formation = new TacticalCombatFormation(
                IsFighterGroup
                    ? TacticalCombatFormationKind.Direct
                    : TacticalCombatFormationKind.Line,
                units,
                sideLane
            );
            ComputedTargets = _firingTargets.AsReadOnly();
            Targets = _targets.AsReadOnly();
            RequestedTargets = _requestedTargets.AsReadOnly();
            RequestedWaypoints = _requestedWaypoints.AsReadOnly();
        }

        /// <summary>
        /// Changes the group's tactical behavior and clears the lists owned by that order.
        /// </summary>
        /// <param name="order">The requested group behavior.</param>
        public void SetOrder(TacticalCombatOrder order)
        {
            if (
                (
                    order == TacticalCombatOrder.Recover
                    || order == TacticalCombatOrder.AttackBattleStation
                ) && !IsFighterGroup
            )
                throw new InvalidOperationException("This order requires a fighter group.");
            if (Order == TacticalCombatOrder.HoldFire)
            {
                _requestedTargets.Clear();
                SetAttackMode(true);
            }
            if (order == TacticalCombatOrder.Anvil)
                _waypoints.Clear();
            else
                _requestedWaypoints.Clear();
            if (
                order
                is TacticalCombatOrder.None
                    or TacticalCombatOrder.Recover
                    or TacticalCombatOrder.Withdraw
                    or TacticalCombatOrder.AttackFighters
                    or TacticalCombatOrder.AttackCapitalShips
                    or TacticalCombatOrder.AttackBattleStation
            )
                _requestedTargets.Clear();
            Order = order;
            OrderRequested?.Invoke(this, order);
        }

        /// <summary>
        /// Applies a queued behavior change and clears only the lists owned by that behavior.
        /// </summary>
        /// <param name="order">The due behavior order.</param>
        internal void ApplyOrder(TacticalCombatOrder order)
        {
            if (ActiveOrder == TacticalCombatOrder.HoldFire)
                _targets.Clear();
            ActiveOrder = order;
            if (order != TacticalCombatOrder.None)
                _waypoints.Clear();
            if (
                order
                is TacticalCombatOrder.Recover
                    or TacticalCombatOrder.Withdraw
                    or TacticalCombatOrder.AttackFighters
                    or TacticalCombatOrder.AttackCapitalShips
                    or TacticalCombatOrder.AttackBattleStation
            )
                _targets.Clear();
            if (order == TacticalCombatOrder.HoldFire && !IsFighterGroup)
                Formation.SetKind(TacticalCombatFormationKind.Surround);
            RebuildFiringTargets(null);
        }

        /// <summary>
        /// Replaces or extends the group's designated target list.
        /// </summary>
        /// <param name="target">The opposing target.</param>
        /// <param name="append">Whether to append instead of replace.</param>
        public void SetTarget(TacticalCombatUnit target, bool append = false)
        {
            RequestTarget(target, append);
            ApplyTarget(target, append);
        }

        /// <summary>
        /// Updates the requested target list before its delayed command takes effect.
        /// </summary>
        /// <param name="target">The opposing target.</param>
        /// <param name="append">Whether to extend the requested list.</param>
        internal void RequestTarget(TacticalCombatUnit target, bool append)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (target.Side == Side)
                throw new InvalidOperationException(
                    "An attack target must belong to the opposing side."
                );
            if (!append)
                _requestedTargets.Clear();
            if (!_requestedTargets.Contains(target))
                _requestedTargets.Add(target);
        }

        /// <summary>
        /// Applies a target command without overwriting later requested changes.
        /// </summary>
        /// <param name="target">The designated target.</param>
        /// <param name="append">Whether to extend the active list.</param>
        internal void ApplyTarget(TacticalCombatUnit target, bool append)
        {
            if (!append)
                _targets.Clear();
            if (!_targets.Contains(target))
                _targets.Add(target);
            RebuildFiringTargets(null);
        }

        /// <summary>
        /// Removes a target from either the requested or active list.
        /// </summary>
        /// <param name="target">The target being removed.</param>
        /// <param name="requested">Whether to update the requested list before dispatch.</param>
        internal void RemoveTarget(TacticalCombatUnit target, bool requested)
        {
            if (requested)
                _requestedTargets.Remove(target);
            else
            {
                _targets.Remove(target);
                RebuildFiringTargets(null);
            }
        }

        /// <summary>
        /// Replaces or extends the group's navigation route.
        /// </summary>
        /// <param name="position">The selected navigation point.</param>
        /// <param name="append">Whether to append instead of replace.</param>
        public void SetWaypoint(Vector3 position, bool append = false)
        {
            RequestWaypoint(position, append);
            ApplyWaypoint(position, append);
        }

        /// <summary>
        /// Updates the requested navigation list before command dispatch.
        /// </summary>
        /// <param name="position">The selected navigation point.</param>
        /// <param name="append">Whether to extend the requested route.</param>
        internal void RequestWaypoint(Vector3 position, bool append)
        {
            if (!append)
                _requestedWaypoints.Clear();
            if (!_requestedWaypoints.Contains(position))
                _requestedWaypoints.Add(position);
        }

        /// <summary>
        /// Applies a route command without replacing later requested changes.
        /// </summary>
        /// <param name="position">The selected navigation point.</param>
        /// <param name="append">Whether to extend the active route.</param>
        internal void ApplyWaypoint(Vector3 position, bool append)
        {
            if (!append)
                _waypoints.Clear();
            if (!_waypoints.Contains(position))
                _waypoints.Add(position);
        }

        /// <summary>
        /// Removes a navigation point from either the requested or active route.
        /// </summary>
        /// <param name="position">The navigation point being removed.</param>
        /// <param name="requested">Whether to update the requested route before dispatch.</param>
        internal void RemoveWaypoint(Vector3 position, bool requested)
        {
            if (requested)
                _requestedWaypoints.Remove(position);
            else
                _waypoints.Remove(position);
        }

        /// <summary>
        /// Selects close attack or an engagement range chosen from weapon profiles.
        /// </summary>
        /// <param name="closeAttack">Whether to use close attack.</param>
        public void SetAttackMode(bool closeAttack)
        {
            CloseAttack = closeAttack;
            AttackModeRequested?.Invoke(this, closeAttack);
        }

        /// <summary>
        /// Applies the due engagement mode and its associated capital-ship formation.
        /// </summary>
        /// <param name="closeAttack">Whether the group should close with its target.</param>
        internal void ApplyAttackMode(bool closeAttack)
        {
            ActiveCloseAttack = closeAttack;
            if (!IsFighterGroup)
                Formation.SetKind(
                    closeAttack
                        ? TacticalCombatFormationKind.Surround
                        : TacticalCombatFormationKind.Line
                );
            Engagement.Update(Members, PrimaryTarget, ActiveCloseAttack);
        }

        /// <summary>
        /// Refreshes movement limits, engagement distances, and computed firing targets.
        /// </summary>
        /// <param name="units">The battle's captured active roster.</param>
        /// <param name="systemTime">The elapsed whole seconds.</param>
        public void Refresh(IReadOnlyList<TacticalCombatUnit> units, float systemTime)
        {
            Engagement.Update(Members, PrimaryTarget, ActiveCloseAttack);
            Formation.Refresh(systemTime);
            TacticalCombatUnit computed = null;
            if (Members.Count > 0)
            {
                TacticalCombatUnit primary = Members[0];
                bool hasLaser = false;
                bool hasOther = false;
                for (int arc = 0; arc < 4; arc++)
                {
                    hasLaser |=
                        primary.Weapons.GetStrength(
                            (TacticalWeaponArc)arc,
                            TacticalWeaponType.LaserCannon
                        ) != 0;
                    hasOther |=
                        primary.Weapons.GetStrength(
                            (TacticalWeaponArc)arc,
                            TacticalWeaponType.IonCannon
                        ) != 0
                        || primary.Weapons.GetStrength(
                            (TacticalWeaponArc)arc,
                            TacticalWeaponType.Turbolaser
                        ) != 0;
                }
                float nearest = float.MaxValue;
                if (hasLaser || hasOther)
                    for (int index = 0; index < units.Count; index++)
                    {
                        TacticalCombatUnit candidate = units[index];
                        if (
                            candidate.Side == Side
                            || !candidate.IsTargetable
                            || candidate.IsFighter != hasLaser
                        )
                            continue;
                        Vector3 delta = Formation.Position - candidate.Position;
                        float distance = (float)(
                            (double)delta.Y * delta.Y
                            + (double)delta.Z * delta.Z
                            + (double)delta.X * delta.X
                        );
                        if (distance < nearest)
                        {
                            nearest = distance;
                            computed = candidate;
                        }
                    }
            }
            RebuildFiringTargets(computed);
        }

        /// <summary>
        /// Advances navigation, engagement movement, and retreat boundary checks.
        /// </summary>
        /// <param name="elapsedMilliseconds">The continuous movement interval.</param>
        /// <param name="battleDepth">The battle's deployment span.</param>
        /// <param name="withdrawalMilliseconds">The side's remaining retreat delay.</param>
        /// <param name="groups">The encounter's indexed groups for reciprocal targeting checks.</param>
        public void Advance(
            float elapsedMilliseconds,
            float battleDepth,
            float withdrawalMilliseconds,
            IReadOnlyDictionary<int, TacticalCombatGroup> groups = null
        )
        {
            if (ActiveOrder == TacticalCombatOrder.AttackBattleStation && AttackRun != null)
            {
                AttackRun.AdvanceMovement(elapsedMilliseconds, battleDepth);
                return;
            }
            if (ActiveOrder == TacticalCombatOrder.Withdraw)
            {
                AdvanceWithdrawal(elapsedMilliseconds, battleDepth, withdrawalMilliseconds);
                return;
            }
            if (_waypoints.Count > 0)
            {
                Vector3 point = _waypoints[0];
                Vector3 heading = TacticalCombatGeometry.Normalize(point - Formation.Position);
                if (Formation.MoveTowards(point, elapsedMilliseconds, 1, heading))
                    return;
                _waypoints.RemoveAt(0);
                _requestedWaypoints.Remove(point);
                if (_waypoints.Count == 0)
                    NavigationCompleted?.Invoke(this);
                if (!IsFighterGroup)
                    return;
            }
            TacticalCombatUnit target = PrimaryTarget;
            if (target == null)
                return;
            if (!target.IsTargetable)
            {
                _targets.RemoveAt(0);
                _requestedTargets.Remove(target);
                TargetRemoved?.Invoke(this, target);
                return;
            }
            if (ActiveOrder == TacticalCombatOrder.Stop)
                return;
            if (IsFighterGroup)
                AdvanceFighterEngagement(target, elapsedMilliseconds);
            else
                AdvanceCapitalEngagement(target, elapsedMilliseconds, groups);
        }

        /// <summary>
        /// Resolves the current displacement requested from a group member.
        /// </summary>
        /// <param name="unit">The moving member.</param>
        /// <param name="battleDepth">The deployment span used by retreat.</param>
        /// <param name="recoveryCarrier">The carrier selected for recovery, if any.</param>
        /// <returns>The requested displacement.</returns>
        public Vector3 GetMemberMovement(
            TacticalCombatUnit unit,
            float battleDepth,
            TacticalCombatUnit recoveryCarrier
        )
        {
            Vector3 movement;
            if (ActiveOrder == TacticalCombatOrder.Recover && recoveryCarrier != null)
                movement =
                    new Vector3(
                        recoveryCarrier.Position.X,
                        recoveryCarrier.Position.Y
                            - (recoveryCarrier.IsBattleStation ? 0 : recoveryCarrier.HalfHeight),
                        recoveryCarrier.Position.Z
                    ) - unit.Position;
            else if (ActiveOrder == TacticalCombatOrder.Withdraw)
            {
                float destinationZ = (float)((double)SideLane.Z * battleDepth * -0.5f);
                movement =
                    Math.Abs((double)unit.Position.Z - destinationZ) > 4
                        ? new Vector3(0, 0, destinationZ - unit.Position.Z)
                        : Vector3.Zero;
            }
            else
                movement = Formation.GetMemberMovement(unit);
            unit.DesiredMovement = movement;
            return movement;
        }

        /// <summary>
        /// Resolves a member's requested heading, including stationary battery alignment.
        /// </summary>
        /// <param name="unit">The moving member.</param>
        /// <returns>The normalized requested heading.</returns>
        public Vector3 GetMemberHeading(TacticalCombatUnit unit)
        {
            if (!unit.IsFighter && ActiveOrder == TacticalCombatOrder.Withdraw)
                return -SideLane;
            Vector3 heading = unit.DesiredMovement;
            if (heading == Vector3.Zero)
            {
                TacticalCombatUnit target = PrimaryTarget;
                if (unit.IsFighter)
                {
                    if (target == null)
                        return Formation.Heading;
                    heading = target.Position - unit.Position;
                }
                else if (
                    target?.IsFighter == false
                    && Formation.Velocity == Vector3.Zero
                    && Formation.StableUpdates >= 5
                )
                {
                    int arc = (int)(
                        TacticalCombatFireControl.GetStrongestArc(unit, false)
                        ?? TacticalWeaponArc.Forward
                    );
                    Vector3 delta = target.Position - unit.Position;
                    double angle = (double)arc * 3.1415927f * 0.5f;
                    double cosine = Math.Cos(angle);
                    double sine = Math.Sin(angle);
                    heading = new Vector3(
                        (float)(delta.X * (double)(float)cosine - sine * delta.Z),
                        delta.Y,
                        (float)(delta.X * sine + cosine * delta.Z)
                    );
                }
                else
                    heading = unit.Heading;
            }
            if (unit.IsFighter)
            {
                if (heading == Vector3.Zero)
                    return Formation.Heading;
                return TacticalCombatGeometry.NormalizeFighterHeading(heading);
            }
            else
            {
                if (heading.X == 0 && heading.Z == 0)
                    heading = unit.Heading;
                heading.Y = 0;
            }
            return TacticalCombatGeometry.Normalize(heading);
        }

        /// <summary>
        /// Calculates the group's sustained attack against a candidate.
        /// </summary>
        /// <param name="target">The candidate target.</param>
        /// <returns>The aggregate attack budget of active members.</returns>
        public float GetAttackRate(TacticalCombatUnit target)
        {
            float total = 0;
            for (int index = 0; index < Members.Count; index++)
                if (Members[index].IsTargetable)
                    total = (float)(
                        (double)total
                        + TacticalCombatEngagement.GetAttackRate(Members[index], target)
                    );
            return total;
        }

        /// <summary>
        /// Rebuilds the computed firing fallback independently of the designated primary target.
        /// </summary>
        /// <param name="computed">The computed category-specific fallback.</param>
        private void RebuildFiringTargets(TacticalCombatUnit computed)
        {
            _firingTargets.Clear();
            if (computed != null)
                _firingTargets.Add(computed);
        }

        /// <summary>
        /// Checks whether a reciprocal target remains a current formation member.
        /// </summary>
        /// <param name="unit">The opposing group's primary target.</param>
        /// <returns>Whether this formation contains the target.</returns>
        private bool ContainsMember(TacticalCombatUnit unit)
        {
            foreach (TacticalCombatUnit member in Members)
                if (ReferenceEquals(member, unit))
                    return true;
            return false;
        }

        /// <summary>
        /// Advances a capital group's reference point into its selected engagement window.
        /// </summary>
        /// <param name="target">The designated opposing unit.</param>
        /// <param name="elapsedMilliseconds">The continuous movement interval.</param>
        /// <param name="groups">The encounter's indexed group ownership.</param>
        private void AdvanceCapitalEngagement(
            TacticalCombatUnit target,
            float elapsedMilliseconds,
            IReadOnlyDictionary<int, TacticalCombatGroup> groups
        )
        {
            Vector3 delta = target.Position - Formation.Position;
            Vector3 heading;
            if (delta == Vector3.Zero)
            {
                heading = SideLane;
                delta = SideLane * -5;
            }
            else
                heading = TacticalCombatGeometry.Normalize(delta);
            float distance = TacticalCombatGeometry.GetLength(delta);
            double buffered = (double)distance + 2;
            if (buffered >= Engagement.MinimumRange && buffered <= Engagement.MaximumRange)
            {
                Formation.Stop();
                return;
            }
            if (!ActiveCloseAttack)
            {
                if (distance < Engagement.MinimumRange)
                {
                    if (
                        groups?.TryGetValue(target.GroupID, out TacticalCombatGroup opposing)
                            == true
                        && opposing.PrimaryTarget != null
                        && ContainsMember(opposing.PrimaryTarget)
                    )
                    {
                        Formation.Stop();
                        return;
                    }
                    delta = SideLane * (float)((double)Engagement.MinimumRange * -1.1f);
                }
                if (!CloseAttack && IsAutomated && Members.Count >= 8)
                    SetAttackMode(true);
            }
            Vector3 direction = TacticalCombatGeometry.Normalize(delta);
            float scalar;
            if (ActiveCloseAttack)
                scalar = Engagement.MovementDistance;
            else
            {
                float ratio =
                    Members.Count >= 6 ? 0.9f
                    : Members.Count >= 3 ? 0.75f
                    : 0.5f;
                float span = Engagement.MaximumRange - Engagement.MinimumRange;
                scalar = Math.Max((float)((double)ratio * span + Engagement.MinimumRange), 2);
            }
            Formation.MoveTowards(
                Formation.Position + direction * scalar,
                elapsedMilliseconds,
                2,
                heading
            );
        }

        /// <summary>
        /// Advances a fighter group's attack point and its close orbit around the target.
        /// </summary>
        /// <param name="target">The designated opposing unit.</param>
        /// <param name="elapsedMilliseconds">The continuous movement interval.</param>
        private void AdvanceFighterEngagement(TacticalCombatUnit target, float elapsedMilliseconds)
        {
            Vector3 delta = target.Position - Formation.Position;
            Vector3 direction = TacticalCombatGeometry.Normalize(
                delta == Vector3.Zero ? SideLane : delta
            );
            Vector3 point = Formation.Position + direction * Engagement.MovementDistance;
            if (Formation.MoveTowards(point, elapsedMilliseconds, 2, direction))
                return;
            _orbitPhase = (float)((double)elapsedMilliseconds * 3.1415927f * 0.0002f + _orbitPhase);
            double fullTurn = (double)3.1415927f + 3.1415927f;
            if (_orbitPhase >= fullTurn)
                _orbitPhase = (float)(_orbitPhase - fullTurn);
            float radius = Math.Max((float)((double)target.CollisionRadius * 2.4f), 1.5f);
            if (SideLane.Z < 0)
                _orbitPhase = -_orbitPhase;
            double cosine = Math.Cos(_orbitPhase) * radius;
            double sine = Math.Sin(_orbitPhase) * radius;
            Formation.SetPosition(
                new Vector3(
                    (float)(target.Position.X + cosine - (float)sine),
                    (float)((double)target.Position.Y + (float)((double)radius * 0.5f)),
                    (float)(
                        (double)target.Position.Z + (float)((double)(float)cosine + (float)sine)
                    )
                )
            );
        }

        /// <summary>
        /// Moves toward the departure boundary and requests due hyperspace transitions.
        /// </summary>
        /// <param name="elapsedMilliseconds">The continuous movement interval.</param>
        /// <param name="battleDepth">The battle's deployment span.</param>
        /// <param name="withdrawalMilliseconds">The side's remaining retreat delay.</param>
        private void AdvanceWithdrawal(
            float elapsedMilliseconds,
            float battleDepth,
            float withdrawalMilliseconds
        )
        {
            Vector3 point = Formation.Position;
            point.Z = (float)((double)battleDepth * -SideLane.Z * 0.5f);
            if (Formation.MoveTowards(point, elapsedMilliseconds, 1, -SideLane))
                return;
            if (withdrawalMilliseconds > 0)
                return;
            float origin = point.Z;
            int count = IsFighterGroup ? Members.Count : Math.Min(Members.Count, 1);
            for (int index = 0; index < count; index++)
            {
                TacticalCombatUnit unit = Members[index];
                if (!unit.IsTargetable || !unit.Systems.HasHyperdrive || unit.GetSpeed() == 0)
                    continue;
                float separation = (float)(-((double)origin - unit.Position.Z) * SideLane.Z);
                if (IsFighterGroup)
                    origin = separation;
                if (separation <= 8 && unit.Status != TacticalCombatUnitStatus.PreparingHyperspace)
                {
                    unit.DesiredHeading = -SideLane;
                    unit.Heading = -SideLane;
                    unit.Status = TacticalCombatUnitStatus.PreparingHyperspace;
                    HyperspaceRequested?.Invoke(unit);
                }
            }
        }
    }
}

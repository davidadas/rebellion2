using System;
using System.Collections.Generic;
using System.Numerics;
using Rebellion.Game.Combat;
using Rebellion.Game.ShipComponents;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Random;

namespace Rebellion.Simulation.Tactical
{
    /// <summary>
    /// Advances data-authored weapon banks, swept projectile contacts, and shield recovery.
    /// Owns no scene objects; the presentation observes emitted shots and impacts.
    /// </summary>
    public sealed class TacticalCombat
    {
        private const float _stepSeconds = 1f / 64f;
        private const float _firingCadenceScale = 2f;
        private readonly ActiveBattle _battle;
        private readonly IRandomNumberProvider _random;
        private readonly List<UnitEntry> _units = new List<UnitEntry>();
        private readonly Dictionary<string, UnitEntry> _byID = new Dictionary<string, UnitEntry>(
            StringComparer.Ordinal
        );
        private readonly Dictionary<(int, int, int), List<UnitEntry>> _cells = new();
        private readonly HashSet<UnitEntry> _seen = new();
        private readonly List<UnitEntry> _candidates = new();
        private readonly List<TargetCandidate> _rankedTargets = new();
        private float _cellSize = 1f;

        public event Action<BattleProjectile> ShotFired;
        public event Action<BattleProjectile, CombatUnit, bool> ShotHit;
        public event Action<CombatUnit> UnitDestroyed;

        /// <summary>
        /// Indexes battle members and resolved model-local mounts once, preserving saved bank state.
        /// </summary>
        /// <param name="battle">The persistent battle.</param>
        /// <param name="mounts">Loaded geometry resolved into each ship's presentation frame.</param>
        public TacticalCombat(ActiveBattle battle, IReadOnlyDictionary<Hardpoint, Mount> mounts)
        {
            _battle = battle ?? throw new ArgumentNullException(nameof(battle));
            if (mounts == null)
                throw new ArgumentNullException(nameof(mounts));
            _random = new SystemRandomProvider(battle.WeaponRandomSeed, battle.WeaponRandomIndex);
            foreach (BattleParticipant participant in battle.GetParticipants())
            foreach (CombatUnit unit in participant.GetCombatants())
            {
                UnitEntry entry = new UnitEntry(unit);
                _units.Add(entry);
                _byID.Add(unit.InstanceID, entry);
                ModelDimensions size = unit.GetUnit().ModelSize;
                _cellSize = Math.Max(
                    _cellSize,
                    Math.Max(size.Width, Math.Max(size.Height, size.Depth))
                );
                foreach (HardpointGroup group in GetGroups(unit))
                {
                    if (string.IsNullOrWhiteSpace(group.ID) || group.GetHardpoints().Count == 0)
                        throw new ArgumentException(
                            "Weapon groups require an ID and at least one mount.",
                            nameof(battle)
                        );
                    WeaponBankState state = unit.WeaponBanks.Find(bank => bank.GroupID == group.ID);
                    if (state == null)
                    {
                        state = new WeaponBankState { GroupID = group.ID };
                        unit.WeaponBanks.Add(state);
                    }
                    if (state.Mounts.Count == 0)
                        foreach (Hardpoint hardpoint in group.GetHardpoints())
                            state.Mounts.Add(new WeaponMountState());
                    if (state.Mounts.Count != group.GetHardpoints().Count)
                        throw new ArgumentException(
                            "Saved tracking state does not match the weapon group.",
                            nameof(battle)
                        );
                    List<MountedHardpoint> mountedHardpoints = new();
                    foreach (Hardpoint hardpoint in group.GetHardpoints())
                    {
                        ValidateWeapon(hardpoint);
                        if (!mounts.TryGetValue(hardpoint, out Mount mount))
                            throw new ArgumentException(
                                "A weapon mount has no loaded model pose.",
                                nameof(mounts)
                            );
                        entry.AcquisitionRadius = Math.Max(
                            entry.AcquisitionRadius,
                            hardpoint.Weapon.Range + mount.Position.Length()
                        );
                        _cellSize = Math.Max(_cellSize, hardpoint.Weapon.Range);
                        mountedHardpoints.Add(new MountedHardpoint(hardpoint, mount));
                    }
                    entry.Banks.Add(new Bank(mountedHardpoints, state));
                }
            }
        }

        /// <summary>
        /// Reports whether a unit still has a living hull and surviving squadron members.
        /// </summary>
        /// <param name="unit">The battle member.</param>
        /// <returns>Whether the unit is alive.</returns>
        public static bool IsAlive(CombatUnit unit) =>
            unit?.GetUnit() switch
            {
                CapitalShip ship => ship.MaxHullStrength <= 0 || ship.CurrentHullStrength > 0,
                Starfighter fighter => fighter.CurrentSquadronSize > 0
                    && (fighter.MaxHullStrength <= 0 || fighter.CurrentHullStrength > 0),
                _ => false,
            };

        /// <summary>
        /// Assigns an explicit enemy target only to deployed ships controlled by the ordering faction.
        /// </summary>
        /// <param name="selection">The ordered ships.</param>
        /// <param name="owner">The ordering faction.</param>
        /// <param name="target">The desired hostile target.</param>
        public void Attack(IReadOnlyList<CombatUnit> selection, string owner, CombatUnit target)
        {
            if (selection == null)
                throw new ArgumentNullException(nameof(selection));
            if (
                target == null
                || !_byID.ContainsKey(target.InstanceID)
                || !IsAvailable(target)
                || target.GetUnit().OwnerInstanceID == owner
            )
                return;
            foreach (CombatUnit unit in selection)
                if (
                    _byID.ContainsKey(unit.InstanceID)
                    && IsAvailable(unit)
                    && unit.GetUnit().OwnerInstanceID == owner
                )
                    unit.AttackTargetInstanceID = target.InstanceID;
        }

        /// <summary>
        /// Advances on a saved fixed-step clock; callers omit paused or arriving time.
        /// </summary>
        /// <param name="deltaTime">Elapsed unpaused battle seconds.</param>
        /// <param name="advanceMovement">Optional movement and carrier advancement on the same fixed battle clock.</param>
        public void Advance(float deltaTime, Action<float> advanceMovement = null)
        {
            if (!float.IsFinite(deltaTime) || deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            _battle.WeaponTimeRemainder += deltaTime;
            while (_battle.WeaponTimeRemainder >= _stepSeconds)
            {
                _battle.WeaponTimeRemainder -= _stepSeconds;
                advanceMovement?.Invoke(_stepSeconds);
                RebuildSpatialIndex();
                AdvanceProjectiles();
                foreach (UnitEntry entry in _units)
                {
                    if (!IsAvailable(entry.Unit))
                        continue;
                    RechargeShields(entry.Unit);
                    PrepareTargetCandidates(entry);
                    foreach (Bank bank in entry.Banks)
                        AdvanceBank(entry, bank);
                }
            }
        }

        /// <summary>
        /// Applies shield-first damage with saved fractional carry, never damaging the strategic source.
        /// </summary>
        /// <param name="target">The damaged battle member.</param>
        /// <param name="damage">Non-negative incoming damage.</param>
        private void ApplyDamage(CombatUnit target, float damage)
        {
            float shields = GetCurrentShields(target);
            float absorbed = Math.Min(shields, damage);
            float remaining = shields - absorbed;
            target.CurrentShieldStrength = (int)Math.Ceiling(remaining);
            target.ShieldDamageRemainder = target.CurrentShieldStrength.Value - remaining;
            damage -= absorbed;
            if (damage <= 0f)
                return;
            target.HullDamageRemainder += damage;
            int wholeDamage = (int)Math.Floor(target.HullDamageRemainder);
            target.HullDamageRemainder -= wholeDamage;
            if (target.GetUnit() is CapitalShip ship)
                ship.CurrentHullStrength = Math.Max(0, ship.CurrentHullStrength - wholeDamage);
            else if (target.GetUnit() is Starfighter fighter)
            {
                fighter.CurrentHullStrength = Math.Max(
                    0,
                    fighter.CurrentHullStrength - wholeDamage
                );
                if (fighter.CurrentHullStrength == 0)
                    fighter.CurrentSquadronSize = 0;
            }
            if (!IsAlive(target))
            {
                target.CurrentShieldStrength = 0;
                target.MoveDestination = null;
                target.AttackTargetInstanceID = null;
                UnitDestroyed?.Invoke(target);
            }
        }

        /// <summary>
        /// Recharges living capital-ship shields by their configured rate and remaining hull fraction.
        /// </summary>
        /// <param name="unit">The battle member receiving recovery.</param>
        private static void RechargeShields(CombatUnit unit)
        {
            if (
                unit.GetUnit() is not CapitalShip ship
                || ship.MaxHullStrength <= 0
                || ship.MaxShieldStrength <= 0
            )
                return;
            float recharge =
                Math.Max(0, ship.ShieldRechargeRate)
                * ((float)ship.CurrentHullStrength / ship.MaxHullStrength)
                * _stepSeconds;
            unit.ShieldRechargeRemainder += recharge;
            int restored = (int)unit.ShieldRechargeRemainder;
            unit.ShieldRechargeRemainder -= restored;
            unit.CurrentShieldStrength = Math.Min(
                ship.MaxShieldStrength,
                (unit.CurrentShieldStrength ?? ship.MaxShieldStrength) + restored
            );
            if (unit.CurrentShieldStrength == ship.MaxShieldStrength)
            {
                unit.ShieldDamageRemainder = 0f;
                unit.ShieldRechargeRemainder = 0f;
            }
        }

        /// <summary>
        /// Advances every gun independently, respecting its own cooldown, tracking, range and arc.
        /// </summary>
        /// <param name="entry">The shooting ship.</param>
        /// <param name="bank">The authored group containing independently timed guns.</param>
        private void AdvanceBank(UnitEntry entry, Bank bank)
        {
            IReadOnlyList<MountedHardpoint> hardpoints = bank.Hardpoints;
            for (int index = 0; index < hardpoints.Count; index++)
            {
                WeaponMountState state = bank.State.Mounts[index];
                state.Cooldown = Math.Max(0f, state.Cooldown - _stepSeconds);
                MountedHardpoint mountedHardpoint = hardpoints[index];
                Hardpoint hardpoint = mountedHardpoint.Hardpoint;
                if (hardpoint.Health <= 0)
                    continue;
                Mount mount = mountedHardpoint.Mount;
                Vector3 origin = ToWorld(entry, mount.Position);
                UnitEntry target = FindTarget(
                    entry,
                    hardpoint,
                    mount,
                    state,
                    origin,
                    mountedHardpoint.ConicalThreshold,
                    mountedHardpoint.RangeSquared
                );
                if (target == null)
                    continue;
                Vector3 aim = PredictAim(origin, target.Unit, hardpoint.Weapon.ProjectileSpeed);
                Vector3 localAimDirection = Vector3.Transform(
                    aim - origin,
                    entry.InverseOrientation
                );
                if (
                    !InLocalArc(
                        hardpoint,
                        mount,
                        localAimDirection,
                        mountedHardpoint.ConicalThreshold,
                        state,
                        true
                    )
                )
                    continue;
                if (state.Cooldown > 0f)
                    continue;
                if (Vector3.DistanceSquared(origin, aim) > mountedHardpoint.RangeSquared)
                    continue;
                Emit(entry.Unit, target.Unit, hardpoint.Weapon, origin, aim);
                state.ShotsFired++;
                WeaponData weapon = hardpoint.Weapon;
                if (state.ShotsFired < weapon.ShotsPerVolley)
                    state.Cooldown = weapon.FireIntervalSeconds * _firingCadenceScale;
                else
                {
                    state.ShotsFired = 0;
                    state.Cooldown =
                        weapon.MinimumRechargeSeconds > 0f
                            ? (
                                weapon.MinimumRechargeSeconds
                                + (weapon.MaximumRechargeSeconds - weapon.MinimumRechargeSeconds)
                                    * NextFraction()
                            ) * _firingCadenceScale
                            : weapon.FireIntervalSeconds * _firingCadenceScale;
                }
            }
        }

        /// <summary>
        /// Prefers an engageable explicit enemy, retains this mount's valid automatic target, and
        /// otherwise independently acquires the nearest unobstructed candidate available to its ship.
        /// </summary>
        /// <param name="source">The shooting ship.</param>
        /// <param name="hardpoint">The authored mount.</param>
        /// <param name="mount">Its resolved local frame.</param>
        /// <param name="state">The mount's independently persisted combat state.</param>
        /// <param name="origin">Its battle-space muzzle.</param>
        /// <param name="conicalThreshold">The cosine threshold for a fixed firing cone.</param>
        /// <param name="rangeSquared">The squared authored weapon range.</param>
        /// <returns>A valid hostile target, or null.</returns>
        private UnitEntry FindTarget(
            UnitEntry source,
            Hardpoint hardpoint,
            Mount mount,
            WeaponMountState state,
            Vector3 origin,
            float conicalThreshold,
            float rangeSquared
        )
        {
            CombatUnit shooter = source.Unit;
            string explicitID = shooter.AttackTargetInstanceID;
            UnitEntry ordered = null;
            if (
                explicitID != null
                && (!_byID.TryGetValue(explicitID, out ordered) || !IsAvailable(ordered.Unit))
            )
            {
                shooter.AttackTargetInstanceID = null;
                ordered = null;
            }
            if (
                CanEngage(source, ordered, hardpoint, mount, origin, conicalThreshold, rangeSquared)
            )
            {
                state.TargetInstanceID = ordered.Unit.InstanceID;
                return ordered;
            }

            UnitEntry retained = null;
            if (
                state.TargetInstanceID != null
                && _byID.TryGetValue(state.TargetInstanceID, out retained)
                && CanEngage(
                    source,
                    retained,
                    hardpoint,
                    mount,
                    origin,
                    conicalThreshold,
                    rangeSquared
                )
            )
                return retained;

            state.TargetInstanceID = null;
            _rankedTargets.Clear();
            foreach (AcquisitionCandidate candidate in source.TargetCandidates)
            {
                Vector3 direction = candidate.Entry.Position - origin;
                Vector3 localDirection = candidate.LocalCenterOffset - mount.Position;
                float distance = direction.LengthSquared();
                if (
                    distance <= rangeSquared
                    && InLocalArc(hardpoint, mount, localDirection, conicalThreshold, null, false)
                )
                    _rankedTargets.Add(
                        new TargetCandidate(candidate.Entry, distance, candidate.Sequence)
                    );
            }
            _rankedTargets.Sort();
            foreach (TargetCandidate candidate in _rankedTargets)
                if (
                    FindContact(origin, candidate.Entry.Position, shooter, out _) == candidate.Entry
                )
                {
                    state.TargetInstanceID = candidate.Entry.Unit.InstanceID;
                    return candidate.Entry;
                }
            return null;
        }

        /// <summary>
        /// Checks whether one mount can currently track and reach a hostile target through a clear line.
        /// </summary>
        /// <param name="source">The shooting ship.</param>
        /// <param name="target">The prospective target, or null.</param>
        /// <param name="hardpoint">The authored mount limits.</param>
        /// <param name="mount">The resolved mount frame.</param>
        /// <param name="origin">The battle-space muzzle.</param>
        /// <param name="conicalThreshold">The cosine threshold for a fixed firing cone.</param>
        /// <param name="rangeSquared">The squared authored weapon range.</param>
        /// <returns>Whether the mount can engage the target now.</returns>
        private bool CanEngage(
            UnitEntry source,
            UnitEntry target,
            Hardpoint hardpoint,
            Mount mount,
            Vector3 origin,
            float conicalThreshold,
            float rangeSquared
        )
        {
            if (
                target == null
                || !IsAvailable(target.Unit)
                || target.Unit.GetUnit().OwnerInstanceID == source.Unit.GetUnit().OwnerInstanceID
            )
                return false;
            Vector3 direction = target.Position - origin;
            Vector3 localDirection =
                Vector3.Transform(target.Position - source.Position, source.InverseOrientation)
                - mount.Position;
            return direction.LengthSquared() <= rangeSquared
                && InLocalArc(hardpoint, mount, localDirection, conicalThreshold, null, false)
                && FindContact(origin, target.Position, source.Unit, out _) == target;
        }

        /// <summary>
        /// Evaluates conical or independent yaw/pitch limits and advances bounded proportional tracking.
        /// </summary>
        /// <param name="hardpoint">The authored limits.</param>
        /// <param name="mount">The resolved mount reference frame.</param>
        /// <param name="localDirection">The desired ship-local firing direction.</param>
        /// <param name="conicalThreshold">The cosine threshold for a fixed firing cone.</param>
        /// <param name="state">The saved articulation, when tracking.</param>
        /// <param name="track">Whether to advance articulation.</param>
        /// <returns>Whether the mount can fire in this direction.</returns>
        private static bool InLocalArc(
            Hardpoint hardpoint,
            Mount mount,
            Vector3 localDirection,
            float conicalThreshold,
            WeaponMountState state,
            bool track
        )
        {
            if (localDirection.LengthSquared() < 1e-12f)
                return false;
            if (!hardpoint.Articulated)
                return Vector3.Dot(Vector3.Normalize(localDirection), mount.Forward)
                    >= conicalThreshold - 1e-6f;
            float x = Vector3.Dot(localDirection, mount.Right);
            float y = Vector3.Dot(localDirection, mount.OrthogonalUp);
            float z = Vector3.Dot(localDirection, mount.Forward);
            float yaw = (float)(Math.Atan2(x, z) * 180d / Math.PI);
            float pitch = (float)(Math.Atan2(y, Math.Sqrt(x * x + z * z)) * 180d / Math.PI);
            if (
                yaw < hardpoint.MinimumYawDegrees
                || yaw > hardpoint.MaximumYawDegrees
                || pitch < hardpoint.MinimumPitchDegrees
                || pitch > hardpoint.MaximumPitchDegrees
            )
                return false;
            if (!track)
                return true;
            float yawError = yaw - state.Yaw;
            float pitchError = pitch - state.Pitch;
            if (
                Math.Abs(yawError) <= hardpoint.AimToleranceDegrees
                && Math.Abs(pitchError) <= hardpoint.AimToleranceDegrees
            )
                return true;
            float fraction = 1f - (float)Math.Exp(-hardpoint.TrackingResponse * _stepSeconds);
            float maximumStep = hardpoint.TrackingDegreesPerSecond * _stepSeconds;
            state.Yaw += Math.Clamp(yawError * fraction, -maximumStep, maximumStep);
            state.Pitch += Math.Clamp(pitchError * fraction, -maximumStep, maximumStep);
            return false;
        }

        /// <summary>
        /// Captures a shot's characteristics at emission and resolves hitscan contacts immediately.
        /// </summary>
        /// <param name="source">The shooting ship.</param>
        /// <param name="target">The chosen hostile target.</param>
        /// <param name="weapon">The shot's immutable weapon snapshot.</param>
        /// <param name="origin">The battle-space muzzle.</param>
        /// <param name="aim">The predicted aim point.</param>
        private void Emit(
            CombatUnit source,
            CombatUnit target,
            WeaponData weapon,
            Vector3 origin,
            Vector3 aim
        )
        {
            BattleProjectile shot = new BattleProjectile
            {
                ID = ++_battle.NextShotID,
                SourceInstanceID = source.InstanceID,
                TargetInstanceID = target.InstanceID,
                OwnerInstanceID = source.GetUnit().OwnerInstanceID,
                Weapon = weapon.CreateCopy(),
                Position = FromVector(origin),
                Direction = FromVector(Vector3.Normalize(aim - origin)),
                RemainingEffectSeconds = weapon.EffectDurationSeconds,
            };
            _battle.Projectiles.Add(shot);
            ShotFired?.Invoke(shot);
            if (weapon.DeliveryMode == WeaponDeliveryMode.Hitscan)
            {
                UnitEntry hit = FindContact(
                    origin,
                    origin + ToVector(shot.Direction) * weapon.Range,
                    source,
                    out Vector3 contact
                );
                if (hit != null)
                {
                    shot.DistanceTraveled = Vector3.Distance(origin, contact);
                    Hit(shot, hit.Unit);
                }
                else
                    shot.DistanceTraveled = weapon.Range;
            }
        }

        /// <summary>
        /// Sweeps each projectile's whole traveled segment and expires shots at their authored range.
        /// </summary>
        private void AdvanceProjectiles()
        {
            for (int index = _battle.Projectiles.Count - 1; index >= 0; index--)
            {
                BattleProjectile shot = _battle.Projectiles[index];
                if (shot.Weapon.DeliveryMode == WeaponDeliveryMode.Hitscan)
                {
                    shot.RemainingEffectSeconds -= _stepSeconds;
                    if (shot.RemainingEffectSeconds <= 0f)
                        _battle.Projectiles.RemoveAt(index);
                    continue;
                }
                Vector3 origin = ToVector(shot.Position);
                float travel = Math.Min(
                    shot.Weapon.ProjectileSpeed * _stepSeconds,
                    shot.Weapon.Range - shot.DistanceTraveled
                );
                Vector3 end = origin + ToVector(shot.Direction) * travel;
                _byID.TryGetValue(shot.SourceInstanceID, out UnitEntry shooter);
                UnitEntry hit = FindContact(origin, end, shooter?.Unit, out Vector3 contact);
                if (hit != null)
                {
                    shot.Position = FromVector(contact);
                    Hit(shot, hit.Unit);
                    _battle.Projectiles.RemoveAt(index);
                }
                else
                {
                    shot.Position = FromVector(end);
                    shot.DistanceTraveled += travel;
                    if (shot.DistanceTraveled >= shot.Weapon.Range)
                        _battle.Projectiles.RemoveAt(index);
                }
            }
        }

        /// <summary>
        /// Applies one contact and notifies presentation independently of source survival.
        /// </summary>
        /// <param name="shot">The contacting shot.</param>
        /// <param name="target">The first hull encountered.</param>
        private void Hit(BattleProjectile shot, CombatUnit target)
        {
            float damage = shot.Weapon.Damage;
            if (target.GetUnit().OwnerInstanceID == shot.OwnerInstanceID)
                damage *= 0.5f;
            bool absorbedByShield = GetCurrentShields(target) > 0f;
            ApplyDamage(target, damage);
            ShotHit?.Invoke(shot, target, absorbedByShield);
        }

        /// <summary>
        /// Finds the earliest oriented-hull intersection using the current spatial index.
        /// </summary>
        /// <param name="start">The segment origin.</param>
        /// <param name="end">The segment endpoint.</param>
        /// <param name="source">The ignored shooting hull.</param>
        /// <param name="contact">The earliest contact point.</param>
        /// <returns>The contacted hull, or null.</returns>
        private UnitEntry FindContact(
            Vector3 start,
            Vector3 end,
            CombatUnit source,
            out Vector3 contact
        )
        {
            Query(Vector3.Min(start, end), Vector3.Max(start, end));
            UnitEntry nearest = null;
            float nearestFraction = float.PositiveInfinity;
            foreach (UnitEntry entry in _candidates)
            {
                CombatUnit unit = entry.Unit;
                if (unit == source || !IsAvailable(unit))
                    continue;
                if (!MayIntersectSphere(start, end, entry.Position, entry.RadiusSquared))
                    continue;
                Vector3 localStart = Vector3.Transform(
                    start - entry.Position,
                    entry.InverseOrientation
                );
                Vector3 localEnd = Vector3.Transform(
                    end - entry.Position,
                    entry.InverseOrientation
                );
                if (
                    IntersectBox(localStart, localEnd, entry.Extents, out float fraction)
                    && fraction < nearestFraction
                )
                {
                    nearest = entry;
                    nearestFraction = fraction;
                }
            }
            contact = nearest == null ? end : Vector3.Lerp(start, end, nearestFraction);
            return nearest;
        }

        /// <summary>
        /// Conservatively rejects a segment that cannot reach an oriented hull's bounding sphere.
        /// </summary>
        /// <param name="start">The segment origin.</param>
        /// <param name="end">The segment endpoint.</param>
        /// <param name="center">The hull center.</param>
        /// <param name="radiusSquared">The squared radius enclosing the complete hull.</param>
        /// <returns>Whether the segment can still intersect the enclosed hull.</returns>
        private static bool MayIntersectSphere(
            Vector3 start,
            Vector3 end,
            Vector3 center,
            float radiusSquared
        )
        {
            Vector3 segment = end - start;
            float lengthSquared = segment.LengthSquared();
            float fraction =
                lengthSquared <= 1e-12f
                    ? 0f
                    : Math.Clamp(Vector3.Dot(center - start, segment) / lengthSquared, 0f, 1f);
            Vector3 nearest = start + segment * fraction;
            float tolerance = Math.Max(1e-6f, radiusSquared * 1e-6f);
            return Vector3.DistanceSquared(nearest, center) <= radiusSquared + tolerance;
        }

        /// <summary>
        /// Computes a continuous segment intersection against a centered box without endpoint tunneling.
        /// </summary>
        /// <param name="start">The local segment origin.</param>
        /// <param name="end">The local segment endpoint.</param>
        /// <param name="extents">The hull's half dimensions.</param>
        /// <param name="fraction">The first contact fraction along the segment.</param>
        /// <returns>Whether a contact exists.</returns>
        private static bool IntersectBox(
            Vector3 start,
            Vector3 end,
            Vector3 extents,
            out float fraction
        )
        {
            fraction = 0f;
            float maximum = 1f;
            Vector3 delta = end - start;
            for (int axis = 0; axis < 3; axis++)
            {
                float origin =
                    axis == 0 ? start.X
                    : axis == 1 ? start.Y
                    : start.Z;
                float direction =
                    axis == 0 ? delta.X
                    : axis == 1 ? delta.Y
                    : delta.Z;
                float extent =
                    axis == 0 ? extents.X
                    : axis == 1 ? extents.Y
                    : extents.Z;
                if (Math.Abs(direction) < 1e-9f)
                {
                    if (origin < -extent || origin > extent)
                        return false;
                    continue;
                }
                float first = (-extent - origin) / direction;
                float last = (extent - origin) / direction;
                fraction = Math.Max(fraction, Math.Min(first, last));
                maximum = Math.Min(maximum, Math.Max(first, last));
                if (fraction > maximum)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Resolves one broad-phase hostile candidate list for all hardpoints on a ship this step.
        /// </summary>
        /// <param name="source">The ship whose hardpoints will independently select targets.</param>
        private void PrepareTargetCandidates(UnitEntry source)
        {
            source.TargetCandidates.Clear();
            if (source.AcquisitionRadius <= 0f)
                return;
            Vector3 center = source.Position;
            Vector3 extent = new Vector3(source.AcquisitionRadius);
            Query(center - extent, center + extent);
            int sequence = 0;
            foreach (UnitEntry candidate in _candidates)
                if (
                    candidate != source
                    && IsAvailable(candidate.Unit)
                    && candidate.Unit.GetUnit().OwnerInstanceID
                        != source.Unit.GetUnit().OwnerInstanceID
                )
                {
                    source.TargetCandidates.Add(
                        new AcquisitionCandidate(
                            candidate,
                            Vector3.Transform(
                                candidate.Position - source.Position,
                                source.InverseOrientation
                            ),
                            sequence
                        )
                    );
                    sequence++;
                }
        }

        /// <summary>
        /// Reuses spatial buckets for target acquisition and swept contacts without scene-graph queries.
        /// </summary>
        private void RebuildSpatialIndex()
        {
            foreach (List<UnitEntry> cell in _cells.Values)
                cell.Clear();
            foreach (UnitEntry entry in _units)
            {
                if (!IsAvailable(entry.Unit))
                    continue;
                ModelDimensions size = entry.Unit.GetUnit().ModelSize;
                entry.Position = ToVector(entry.Unit.Position);
                entry.Orientation = Orientation(entry.Unit);
                entry.InverseOrientation = Quaternion.Inverse(entry.Orientation);
                entry.Extents = new Vector3(size.Width, size.Height, size.Depth) * 0.5f;
                entry.RadiusSquared = entry.Extents.LengthSquared();
                float radius = (float)Math.Sqrt(entry.RadiusSquared);
                (int X, int Y, int Z) minimum = Cell(entry.Position - new Vector3(radius));
                (int X, int Y, int Z) maximum = Cell(entry.Position + new Vector3(radius));
                for (int x = minimum.X; x <= maximum.X; x++)
                for (int y = minimum.Y; y <= maximum.Y; y++)
                for (int z = minimum.Z; z <= maximum.Z; z++)
                {
                    if (!_cells.TryGetValue((x, y, z), out List<UnitEntry> cell))
                    {
                        cell = new List<UnitEntry>();
                        _cells.Add((x, y, z), cell);
                    }
                    cell.Add(entry);
                }
            }
        }

        /// <summary>
        /// Collects unique nearby hulls into reusable storage for one spatial query.
        /// </summary>
        /// <param name="minimum">The query's minimum battle coordinate.</param>
        /// <param name="maximum">The query's maximum battle coordinate.</param>
        private void Query(Vector3 minimum, Vector3 maximum)
        {
            _seen.Clear();
            _candidates.Clear();
            (int X, int Y, int Z) low = Cell(minimum);
            (int X, int Y, int Z) high = Cell(maximum);
            for (int x = low.X; x <= high.X; x++)
            for (int y = low.Y; y <= high.Y; y++)
            for (int z = low.Z; z <= high.Z; z++)
                if (_cells.TryGetValue((x, y, z), out List<UnitEntry> cell))
                    foreach (UnitEntry entry in cell)
                        if (_seen.Add(entry))
                            _candidates.Add(entry);
        }

        /// <summary>
        /// Maps a battle point to its spatial bucket.
        /// </summary>
        /// <param name="position">The battle point.</param>
        /// <returns>The integer bucket coordinates.</returns>
        private (int, int, int) Cell(Vector3 position) =>
            (
                (int)Math.Floor(position.X / _cellSize),
                (int)Math.Floor(position.Y / _cellSize),
                (int)Math.Floor(position.Z / _cellSize)
            );

        /// <summary>
        /// Predicts a constant-velocity intercept without allowing an impossible aim beyond weapon range.
        /// </summary>
        /// <param name="origin">The muzzle position.</param>
        /// <param name="target">The moving target.</param>
        /// <param name="speed">The authored projectile speed.</param>
        /// <returns>The intercept point, or the current target position when interception is impossible.</returns>
        private static Vector3 PredictAim(Vector3 origin, CombatUnit target, float speed)
        {
            Vector3 position = ToVector(target.Position);
            if (speed <= 0f || target.MoveDestination == null)
                return position;
            float targetSpeed = TacticalMovement.GetSpeed(target);
            Vector3 velocity = Vector3.Normalize(ToVector(target.Forward)) * targetSpeed;
            Vector3 displacement = position - origin;
            float a = velocity.LengthSquared() - speed * speed;
            float b = 2f * Vector3.Dot(displacement, velocity);
            float c = displacement.LengthSquared();
            float discriminant = b * b - 4f * a * c;
            if (discriminant < 0f)
                return position;
            float time;
            if (Math.Abs(a) < 1e-6f)
                time = Math.Abs(b) > 1e-6f ? -c / b : -1f;
            else
            {
                float root = (float)Math.Sqrt(discriminant);
                float first = (-b - root) / (2f * a);
                float second = (-b + root) / (2f * a);
                time = first > 0f ? first : second;
                if (second > 0f && (time <= 0f || second < time))
                    time = second;
            }
            return time > 0f ? position + velocity * time : position;
        }

        /// <summary>
        /// Returns a deterministic recharge draw and preserves its stream position.
        /// </summary>
        /// <returns>A fraction in the half-open interval from zero to one.</returns>
        private float NextFraction()
        {
            _battle.WeaponRandomIndex++;
            return (float)_random.NextDouble();
        }

        /// <summary>
        /// Rejects invalid authored tuning instead of silently arming a default weapon.
        /// </summary>
        /// <param name="hardpoint">The mount to validate.</param>
        private static void ValidateWeapon(Hardpoint hardpoint)
        {
            WeaponData weapon = hardpoint?.Weapon;
            if (
                weapon == null
                || !float.IsFinite(weapon.Damage)
                || weapon.Damage <= 0f
                || weapon.WeaponType is HardpointWeaponType.None or HardpointWeaponType.TractorBeam
                || !Enum.IsDefined(typeof(WeaponDeliveryMode), weapon.DeliveryMode)
                || !float.IsFinite(weapon.Range)
                || weapon.Range <= 0f
                || !float.IsFinite(weapon.FireIntervalSeconds)
                || weapon.FireIntervalSeconds <= 0f
                || weapon.ShotsPerVolley <= 0
                || !float.IsFinite(weapon.ProjectileSpeed)
                || (
                    weapon.DeliveryMode == WeaponDeliveryMode.Projectile
                    && weapon.ProjectileSpeed <= 0f
                )
                || weapon.MinimumRechargeSeconds < 0f
                || !float.IsFinite(weapon.MinimumRechargeSeconds)
                || !float.IsFinite(weapon.MaximumRechargeSeconds)
                || weapon.MaximumRechargeSeconds < weapon.MinimumRechargeSeconds
                || !float.IsFinite(hardpoint.FiringArcDegrees)
                || hardpoint.FiringArcDegrees < 0f
                || hardpoint.FiringArcDegrees > 360f
            )
                throw new ArgumentException(
                    "Weapon tuning must have finite positive damage, range, cadence, and projectile speed.",
                    nameof(hardpoint)
                );
        }

        /// <summary>
        /// Returns authored banks from either supported ship kind.
        /// </summary>
        /// <param name="unit">The battle member.</param>
        /// <returns>The authored weapon groups.</returns>
        public static IReadOnlyList<HardpointGroup> GetGroups(CombatUnit unit) =>
            unit.GetUnit() is CapitalShip ship
                ? ship.GetHardpointGroups()
                : ((Starfighter)unit.GetUnit()).GetHardpointGroups();

        /// <summary>
        /// Reports whether a hull is present in open battle space and can participate in encounters.
        /// </summary>
        /// <param name="unit">The battle member.</param>
        /// <returns>Whether the unit can shoot or receive a hit.</returns>
        private static bool IsAvailable(CombatUnit unit) =>
            !unit.HasRetreated
            && IsAlive(unit)
            && unit.FlightState != FighterFlightState.Docked
            && unit.FlightState != FighterFlightState.Queued
            && !(unit.FlightState == FighterFlightState.Launching && unit.FlightDelay > 0f);

        /// <summary>
        /// Returns the authored maximum shield strength.
        /// </summary>
        /// <param name="unit">The battle member.</param>
        /// <returns>The non-negative shield capacity.</returns>
        public static int GetMaximumShields(CombatUnit unit) =>
            Math.Max(
                0,
                unit.GetUnit() is CapitalShip ship
                    ? ship.MaxShieldStrength
                    : ((Starfighter)unit.GetUnit()).ShieldStrength
            );

        /// <summary>
        /// Returns the battle member's current fractional shield strength.
        /// </summary>
        /// <param name="unit">The battle member.</param>
        /// <returns>The non-negative shield strength before the next hit.</returns>
        private static float GetCurrentShields(CombatUnit unit) =>
            Math.Max(
                0f,
                (unit.CurrentShieldStrength ?? GetMaximumShields(unit)) - unit.ShieldDamageRemainder
            );

        /// <summary>
        /// Resolves a ship's forward vector into its orthonormal battle orientation.
        /// </summary>
        /// <param name="unit">The battle member.</param>
        /// <returns>The ship-local to battle-space rotation.</returns>
        private static Quaternion Orientation(CombatUnit unit)
        {
            Vector3 forward = Vector3.Normalize(ToVector(unit.Forward));
            Vector3 reference = Math.Abs(forward.Y) > 0.999f ? Vector3.UnitZ : Vector3.UnitY;
            Vector3 right = Vector3.Normalize(Vector3.Cross(reference, forward));
            Vector3 up = Vector3.Cross(forward, right);
            return Quaternion.CreateFromRotationMatrix(
                new Matrix4x4(
                    right.X,
                    right.Y,
                    right.Z,
                    0f,
                    up.X,
                    up.Y,
                    up.Z,
                    0f,
                    forward.X,
                    forward.Y,
                    forward.Z,
                    0f,
                    0f,
                    0f,
                    0f,
                    1f
                )
            );
        }

        /// <summary>
        /// Transforms one resolved local muzzle position into battle coordinates.
        /// </summary>
        /// <param name="source">The indexed shooting ship.</param>
        /// <param name="local">The ship-local point.</param>
        /// <returns>The battle-space point.</returns>
        private static Vector3 ToWorld(UnitEntry source, Vector3 local) =>
            source.Position + Vector3.Transform(local, source.Orientation);

        /// <summary>
        /// Converts a saved vector into a simulation value.
        /// </summary>
        /// <param name="value">The saved vector.</param>
        /// <returns>The simulation vector.</returns>
        private static Vector3 ToVector(BattleVector3 value) =>
            new Vector3(value.X, value.Y, value.Z);

        /// <summary>
        /// Checks model-resolved coordinates before they enter spatial or angular calculations.
        /// </summary>
        /// <param name="value">The resolved vector.</param>
        /// <returns>Whether all components are finite.</returns>
        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

        /// <summary>
        /// Converts a simulation value into saved battle data.
        /// </summary>
        /// <param name="value">The simulation vector.</param>
        /// <returns>The saved vector.</returns>
        private static BattleVector3 FromVector(Vector3 value) =>
            new BattleVector3
            {
                X = value.X,
                Y = value.Y,
                Z = value.Z,
            };

        /// <summary>
        /// Holds a mount resolved from its loaded model into ship-local coordinates.
        /// </summary>
        public readonly struct Mount
        {
            public Vector3 Position { get; }
            public Vector3 Forward { get; }
            public Vector3 Up { get; }
            public Vector3 Right { get; }
            public Vector3 OrthogonalUp { get; }

            /// <summary>
            /// Captures the model-resolved position and orthogonal orientation.
            /// </summary>
            /// <param name="position">The ship-local muzzle.</param>
            /// <param name="forward">The ship-local barrel reference direction.</param>
            /// <param name="up">The ship-local mount up direction.</param>
            public Mount(Vector3 position, Vector3 forward, Vector3 up)
            {
                if (
                    !IsFinite(position)
                    || !IsFinite(forward)
                    || !IsFinite(up)
                    || forward.LengthSquared() < 1e-12f
                    || Vector3.Cross(up, forward).LengthSquared() < 1e-12f
                )
                    throw new ArgumentException(
                        "Weapon mounts require a finite position and independent orientation axes."
                    );
                Position = position;
                Forward = Vector3.Normalize(forward);
                Up = Vector3.Normalize(up);
                Right = Vector3.Normalize(Vector3.Cross(Up, Forward));
                OrthogonalUp = Vector3.Cross(Forward, Right);
            }
        }

        /// <summary>
        /// Holds one ship-level hostile candidate in the source ship's local frame.
        /// </summary>
        private readonly struct AcquisitionCandidate
        {
            public UnitEntry Entry { get; }
            public Vector3 LocalCenterOffset { get; }
            public int Sequence { get; }

            /// <summary>
            /// Captures one broad-phase candidate and its stable discovery order.
            /// </summary>
            /// <param name="entry">The prospective target.</param>
            /// <param name="localCenterOffset">Its center relative to the source ship.</param>
            /// <param name="sequence">The stable broad-phase discovery position.</param>
            public AcquisitionCandidate(UnitEntry entry, Vector3 localCenterOffset, int sequence)
            {
                Entry = entry;
                LocalCenterOffset = localCenterOffset;
                Sequence = sequence;
            }
        }

        /// <summary>
        /// Orders one mount's eligible targets by distance while retaining broad-phase tie order.
        /// </summary>
        private readonly struct TargetCandidate : IComparable<TargetCandidate>
        {
            public UnitEntry Entry { get; }
            private float Distance { get; }
            private int Sequence { get; }

            /// <summary>
            /// Captures one hardpoint-specific candidate ranking.
            /// </summary>
            /// <param name="entry">The prospective target.</param>
            /// <param name="distance">Squared distance from this mount's muzzle.</param>
            /// <param name="sequence">The stable broad-phase discovery position.</param>
            public TargetCandidate(UnitEntry entry, float distance, int sequence)
            {
                Entry = entry;
                Distance = distance;
                Sequence = sequence;
            }

            /// <summary>
            /// Compares distance first and broad-phase discovery order second.
            /// </summary>
            /// <param name="other">The other candidate.</param>
            /// <returns>The relative target preference.</returns>
            public int CompareTo(TargetCandidate other)
            {
                int distance = Distance.CompareTo(other.Distance);
                return distance != 0 ? distance : Sequence.CompareTo(other.Sequence);
            }
        }

        /// <summary>
        /// Binds immutable targeting values to one resolved weapon mount.
        /// </summary>
        private readonly struct MountedHardpoint
        {
            public Hardpoint Hardpoint { get; }
            public Mount Mount { get; }
            public float ConicalThreshold { get; }
            public float RangeSquared { get; }

            /// <summary>
            /// Resolves invariant range and firing-cone values once when combat is created.
            /// </summary>
            /// <param name="hardpoint">The authored weapon mount.</param>
            /// <param name="mount">Its resolved local geometry.</param>
            public MountedHardpoint(Hardpoint hardpoint, Mount mount)
            {
                Hardpoint = hardpoint;
                Mount = mount;
                ConicalThreshold = (float)Math.Cos(hardpoint.FiringArcDegrees * Math.PI / 360d);
                RangeSquared = hardpoint.Weapon.Range * hardpoint.Weapon.Range;
            }
        }

        private sealed class UnitEntry
        {
            public CombatUnit Unit { get; }
            public List<Bank> Banks { get; } = new List<Bank>();
            public List<AcquisitionCandidate> TargetCandidates { get; } =
                new List<AcquisitionCandidate>();
            public float AcquisitionRadius { get; set; }
            public Vector3 Position { get; set; }
            public Quaternion Orientation { get; set; }
            public Quaternion InverseOrientation { get; set; }
            public Vector3 Extents { get; set; }
            public float RadiusSquared { get; set; }

            /// <summary>
            /// Indexes one battle member without copying its mutable state.
            /// </summary>
            /// <param name="unit">The battle member.</param>
            public UnitEntry(CombatUnit unit) => Unit = unit;
        }

        private sealed class Bank
        {
            public IReadOnlyList<MountedHardpoint> Hardpoints { get; }
            public WeaponBankState State { get; }

            /// <summary>
            /// Binds immutable authored bank data to its saved runtime state.
            /// </summary>
            /// <param name="hardpoints">The bank's resolved weapon mounts.</param>
            /// <param name="state">The saved cadence and articulation.</param>
            public Bank(IReadOnlyList<MountedHardpoint> hardpoints, WeaponBankState state)
            {
                Hardpoints = hardpoints;
                State = state;
            }
        }
    }
}

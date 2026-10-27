using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.ShipComponents;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Tactical;
using Rebellion.Util.Serialization;

namespace Rebellion.Tests.Tactical
{
    [TestFixture]
    public sealed class TacticalCombatTests
    {
        private ActiveBattle _battle;
        private CombatUnit _attacker;
        private CombatUnit _defender;

        /// <summary>
        /// Creates two independent opposing hulls and one explicitly authored weapon bank.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _battle = new ActiveBattle { Kind = BattleKind.Space, WeaponRandomSeed = 1234 };
            CapitalShip attacker = CreateShip("A", "OWNER");
            HardpointGroup group = new HardpointGroup { ID = "main" };
            group.GetHardpoints().Add(CreateMount());
            attacker.GetHardpointGroups().Add(group);
            _attacker = _battle.AddCombatants(attacker)[0];
            _defender = _battle.AddCombatants(CreateShip("B", "ENEMY"))[0];
            _defender.Position.Z = 10f;
        }

        /// <summary>
        /// Verifies a target beyond the configured muzzle range does not emit a shot.
        /// </summary>
        [Test]
        public void Advance_TargetOutsideRange_DoesNotFire()
        {
            _defender.Position.Z = 20.01f;
            CreateCombat(_battle).Advance(1f);
            Assert.That(_battle.NextShotID, Is.Zero);
        }

        /// <summary>
        /// Verifies the configured range boundary remains inclusive.
        /// </summary>
        [Test]
        public void Advance_TargetAtRangeBoundary_Fires()
        {
            _defender.Position.Z = 20f;
            CreateCombat(_battle).Advance(1f / 64f);
            Assert.That(_battle.NextShotID, Is.EqualTo(1));
        }

        /// <summary>
        /// Verifies an idle mount independently acquires and retains a nearby hostile target.
        /// </summary>
        [Test]
        public void Advance_NoExplicitTarget_AcquiresMountTargetAndFires()
        {
            CreateCombat(_battle).Advance(1f / 64f);

            Assert.That(_battle.NextShotID, Is.EqualTo(1));
            Assert.That(
                _attacker.Weapons.Banks[0].Mounts[0].TargetInstanceID,
                Is.EqualTo(_defender.InstanceID)
            );
        }

        /// <summary>
        /// Verifies a rear target cannot be attacked by a forward conical mount.
        /// </summary>
        [Test]
        public void Advance_TargetOutsideArc_DoesNotFire()
        {
            _defender.Position.Z = -10f;
            CreateCombat(_battle).Advance(1f);
            Assert.That(_battle.NextShotID, Is.Zero);
        }

        /// <summary>
        /// Verifies range is evaluated from the muzzle rather than the ship center.
        /// </summary>
        [Test]
        public void Advance_OffsetMuzzle_UsesMuzzleRange()
        {
            _defender.Position.Z = 25f;
            Mount().Position.Z = 5f;
            CreateCombat(_battle).Advance(1f / 64f);
            Assert.That(_battle.NextShotID, Is.EqualTo(1));
        }

        /// <summary>
        /// Verifies ship heading rotates its local firing envelope.
        /// </summary>
        [Test]
        public void Advance_RotatedHull_RotatesFiringArc()
        {
            _attacker.Forward = new BattleVector3 { X = 1f };
            _defender.Position = new BattleVector3 { X = 10f };
            CreateCombat(_battle).Advance(1f / 64f);
            Assert.That(_battle.NextShotID, Is.EqualTo(1));
        }

        /// <summary>
        /// Verifies a ship can fire during the same fixed step in which it advances.
        /// </summary>
        [Test]
        public void Advance_MovingShooterInRange_FiresWithoutStopping()
        {
            _attacker.Data.SublightSpeed = 2;
            _attacker.MoveOrder.Destination = new BattleVector3 { Z = 100f };
            CreateCombat(_battle)
                .Advance(1f / 64f, deltaTime => TacticalMovement.Advance(_attacker, deltaTime));
            Assert.That(_attacker.MoveOrder.Destination, Is.Not.Null);
            Assert.That(_battle.NextShotID, Is.EqualTo(1));
        }

        /// <summary>
        /// Verifies an ordered ship keeps approaching its explicit target while a mount independently
        /// engages a different hostile already inside its weapon envelope.
        /// </summary>
        [Test]
        public void Advance_ExplicitTargetOutsideRange_FiresAtNearbyEnemyWhileMoving()
        {
            _defender.Position.Z = 100f;
            CombatUnit nearby = _battle.AddCombatants(CreateShip("C", "ENEMY"))[0];
            nearby.Position.Z = 10f;
            _attacker.Data.SublightSpeed = 2;
            _attacker.MoveOrder.Destination = new BattleVector3 { Z = 100f };
            TacticalCombat combat = CreateCombat(_battle);
            combat.Attack(new[] { _attacker }, "OWNER", _defender);

            combat.Advance(1f / 64f, deltaTime => TacticalMovement.Advance(_attacker, deltaTime));

            Assert.That(_attacker.Weapons.AttackTargetInstanceID, Is.EqualTo(_defender.InstanceID));
            Assert.That(_attacker.MoveOrder.Destination, Is.Not.Null);
            Assert.That(_battle.Projectiles[0].TargetInstanceID, Is.EqualTo(nearby.InstanceID));
            Assert.That(
                _attacker.Weapons.Banks[0].Mounts[0].TargetInstanceID,
                Is.EqualTo(nearby.InstanceID)
            );
        }

        /// <summary>
        /// Verifies separate muzzle positions independently choose their nearest valid targets from
        /// one ship-level candidate set.
        /// </summary>
        [Test]
        public void Advance_DifferentHardpoints_AcquireIndependentTargets()
        {
            Hardpoint left = Mount();
            left.FiringArcDegrees = 360f;
            left.Position.X = -5f;
            Hardpoint right = CreateMount();
            right.FiringArcDegrees = 360f;
            right.Position.X = 5f;
            TacticalCombat.GetGroups(_attacker)[0].GetHardpoints().Add(right);
            _defender.Position = new BattleVector3 { X = -5f, Z = 10f };
            CombatUnit second = _battle.AddCombatants(CreateShip("C", "ENEMY"))[0];
            second.Position = new BattleVector3 { X = 5f, Z = 10f };

            CreateCombat(_battle).Advance(1f / 64f);

            Assert.That(
                _attacker.Weapons.Banks[0].Mounts[0].TargetInstanceID,
                Is.EqualTo(_defender.InstanceID)
            );
            Assert.That(
                _attacker.Weapons.Banks[0].Mounts[1].TargetInstanceID,
                Is.EqualTo(second.InstanceID)
            );
        }

        /// <summary>
        /// Verifies an engageable automatic target remains stable when another candidate becomes nearer.
        /// </summary>
        [Test]
        public void Advance_AutomaticTargetRemainsEngageable_RetainsMountTarget()
        {
            Mount().FiringArcDegrees = 360f;
            CombatUnit second = _battle.AddCombatants(CreateShip("C", "ENEMY"))[0];
            second.Position = new BattleVector3 { X = 5f, Z = 15f };
            TacticalCombat combat = CreateCombat(_battle);
            combat.Advance(1f / 64f);
            second.Position = new BattleVector3 { X = 5f, Z = 5f };

            combat.Advance(1f / 64f);

            Assert.That(
                _attacker.Weapons.Banks[0].Mounts[0].TargetInstanceID,
                Is.EqualTo(_defender.InstanceID)
            );
        }

        /// <summary>
        /// Verifies opportunity fire does not displace an explicit target a gun can already engage.
        /// </summary>
        [Test]
        public void Advance_ExplicitTargetInsideEnvelope_PrefersOrderedTargetOverNearerEnemy()
        {
            _defender.Position.Z = 20f;
            CombatUnit nearby = _battle.AddCombatants(CreateShip("C", "ENEMY"))[0];
            nearby.Position = new BattleVector3 { X = 2f, Z = 10f };
            TacticalCombat combat = CreateCombat(_battle);
            combat.Attack(new[] { _attacker }, "OWNER", _defender);

            combat.Advance(1f / 64f);

            Assert.That(_battle.Projectiles[0].TargetInstanceID, Is.EqualTo(_defender.InstanceID));
        }

        /// <summary>
        /// Verifies predictive aiming does not mistake a moving target's future intercept point for an obstructed line of fire.
        /// </summary>
        [Test]
        public void Advance_ApproachingTargetInRange_FiresAtPredictedPosition()
        {
            _defender.Data.SublightSpeed = 3;
            _defender.Forward = new BattleVector3 { Z = -1f };
            _defender.MoveOrder.Destination = new BattleVector3 { Z = -100f };
            CreateCombat(_battle)
                .Advance(1f / 64f, deltaTime => TacticalMovement.Advance(_defender, deltaTime));
            Assert.That(_defender.MoveOrder.Destination, Is.Not.Null);
            Assert.That(_battle.NextShotID, Is.EqualTo(1));
        }

        /// <summary>
        /// Verifies projectile damage is not applied at emission.
        /// </summary>
        [Test]
        public void Advance_ProjectileInFlight_DoesNotDamageBeforeContact()
        {
            CreateCombat(_battle).Advance(0.5f);
            Assert.That(_defender.Condition.CurrentHullStrength, Is.EqualTo(100));
            Assert.That(
                _battle.Projectiles[0].DistanceTraveled,
                Is.EqualTo(10f * (0.5f - 1f / 64f)).Within(0.0001f)
            );
        }

        /// <summary>
        /// Verifies a fast projectile still hits a thin hull crossed inside one fixed step.
        /// </summary>
        [Test]
        public void Advance_FastProjectile_SweepsThroughThinTarget()
        {
            Mount().Weapon.Projectile.Speed = 10000f;
            CreateCombat(_battle).Advance(2f / 64f);
            Assert.That(_defender.Condition.CurrentHullStrength, Is.EqualTo(90));
        }

        /// <summary>
        /// Verifies contact consumes shields before applying overflow to the hull.
        /// </summary>
        [Test]
        public void Advance_ShieldedTarget_AppliesOnlyOverflowToHull()
        {
            _defender.Condition.CurrentShieldStrength = 6;
            Mount().Weapon.Projectile.DeliveryMode = WeaponDeliveryMode.Hitscan;
            bool? absorbedByShield = null;
            TacticalCombat combat = CreateCombat(_battle);
            combat.ShotHit += (_, _, absorbed) => absorbedByShield = absorbed;
            combat.Advance(1f / 64f);
            Assert.That(_defender.Condition.CurrentShieldStrength, Is.Zero);
            Assert.That(_defender.Condition.CurrentHullStrength, Is.EqualTo(96));
            Assert.That(absorbedByShield, Is.True);
        }

        /// <summary>
        /// Verifies small hits retain fractions rather than truncating each shot to zero.
        /// </summary>
        [Test]
        public void Advance_FractionalDamage_AccumulatesAcrossShots()
        {
            Mount().Weapon.Projectile.Damage = 0.25f;
            Mount().Weapon.Projectile.DeliveryMode = WeaponDeliveryMode.Hitscan;
            Mount().Weapon.FireIntervalSeconds = 1f / 64f;
            CreateCombat(_battle).Advance(8f / 64f);
            Assert.That(_defender.Condition.CurrentHullStrength, Is.EqualTo(99));
        }

        /// <summary>
        /// Verifies guns in the same group can fire together rather than taking turns.
        /// </summary>
        [Test]
        public void Advance_MultipleGunsInGroup_FiresEachReadyGun()
        {
            TacticalCombat.GetGroups(_attacker)[0].GetHardpoints().Add(CreateMount());
            CreateCombat(_battle).Advance(0.5f);
            Assert.That(_battle.NextShotID, Is.EqualTo(2));
        }

        /// <summary>
        /// Verifies a slow gun cannot throttle another gun in its group.
        /// </summary>
        [Test]
        public void Advance_DifferentGunCooldowns_RechargesIndependently()
        {
            Hardpoint second = CreateMount();
            second.Weapon.FireIntervalSeconds = 0.125f;
            TacticalCombat.GetGroups(_attacker)[0].GetHardpoints().Add(second);
            CreateCombat(_battle).Advance(0.5f);
            Assert.That(_battle.NextShotID, Is.EqualTo(3));
            Assert.That(_attacker.Weapons.Banks[0].Mounts[0].Cooldown, Is.GreaterThan(1.5f));
            Assert.That(_attacker.Weapons.Banks[0].Mounts[1].Cooldown, Is.LessThan(0.25f));
        }

        /// <summary>
        /// Verifies an out-of-range gun neither fires nor transfers its shots to a usable gun.
        /// </summary>
        [Test]
        public void Advance_OneGunOutOfRange_DoesNotTransferItsShots()
        {
            Hardpoint second = CreateMount();
            second.Weapon.Range = 5f;
            TacticalCombat.GetGroups(_attacker)[0].GetHardpoints().Add(second);
            CreateCombat(_battle).Advance(0.5f);
            Assert.That(_battle.NextShotID, Is.EqualTo(1));
            Assert.That(_attacker.Weapons.Banks[0].Mounts[1].ShotsFired, Is.Zero);
        }

        /// <summary>
        /// Verifies full volleys wait for their authored recharge interval.
        /// </summary>
        [Test]
        public void Advance_CompletedVolley_WaitsForRecharge()
        {
            WeaponData weapon = Mount().Weapon;
            weapon.FireIntervalSeconds = 0.125f;
            weapon.ShotsPerVolley = 2;
            weapon.MinimumRechargeSeconds = 1f;
            weapon.MaximumRechargeSeconds = 1f;
            CreateCombat(_battle).Advance(0.5f);
            Assert.That(_battle.NextShotID, Is.EqualTo(2));
            Assert.That(_attacker.Weapons.Banks[0].Mounts[0].Cooldown, Is.GreaterThan(1.5f));
        }

        /// <summary>
        /// Verifies tactical cadence doubles runtime delays without rewriting authored weapon data.
        /// </summary>
        [Test]
        public void Advance_AuthoredFireInterval_SimulatesAtHalfCadence()
        {
            WeaponData weapon = Mount().Weapon;
            weapon.FireIntervalSeconds = 0.125f;
            TacticalCombat combat = CreateCombat(_battle);

            combat.Advance(1f / 64f);
            combat.Advance(0.125f);

            Assert.That(_battle.NextShotID, Is.EqualTo(1));
            Assert.That(weapon.FireIntervalSeconds, Is.EqualTo(0.125f));
            Assert.That(
                _attacker.Weapons.Banks[0].Mounts[0].Cooldown,
                Is.EqualTo(0.125f).Within(0.0001f)
            );

            combat.Advance(0.125f);

            Assert.That(_battle.NextShotID, Is.EqualTo(2));
        }

        /// <summary>
        /// Verifies a disabled hardpoint cannot fire.
        /// </summary>
        [Test]
        public void Advance_DestroyedMount_DoesNotFire()
        {
            Mount().Health = 0;
            CreateCombat(_battle).Advance(1f);
            Assert.That(_battle.NextShotID, Is.Zero);
        }

        /// <summary>
        /// Verifies allies blocking the muzzle-to-target segment prevent firing through the formation.
        /// </summary>
        [Test]
        public void Advance_FriendlyHullBlocksTarget_DoesNotFire()
        {
            CombatUnit blocker = _battle.AddCombatants(CreateShip("C", "OWNER"))[0];
            blocker.Position.Z = 5f;
            CreateCombat(_battle).Advance(1f);
            Assert.That(_battle.NextShotID, Is.Zero);
        }

        /// <summary>
        /// Verifies docked squadrons are neither targets nor firing participants.
        /// </summary>
        [Test]
        public void Advance_DockedTarget_DoesNotFire()
        {
            _defender.Flight.Phase = FighterFlightState.Docked;
            CreateCombat(_battle).Advance(1f);
            Assert.That(_battle.NextShotID, Is.Zero);
        }

        /// <summary>
        /// Verifies destroyed hulls stop shooting and cannot regain shields.
        /// </summary>
        [Test]
        public void Advance_DestroyedShooter_DoesNotFireOrRecharge()
        {
            CombatUnitData ship = _attacker.Data;
            _attacker.Condition.CurrentHullStrength = 0;
            ship.MaxShieldStrength = 20;
            ship.ShieldRechargeRate = 10;
            _attacker.Condition.CurrentShieldStrength = 0;
            CreateCombat(_battle).Advance(1f);
            Assert.That(_battle.NextShotID, Is.Zero);
            Assert.That(_attacker.Condition.CurrentShieldStrength, Is.Zero);
        }

        /// <summary>
        /// Verifies recharge is expressed per battle second and scaled by remaining hull condition.
        /// </summary>
        [Test]
        public void Advance_DamagedHull_ScalesShieldRecharge()
        {
            CombatUnitData ship = _attacker.Data;
            _attacker.Condition.CurrentHullStrength = 50;
            ship.MaxShieldStrength = 20;
            ship.ShieldRechargeRate = 10;
            _attacker.Condition.CurrentShieldStrength = 0;
            _defender.HasRetreated = true;
            CreateCombat(_battle).Advance(1f);
            Assert.That(_attacker.Condition.CurrentShieldStrength, Is.EqualTo(5));
        }

        /// <summary>
        /// Verifies shield recharge cannot exceed authored capacity.
        /// </summary>
        [Test]
        public void Advance_FullShields_DoesNotOvercharge()
        {
            CombatUnitData ship = _attacker.Data;
            ship.MaxShieldStrength = 20;
            ship.ShieldRechargeRate = 100;
            _attacker.Condition.CurrentShieldStrength = 19;
            _defender.HasRetreated = true;
            CreateCombat(_battle).Advance(1f);
            Assert.That(_attacker.Condition.CurrentShieldStrength, Is.EqualTo(20));
        }

        /// <summary>
        /// Verifies game state only mutates battle copies, not strategic source ships.
        /// </summary>
        [Test]
        public void Advance_LethalHit_ReportsDestructionOnce()
        {
            Mount().Weapon.Projectile.DeliveryMode = WeaponDeliveryMode.Hitscan;
            Mount().Weapon.Projectile.Damage = 1000f;
            int destroyed = 0;
            TacticalCombat combat = CreateCombat(_battle);
            combat.UnitDestroyed += _ => destroyed++;
            combat.Advance(2f);
            Assert.That(destroyed, Is.EqualTo(1));
            Assert.That(TacticalCombat.IsAlive(_defender), Is.False);
        }

        /// <summary>
        /// Verifies an explicit attack only affects the issuing faction's ships.
        /// </summary>
        [Test]
        public void Attack_EnemySelection_DoesNotChangeEnemyOrders()
        {
            CreateCombat(_battle).Attack(new[] { _defender }, "OWNER", _attacker);
            Assert.That(_defender.Weapons.AttackTargetInstanceID, Is.Null);
        }

        /// <summary>
        /// Verifies an explicit hostile target is stored by stable battle identity.
        /// </summary>
        [Test]
        public void Attack_FriendlySelection_StoresHostileTarget()
        {
            CreateCombat(_battle).Attack(new[] { _attacker }, "OWNER", _defender);
            Assert.That(_attacker.Weapons.AttackTargetInstanceID, Is.EqualTo(_defender.InstanceID));
        }

        /// <summary>
        /// Verifies malformed weapon data is rejected instead of receiving hidden default tuning.
        /// </summary>
        [Test]
        public void Constructor_ZeroProjectileSpeed_RejectsInvalidWeapon()
        {
            Mount().Weapon.Projectile.Speed = 0f;
            Assert.Throws<ArgumentException>(() => CreateCombat(_battle));
        }

        /// <summary>
        /// Verifies saved projectiles, random state, fractional time, and cooldowns resume identically.
        /// </summary>
        [Test]
        public void Advance_SaveReload_MatchesUninterruptedBattle()
        {
            Mount().Weapon.ShotsPerVolley = 2;
            Mount().Weapon.FireIntervalSeconds = 0.125f;
            Mount().Weapon.MinimumRechargeSeconds = 0.5f;
            Mount().Weapon.MaximumRechargeSeconds = 0.9f;
            Hardpoint second = CreateMount();
            second.Weapon.FireIntervalSeconds = 0.25f;
            TacticalCombat.GetGroups(_attacker)[0].GetHardpoints().Add(second);
            TacticalCombat combat = CreateCombat(_battle);
            combat.Advance(0.337f);
            ActiveBattle resumed = SerializationHelper.Deserialize<ActiveBattle>(
                SerializationHelper.Serialize(_battle)
            );
            combat.Advance(2f);
            CreateCombat(resumed, _battle).Advance(2f);
            Assert.That(
                SerializationHelper.Serialize(resumed),
                Is.EqualTo(SerializationHelper.Serialize(_battle))
            );
        }

        /// <summary>
        /// Verifies the fixed clock produces equivalent outcomes for different rendering frame partitions.
        /// </summary>
        [Test]
        public void Advance_DifferentFramePartitions_MatchesShotCountAndDamage()
        {
            ActiveBattle other = SerializationHelper.Deserialize<ActiveBattle>(
                SerializationHelper.Serialize(_battle)
            );
            CreateCombat(_battle).Advance(2f);
            TacticalCombat combat = CreateCombat(other, _battle);
            for (int index = 0; index < 128; index++)
                combat.Advance(1f / 64f);
            Assert.That(
                SerializationHelper.Serialize(other),
                Is.EqualTo(SerializationHelper.Serialize(_battle))
            );
        }

        /// <summary>
        /// Verifies a flying shot retains its emitted speed and damage if its source weapon changes.
        /// </summary>
        [Test]
        public void Advance_SourceWeaponChanges_PreservesEmittedShotData()
        {
            TacticalCombat combat = CreateCombat(_battle);
            combat.Advance(1f / 64f);
            Mount().Weapon.Projectile.Speed = 10000f;
            Mount().Weapon.Projectile.Damage = 1000f;
            Mount().Health = 0;
            combat.Advance(1f);
            Assert.That(_defender.Condition.CurrentHullStrength, Is.EqualTo(90));
        }

        [Test]
        public void Advance_Hitscan_ResolvesDamageWithoutSavingPresentationEffects()
        {
            Mount().Weapon.Projectile.DeliveryMode = WeaponDeliveryMode.Hitscan;
            Mount().Weapon.Projectile.EffectDurationSeconds = 0.2f;
            BattleProjectile emitted = null;
            WeaponData firingWeapon = null;
            TacticalCombat combat = CreateCombat(_battle);
            combat.ShotFired += (shot, weapon) =>
            {
                emitted = shot;
                firingWeapon = weapon;
            };

            combat.Advance(1f / 64f);

            Assert.AreEqual(90, _defender.Condition.CurrentHullStrength);
            Assert.IsEmpty(_battle.Projectiles);
            Assert.IsNotNull(emitted);
            Assert.AreSame(Mount().Weapon, firingWeapon);
            Assert.AreNotSame(firingWeapon.Projectile, emitted.Projectile);
        }

        /// <summary>
        /// Returns the fixture's independently copied weapon mount.
        /// </summary>
        /// <returns>The fixture's primary weapon.</returns>
        private Hardpoint Mount() => TacticalCombat.GetGroups(_attacker)[0].GetHardpoints()[0];

        /// <summary>
        /// Resolves test-owned local mounts without any Unity scene dependency.
        /// </summary>
        /// <param name="battle">The tested battle.</param>
        /// <param name="geometrySource">The measured geometry to restore after deserialization.</param>
        /// <returns>The authoritative combat simulation.</returns>
        private static TacticalCombat CreateCombat(
            ActiveBattle battle,
            ActiveBattle geometrySource = null
        )
        {
            if (geometrySource != null)
            {
                Dictionary<string, ModelDimensions> geometry = geometrySource
                    .GetParticipants()
                    .SelectMany(participant => participant.GetCombatants())
                    .ToDictionary(unit => unit.InstanceID, unit => unit.Data.ModelSize);
                foreach (
                    CombatUnit unit in battle
                        .GetParticipants()
                        .SelectMany(participant => participant.GetCombatants())
                )
                    unit.Data.ModelSize = geometry[unit.InstanceID].CreateCopy();
            }
            Dictionary<Hardpoint, TacticalCombat.Mount> mounts = new();
            foreach (BattleParticipant participant in battle.GetParticipants())
            foreach (CombatUnit unit in participant.GetCombatants())
            foreach (HardpointGroup group in TacticalCombat.GetGroups(unit))
            foreach (Hardpoint point in group.GetHardpoints())
                mounts.Add(
                    point,
                    new TacticalCombat.Mount(
                        new Vector3(point.Position.X, point.Position.Y, point.Position.Z),
                        Vector3.UnitZ,
                        Vector3.UnitY
                    )
                );
            return new TacticalCombat(battle, mounts);
        }

        /// <summary>
        /// Creates an explicitly authored bank mount with simple measurable projectile timing.
        /// </summary>
        /// <returns>The authored mount.</returns>
        private static Hardpoint CreateMount() =>
            new Hardpoint
            {
                Health = 100,
                FiringArcDegrees = 30f,
                Weapon = new WeaponData
                {
                    WeaponType = HardpointWeaponType.LaserCannon,
                    Range = 20f,
                    FireIntervalSeconds = 1f,
                    Projectile = new ProjectileData
                    {
                        DeliveryMode = WeaponDeliveryMode.Projectile,
                        Damage = 10f,
                        Speed = 10f,
                        EffectDurationSeconds = 0.2f,
                    },
                },
            };

        /// <summary>
        /// Creates one conventional hull for the fixture's battle snapshots.
        /// </summary>
        /// <param name="id">The source ship identity.</param>
        /// <param name="owner">The controlling faction.</param>
        /// <returns>The source capital ship.</returns>
        private static CapitalShip CreateShip(string id, string owner) =>
            new CapitalShip
            {
                InstanceID = id,
                OwnerInstanceID = owner,
                MaxHullStrength = 100,
                CurrentHullStrength = 100,
                ModelSize = new ModelDimensions
                {
                    Width = 2f,
                    Height = 2f,
                    Depth = 2f,
                },
            };
    }
}

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
    public sealed class TacticalBattleSessionTests
    {
        [Test]
        public void Advance_MovingRoster_AdvancesMovementAndGunTimerTogether()
        {
            ActiveBattle battle = CreateBattle();
            SpaceCombatUnit unit = battle.GetParticipants()[0].GetCombatants()[0];
            TacticalBattleSession session = CreateSession(battle);
            unit.MoveOrder.Destination = new BattleVector3 { Z = 100f };
            unit.Weapons.Banks[0].Mounts[0].Cooldown = 1f;

            session.Advance(0.5f);

            Assert.That(unit.Position.Z, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(unit.Weapons.Banks[0].Mounts[0].Cooldown, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void Advance_SaveReload_MatchesUninterruptedMovementAndCombat()
        {
            ActiveBattle battle = CreateBattle();
            SpaceCombatUnit unit = battle.GetParticipants()[0].GetCombatants()[0];
            unit.MoveOrder.Destination = new BattleVector3 { Z = 100f };
            TacticalBattleSession uninterrupted = CreateSession(battle);
            uninterrupted.Advance(0.337f);
            ActiveBattle restored = SerializationHelper.Deserialize<ActiveBattle>(
                SerializationHelper.Serialize(battle)
            );
            Dictionary<string, ModelDimensions> geometry = battle
                .GetParticipants()
                .SelectMany(participant => participant.GetCombatants())
                .ToDictionary(member => member.InstanceID, member => member.ModelSize);
            foreach (
                SpaceCombatUnit member in restored
                    .GetParticipants()
                    .SelectMany(participant => participant.GetCombatants())
            )
                member.ModelSize = geometry[member.InstanceID].CreateCopy();
            TacticalBattleSession resumed = CreateSession(restored);

            uninterrupted.Advance(2f);
            resumed.Advance(2f);

            Assert.That(
                SerializationHelper.Serialize(restored),
                Is.EqualTo(SerializationHelper.Serialize(battle))
            );
        }

        [Test]
        public void Clear_LoadedBattle_ReleasesServicesAndRetainsSelectedSpeed()
        {
            TacticalBattleSession session = CreateSession(CreateBattle());
            session.Clock.SetSpeed(TacticalBattleSpeed.Double);

            session.Clear();

            Assert.IsNull(session.Battle);
            Assert.IsNull(session.Combat);
            Assert.IsNull(session.Carriers);
            Assert.AreEqual(TacticalBattleSpeed.Double, session.Clock.Speed);
        }

        /// <summary>
        /// Creates opposing hulls with independent authored weapon and geometry inputs.
        /// </summary>
        /// <returns>The controlled battle state.</returns>
        private static ActiveBattle CreateBattle()
        {
            ActiveBattle battle = new ActiveBattle
            {
                Kind = BattleKind.Space,
                FlightRandomSeed = 11,
                WeaponRandomSeed = 42,
            };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "SOURCE",
                OwnerInstanceID = "OWNER",
                MaxHullStrength = 100,
                CurrentHullStrength = 100,
                SublightSpeed = 4,
                ModelSize = new ModelDimensions
                {
                    Width = 2f,
                    Height = 2f,
                    Depth = 2f,
                },
            };
            HardpointGroup group = new HardpointGroup { ID = "primary" };
            group
                .GetHardpoints()
                .Add(
                    new Hardpoint
                    {
                        Health = 100,
                        FiringArcDegrees = 90f,
                        Weapon = new WeaponData
                        {
                            WeaponType = HardpointWeaponType.LaserCannon,
                            Range = 30f,
                            FireIntervalSeconds = 0.25f,
                            Projectile = new ProjectileData { Damage = 2f, Speed = 10f },
                        },
                    }
                );
            ship.GetHardpointGroups().Add(group);
            battle.AddCombatants(ship);
            ship.InstanceID = "TARGET";
            ship.OwnerInstanceID = "ENEMY";
            ship.GetHardpointGroups().Clear();
            battle.AddCombatants(ship)[0].Position.Z = 12f;
            return battle;
        }

        /// <summary>
        /// Rebuilds battle execution from prepared model-local mounts.
        /// </summary>
        /// <param name="battle">The battle state to execute.</param>
        /// <returns>The initialized battle session.</returns>
        private static TacticalBattleSession CreateSession(ActiveBattle battle)
        {
            Dictionary<Hardpoint, TacticalCombat.Mount> mounts = new();
            foreach (
                SpaceCombatUnit member in battle
                    .GetParticipants()
                    .SelectMany(participant => participant.GetCombatants())
            )
            foreach (HardpointGroup group in member.GetHardpointGroups())
            foreach (Hardpoint point in group.GetHardpoints())
                mounts.Add(
                    point,
                    new TacticalCombat.Mount(Vector3.Zero, Vector3.UnitZ, Vector3.UnitY)
                );
            TacticalBattleSession session = new TacticalBattleSession();
            session.Initialize(battle, mounts, new Dictionary<SpaceCombatUnit, Vector3[][]>());
            return session;
        }
    }
}

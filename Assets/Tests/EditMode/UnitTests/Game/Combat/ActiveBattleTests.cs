using System;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;
using Rebellion.Game.ShipComponents;
using Rebellion.Game.Units;
using Rebellion.Util.Serialization;

namespace Rebellion.Tests.Game.Combat
{
    [TestFixture]
    public sealed class ActiveBattleTests
    {
        [Test]
        public void AddCombatant_SingleUnit_CreatesIndependentCopyForOwningFaction()
        {
            CapitalShip source = new CapitalShip
            {
                InstanceID = "ship",
                OwnerInstanceID = "faction",
                CurrentHullStrength = 75,
            };
            ActiveBattle battle = CreateBattle();

            CombatUnit combatant = battle.AddCombatant(source);

            Assert.AreEqual("faction", combatant.Unit.OwnerInstanceID);
            Assert.AreSame(combatant, battle.Combatants.Single());
            Assert.AreNotSame(source, combatant.Unit);
            ((CapitalShip)combatant.Unit).CurrentHullStrength = 10;
            Assert.AreEqual(75, source.CurrentHullStrength);
        }

        [Test]
        public void AddCombatant_DifferentOwners_KeepCombatantsInEntryOrder()
        {
            ActiveBattle battle = CreateBattle();

            battle.AddCombatant(new CapitalShip { OwnerInstanceID = "first" });
            battle.AddCombatant(new CapitalShip { OwnerInstanceID = "second" });
            battle.AddCombatant(new CapitalShip { OwnerInstanceID = "first" });

            CollectionAssert.AreEqual(
                new[] { "first", "second", "first" },
                battle.Combatants.Select(combatant => combatant.Unit.OwnerInstanceID)
            );
        }

        [Test]
        public void AddCombatant_DestroyedSquadron_DoesNotAddCombatant()
        {
            ActiveBattle battle = CreateBattle();

            CombatUnit combatant = battle.AddCombatant(
                new Starfighter { OwnerInstanceID = "faction", CurrentSquadronSize = 0 }
            );

            Assert.IsNull(combatant);
            Assert.IsEmpty(battle.Combatants);
        }

        [Test]
        public void AddCombatant_UnsupportedUnit_ThrowsBeforeAddingCombatant()
        {
            ActiveBattle battle = CreateBattle();

            Assert.Throws<ArgumentException>(() =>
                battle.AddCombatant(new Regiment { OwnerInstanceID = "faction" })
            );
            Assert.IsEmpty(battle.Combatants);
        }

        [Test]
        public void AddCombatant_GroundBattle_RejectsSpaceUnit()
        {
            ActiveBattle battle = new ActiveBattle { Kind = BattleKind.Ground };

            Assert.Throws<InvalidOperationException>(() =>
                battle.AddCombatant(new CapitalShip { OwnerInstanceID = "faction" })
            );
        }

        [Test]
        public void Serialize_ActiveBattle_RoundTripsBattleCopyAndSourceIdentity()
        {
            ActiveBattle battle = CreateBattle();
            battle.PlanetInstanceID = "planet";
            battle.AddCombatant(
                new CapitalShip
                {
                    InstanceID = "ship",
                    OwnerInstanceID = "faction",
                    CurrentHullStrength = 75,
                }
            );

            string xml = SerializationHelper.Serialize(battle);
            Assert.That(xml, Does.Contain("<Combatants>").And.Not.Contain("<Participants>"));

            ActiveBattle restored = SerializationHelper.Deserialize<ActiveBattle>(xml);
            CombatUnit restoredCombatant = restored.GetCombatant("ship");

            Assert.IsNotNull(restoredCombatant);
            Assert.AreEqual("planet", restored.PlanetInstanceID);
            Assert.AreEqual(75, ((CapitalShip)restoredCombatant.Unit).CurrentHullStrength);
        }

        [Test]
        public void Deserialize_ActiveBattleWithParticipants_RestoresFlatCombatants()
        {
            const string xml =
                @"<ActiveBattle>
                  <Participants>
                    <BattleParticipant>
                      <Combatants>
                        <CombatUnit>
                          <CapitalShip>
                            <OwnerInstanceID>first</OwnerInstanceID>
                          </CapitalShip>
                          <SourceUnitInstanceID>ship</SourceUnitInstanceID>
                        </CombatUnit>
                      </Combatants>
                      <FactionInstanceID>first</FactionInstanceID>
                    </BattleParticipant>
                  </Participants>
                  <Kind>Space</Kind>
                </ActiveBattle>";

            ActiveBattle restored = SerializationHelper.Deserialize<ActiveBattle>(xml);

            Assert.AreEqual("first", restored.GetCombatant("ship").Unit.OwnerInstanceID);
        }

        [Test]
        public void GameRootSerialization_ActiveBattle_RoundTripsLocationAndSides()
        {
            GameRoot game = new GameRoot
            {
                ActiveBattle = new ActiveBattle
                {
                    Kind = BattleKind.Space,
                    PlanetInstanceID = "planet",
                    AttackerOwnerInstanceID = "attacker",
                    DefenderOwnerInstanceID = "defender",
                },
            };

            GameRoot restored = SerializationHelper.Deserialize<GameRoot>(
                SerializationHelper.Serialize(game)
            );

            Assert.IsNotNull(restored.ActiveBattle);
            Assert.AreEqual("planet", restored.ActiveBattle.PlanetInstanceID);
            Assert.AreEqual(BattleKind.Space, restored.ActiveBattle.Kind);
            Assert.AreEqual("attacker", restored.ActiveBattle.AttackerOwnerInstanceID);
            Assert.AreEqual("defender", restored.ActiveBattle.DefenderOwnerInstanceID);
        }

        [Test]
        public void GameRootSerialization_ActiveBattle_RoundTripsLiveDamageAndPosition()
        {
            GameRoot game = new GameRoot { ActiveBattle = CreateBattle() };
            CombatUnit unit = game.ActiveBattle.AddCombatant(
                new CapitalShip
                {
                    InstanceID = "ship",
                    OwnerInstanceID = "owner",
                    MaxHullStrength = 1000,
                    CurrentHullStrength = 1000,
                }
            );
            unit.InitializeTacticalState(CombatSide.Attacker, 1);
            unit.Position = new Vector3(12, 0, -48);
            unit.Systems.ApplyHullLoss(99.5f);

            string xml = SerializationHelper.Serialize(game);
            Assert.That(xml, Does.Contain("<Hull>900.5</Hull>"));
            CombatUnit restored = SerializationHelper
                .Deserialize<GameRoot>(xml)
                .ActiveBattle.GetCombatant("ship");

            Assert.AreEqual(unit.Position, restored.Position);
            Assert.AreEqual(900.5f, restored.Systems.Hull);
            Assert.AreEqual("ship", restored.InstanceID);
            Assert.AreEqual(
                unit.GetBatteryCharge(TacticalWeaponArc.Forward),
                restored.GetBatteryCharge(TacticalWeaponArc.Forward)
            );
        }

        [Test]
        public void GameRootSerialization_AuthoredMounts_SharesArcChargeAcrossReload()
        {
            GameRoot game = new() { ActiveBattle = CreateBattle() };
            CapitalShip source = new()
            {
                InstanceID = "ship",
                OwnerInstanceID = "owner",
                CurrentHullStrength = 100,
                MaxHullStrength = 100,
                Engine = new Engine { SublightSpeed = 10, Maneuverability = 3 },
            };
            HardpointGroup group = new() { Arc = TacticalWeaponArc.Starboard };
            foreach (int damage in new[] { 7, 11 })
                group
                    .GetHardpoints()
                    .Add(
                        new Hardpoint
                        {
                            FiringArcDegrees = 25,
                            Weapon = new WeaponData
                            {
                                WeaponType = HardpointWeaponType.LaserCannon,
                                Damage = damage,
                                Range = 200,
                            },
                        }
                    );
            source.HardpointGroups.Add(group);
            CombatUnit unit = game.ActiveBattle.AddCombatant(source);
            unit.InitializeTacticalState(CombatSide.Attacker, 1);
            unit.DischargeBattery(TacticalWeaponArc.Starboard, TacticalWeaponType.LaserCannon);
            unit.RechargeBatteries(1, 6);

            string xml = SerializationHelper.Serialize(game);
            Assert.That(xml, Does.Contain("<HardpointGroups>").And.Not.Contain("<PrimaryWeapons>"));
            CombatUnit restored = SerializationHelper
                .Deserialize<GameRoot>(xml)
                .ActiveBattle.GetCombatant("ship");

            Assert.AreEqual(
                0,
                restored.GetWeaponStrength(
                    TacticalWeaponArc.Forward,
                    TacticalWeaponType.LaserCannon
                )
            );
            Assert.AreEqual(
                18,
                restored.GetWeaponStrength(
                    TacticalWeaponArc.Starboard,
                    TacticalWeaponType.LaserCannon
                )
            );
            Assert.AreEqual(
                200,
                restored.GetWeaponRange(TacticalWeaponArc.Starboard, TacticalWeaponType.LaserCannon)
            );
            Assert.AreEqual(6, restored.GetBatteryCharge(TacticalWeaponArc.Starboard));
            Assert.AreEqual(10, ((CapitalShip)restored.Unit).SublightSpeed);
            restored.RechargeBatteries(1, 12);
            Assert.IsTrue(restored.IsBatteryReady(TacticalWeaponArc.Starboard));
            Assert.AreEqual(25, restored.Batteries[1].GetHardpoints()[0].FiringArcDegrees);
            Assert.AreNotSame(group, unit.Batteries[1]);
        }

        [Test]
        public void Deserialize_ActiveBattleWithLegacyWeapons_RestoresQueuedCharge()
        {
            const string xml =
                @"<CombatUnit>
                  <CapitalShip />
                  <Weapons>
                    <Charge><Single>1.5</Single><Single>0</Single><Single>0</Single><Single>0</Single></Charge>
                    <Ready><UInt32>0</UInt32><UInt32>0</UInt32><UInt32>0</UInt32><UInt32>0</UInt32></Ready>
                    <RechargeOrder><Int32>0</Int32><Int32>-1</Int32><Int32>-1</Int32><Int32>-1</Int32></RechargeOrder>
                    <RechargeCount>1</RechargeCount>
                  </Weapons>
                </CombatUnit>";

            CombatUnit restored = SerializationHelper.Deserialize<CombatUnit>(xml);
            Assert.AreEqual(1.5f, restored.GetBatteryCharge(TacticalWeaponArc.Forward));
            Assert.IsFalse(restored.IsBatteryReady(TacticalWeaponArc.Forward));
        }

        /// <summary>
        /// Creates an empty space battle for a test.
        /// </summary>
        /// <returns>The new battle.</returns>
        private static ActiveBattle CreateBattle() => new ActiveBattle { Kind = BattleKind.Space };
    }
}

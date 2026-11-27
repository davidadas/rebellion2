using System;
using System.Linq;
using System.Numerics;
using System.Xml.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Combat;
using Rebellion.Game.Results;
using Rebellion.Game.ShipComponents;
using Rebellion.Game.Units;
using Rebellion.Tactical;
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
        public void Deserialize_ActiveBattleWithLegacyGroupIndexes_PreservesUnitWithoutIndexes()
        {
            const string xml =
                @"<ActiveBattle>
                  <Combatants>
                    <CombatUnit>
                      <CapitalShip><OwnerInstanceID>first</OwnerInstanceID></CapitalShip>
                      <SourceUnitInstanceID>ship</SourceUnitInstanceID>
                      <GroupID>2</GroupID>
                      <RequestedGroupID>3</RequestedGroupID>
                    </CombatUnit>
                  </Combatants>
                  <Kind>Space</Kind>
                </ActiveBattle>";

            ActiveBattle battle = SerializationHelper.Deserialize<ActiveBattle>(
                xml,
                new GameSerializerSettings { IgnoreUnknownElements = true }
            );

            Assert.AreEqual("first", battle.GetCombatant("ship").Unit.OwnerInstanceID);
            Assert.That(
                SerializationHelper.Serialize(battle),
                Does.Not.Contain("<GroupID>").And.Not.Contain("<RequestedGroupID>")
            );
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
                    AttackingFactionIDs = { "attacker", "other-attacker" },
                    DefendingFactionIDs = { "defender", "other-defender" },
                },
            };
            game.ActiveBattle.AddCombatant(
                new CapitalShip { InstanceID = "ship", OwnerInstanceID = "attacker" }
            );

            string xml = SerializationHelper.Serialize(game);
            XElement battle = XDocument.Parse(xml).Root.Element("ActiveBattle");
            CollectionAssert.AreEqual(
                new[] { "attacker", "other-attacker" },
                battle.Element("AttackingFactionIDs").Elements("FactionID").Select(id => id.Value)
            );
            CollectionAssert.AreEqual(
                new[] { "defender", "other-defender" },
                battle.Element("DefendingFactionIDs").Elements("FactionID").Select(id => id.Value)
            );
            Assert.IsNull(battle.Element("AttackerOwnerInstanceID"));
            Assert.IsNull(battle.Element("DefenderOwnerInstanceID"));

            ActiveBattle restored = SerializationHelper.Deserialize<GameRoot>(xml).ActiveBattle;
            Assert.AreEqual("planet", restored.PlanetInstanceID);
            Assert.AreEqual(BattleKind.Space, restored.Kind);
            CollectionAssert.AreEqual(
                game.ActiveBattle.AttackingFactionIDs,
                restored.AttackingFactionIDs
            );
            CollectionAssert.AreEqual(
                game.ActiveBattle.DefendingFactionIDs,
                restored.DefendingFactionIDs
            );
            Assert.AreEqual("attacker", restored.Combatants.Single().Unit.OwnerInstanceID);
        }

        [Test]
        public void Deserialize_LegacySingleFactionPerSide_MigratesToFactionLists()
        {
            const string xml =
                @"<ActiveBattle>
                  <AttackerOwnerInstanceID>attacker</AttackerOwnerInstanceID>
                  <DefenderOwnerInstanceID>defender</DefenderOwnerInstanceID>
                  <Kind>Space</Kind>
                </ActiveBattle>";

            ActiveBattle restored = SerializationHelper.Deserialize<ActiveBattle>(xml);

            CollectionAssert.AreEqual(new[] { "attacker" }, restored.AttackingFactionIDs);
            CollectionAssert.AreEqual(new[] { "defender" }, restored.DefendingFactionIDs);
            Assert.IsEmpty(restored.Combatants);
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
            TacticalCombatDeployment.InitializeUnit(unit, null, CombatSide.Attacker, 1);
            unit.Position = new Vector3(12, 0, -48);
            unit.ApplyHullLoss(99.5f);
            TacticalCombatDamage.ApplyComponentEvent(
                unit,
                TacticalShipSystem.Engines,
                isRepair: false
            );

            string xml = SerializationHelper.Serialize(game);
            Assert.That(
                xml,
                Does.Contain("<Hull>900.5</Hull>").And.Not.Contain("<ComponentDamage>")
            );
            CombatUnit restored = SerializationHelper
                .Deserialize<GameRoot>(xml)
                .ActiveBattle.GetCombatant("ship");

            Assert.AreEqual(unit.Position, restored.Position);
            Assert.AreEqual(900.5f, restored.Hull);
            Assert.AreEqual(1u, restored.GetDamage(TacticalShipSystem.Engines));
            Assert.AreEqual(1, ((CapitalShip)restored.Unit).SystemDamage.Engines);
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
            Assert.AreEqual(
                18,
                source.GetPrimaryWeaponStrength(
                    PrimaryWeaponType.LaserCannon,
                    PrimaryWeaponArc.Starboard
                )
            );
            Assert.AreEqual(200, source.GetPrimaryWeaponRange(PrimaryWeaponType.LaserCannon));
            CombatUnit unit = game.ActiveBattle.AddCombatant(source);
            TacticalCombatDeployment.InitializeUnit(unit, null, CombatSide.Attacker, 1);
            unit.DischargeBattery(TacticalWeaponArc.Starboard, TacticalWeaponType.LaserCannon);
            TacticalCombatFireControl.RechargeBatteries(unit, 1, 6);

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
            TacticalCombatFireControl.RechargeBatteries(restored, 1, 12);
            Assert.IsTrue(restored.IsBatteryReady(TacticalWeaponArc.Starboard));
            Assert.AreEqual(25, restored.Batteries[1].GetHardpoints()[0].FiringArcDegrees);
            Assert.AreNotSame(group, unit.Batteries[1]);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Deserialize_LegacyCapitalShipDamage_UsesTheLiveBattleDamage(
            bool damageBeforeShip
        )
        {
            const string ship =
                "<CapitalShip><SystemDamage><Engines>1</Engines></SystemDamage></CapitalShip>";
            const string damage = "<ComponentDamage><Engines>3</Engines></ComponentDamage>";
            string contents = damageBeforeShip ? damage + ship : ship + damage;

            CombatUnit restored = SerializationHelper.Deserialize<CombatUnit>(
                "<CombatUnit>" + contents + "</CombatUnit>"
            );

            Assert.AreEqual(3u, restored.GetDamage(TacticalShipSystem.Engines));
            Assert.AreEqual(3, ((CapitalShip)restored.Unit).SystemDamage.Engines);
            Assert.That(
                SerializationHelper.Serialize(restored),
                Does.Not.Contain("<ComponentDamage>")
            );
        }

        [Test]
        public void Deserialize_ActiveBattleWithLegacySystemsAndWeapons_RestoresLiveState()
        {
            const string xml =
                @"<CombatUnit>
                  <CapitalShip />
                  <Systems>
                    <ComponentDamage><UInt32>1</UInt32><UInt32>0</UInt32><UInt32>0</UInt32><UInt32>2</UInt32><UInt32>1</UInt32></ComponentDamage>
                    <HyperdriveCount>2</HyperdriveCount>
                    <DamageEventCount>4</DamageEventCount>
                    <RepairMilliseconds>98.25</RepairMilliseconds>
                    <ShieldLimit>50</ShieldLimit>
                    <MaximumHull>100</MaximumHull>
                    <Hull>35.5</Hull>
                    <Shields>12.25</Shields>
                  </Systems>
                  <Weapons>
                    <Charge><Single>1.5</Single><Single>0</Single><Single>0</Single><Single>0</Single></Charge>
                    <Ready><UInt32>0</UInt32><UInt32>0</UInt32><UInt32>0</UInt32><UInt32>0</UInt32></Ready>
                    <RechargeOrder><Int32>0</Int32><Int32>-1</Int32><Int32>-1</Int32><Int32>-1</Int32></RechargeOrder>
                    <RechargeCount>1</RechargeCount>
                  </Weapons>
                </CombatUnit>";

            CombatUnit restored = SerializationHelper.Deserialize<CombatUnit>(xml);
            Assert.AreEqual(35.5f, restored.Hull);
            Assert.AreEqual(12.25f, restored.Shields);
            Assert.AreEqual(2u, restored.GetDamage(TacticalShipSystem.Engines));
            Assert.AreEqual(98.25f, restored.RepairMilliseconds);
            Assert.IsTrue(restored.HasHyperdrive);
            Assert.AreEqual(1.5f, restored.GetBatteryCharge(TacticalWeaponArc.Forward));
            Assert.IsFalse(restored.IsBatteryReady(TacticalWeaponArc.Forward));
        }

        [Test]
        public void Deserialize_LegacyDriveDamageBeforeShip_DisablesTheFirstDrive()
        {
            const string xml =
                "<CombatUnit><Systems>"
                + "<ComponentDamage><UInt32>0</UInt32><UInt32>0</UInt32><UInt32>0</UInt32><UInt32>0</UInt32><UInt32>1</UInt32></ComponentDamage>"
                + "</Systems><CapitalShip><Hyperdrive>4</Hyperdrive><BackupHyperdrive>6</BackupHyperdrive></CapitalShip></CombatUnit>";

            CombatUnit restored = SerializationHelper.Deserialize<CombatUnit>(xml);

            Assert.AreEqual(6, ((CapitalShip)restored.Unit).GetOperationalHyperdrive());
        }

        /// <summary>
        /// Creates an empty space battle for a test.
        /// </summary>
        /// <returns>The new battle.</returns>
        private static ActiveBattle CreateBattle() => new ActiveBattle { Kind = BattleKind.Space };
    }
}

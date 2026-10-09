using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

namespace Rebellion.Tests.Game.Combat
{
    [TestFixture]
    public sealed class ActiveBattleTests
    {
        [Test]
        public void AddCombatants_CapitalShip_AddsOneIndependentCopyToOwnerParticipant()
        {
            CapitalShip source = new CapitalShip
            {
                InstanceID = "CAPITAL_SHIP_1",
                OwnerInstanceID = "FACTION_1",
                CurrentHullStrength = 75,
            };
            ActiveBattle battle = CreateBattle();

            IReadOnlyList<CombatUnit> addedCombatants = battle.AddCombatants(source);
            BattleParticipant participant = battle.GetParticipants().Single();

            Assert.AreEqual(1, addedCombatants.Count);
            Assert.AreEqual("FACTION_1", participant.FactionInstanceID);
            Assert.AreSame(addedCombatants[0], participant.GetCombatants()[0]);
            Assert.AreNotSame(source, addedCombatants[0].GetUnit());
        }

        [Test]
        public void AddCombatants_StarfighterSquadron_AddsOneCopyPerSurvivingFighter()
        {
            Starfighter source = new Starfighter
            {
                InstanceID = "STARFIGHTER_SQUADRON_1",
                OwnerInstanceID = "FACTION_1",
                MaxSquadronSize = 12,
                CurrentSquadronSize = 3,
            };
            ActiveBattle battle = CreateBattle();

            IReadOnlyList<CombatUnit> addedCombatants = battle.AddCombatants(source);
            BattleParticipant participant = battle.GetParticipants().Single();

            Assert.AreEqual(3, addedCombatants.Count);
            Assert.AreEqual(3, participant.GetCombatants().Count);
            foreach (CombatUnit combatUnit in addedCombatants)
            {
                Assert.AreEqual(source.InstanceID, combatUnit.SourceUnitInstanceID);
                Starfighter fighter = combatUnit.GetUnit() as Starfighter;
                Assert.IsNotNull(fighter);
                Assert.AreEqual(1, fighter.CurrentSquadronSize);
            }
        }

        [Test]
        public void AddCombatants_DifferentFactionOwners_CreatesSeparateParticipants()
        {
            CapitalShip firstSource = new CapitalShip
            {
                InstanceID = "CAPITAL_SHIP_1",
                OwnerInstanceID = "FACTION_1",
            };
            CapitalShip secondSource = new CapitalShip
            {
                InstanceID = "CAPITAL_SHIP_2",
                OwnerInstanceID = "FACTION_2",
            };
            ActiveBattle battle = CreateBattle();

            battle.AddCombatants(firstSource);
            battle.AddCombatants(secondSource);

            Assert.AreEqual(2, battle.GetParticipants().Count);
            Assert.AreEqual(
                1,
                battle
                    .GetParticipants()
                    .Single(participant => participant.FactionInstanceID == "FACTION_1")
                    .GetCombatants()
                    .Count
            );
            Assert.AreEqual(
                1,
                battle
                    .GetParticipants()
                    .Single(participant => participant.FactionInstanceID == "FACTION_2")
                    .GetCombatants()
                    .Count
            );
        }

        [Test]
        public void AddCombatants_GroundBattle_ThrowsBeforeAddingParticipant()
        {
            CapitalShip source = new CapitalShip
            {
                InstanceID = "CAPITAL_SHIP_1",
                OwnerInstanceID = "FACTION_1",
            };
            ActiveBattle battle = CreateBattle(BattleKind.Ground);

            Assert.Throws<InvalidOperationException>(() => battle.AddCombatants(source));

            Assert.IsEmpty(battle.GetParticipants());
        }

        [Test]
        public void AddCombatants_UnsupportedUnit_ThrowsBeforeAddingParticipant()
        {
            Regiment source = new Regiment
            {
                InstanceID = "REGIMENT_1",
                OwnerInstanceID = "FACTION_1",
            };
            ActiveBattle battle = CreateBattle();

            Assert.Throws<ArgumentException>(() => battle.AddCombatants(source));

            Assert.IsEmpty(battle.GetParticipants());
        }

        [Test]
        public void Serialize_CapitalShipBattleCopy_RoundTripsSourceAndCopy()
        {
            CapitalShip source = new CapitalShip
            {
                InstanceID = "CAPITAL_SHIP_1",
                OwnerInstanceID = "FACTION_1",
                CurrentHullStrength = 75,
                ModelPath = "Pack/Units/TestCapitalShip/Models/model",
                ModelSize = new ModelDimensions
                {
                    Width = 100f,
                    Height = 40f,
                    Depth = 250f,
                },
            };
            ActiveBattle battle = CreateBattle();
            battle.PlanetInstanceID = "PLANET_1";
            battle
                .GetParticipants()
                .Add(
                    new BattleParticipant
                    {
                        FactionInstanceID = "FACTION_2",
                        BattleMapSlotID = "defender",
                    }
                );
            CombatUnit combatUnit = battle.AddCombatants(source)[0];
            combatUnit.Position = new BattleVector3
            {
                X = 10f,
                Y = 20f,
                Z = 30f,
            };
            combatUnit.Forward = new BattleVector3
            {
                X = 0f,
                Y = 1f,
                Z = 0f,
            };

            string xml = SerializationHelper.Serialize(battle);
            ActiveBattle restored = SerializationHelper.Deserialize<ActiveBattle>(xml);
            CombatUnit restoredCombatUnit = restored
                .GetParticipants()
                .Single(participant => participant.FactionInstanceID == "FACTION_1")
                .GetCombatants()[0];
            CapitalShip restoredShip = restoredCombatUnit.GetUnit() as CapitalShip;

            Assert.AreEqual(source.InstanceID, restoredCombatUnit.SourceUnitInstanceID);
            Assert.IsNotNull(restoredShip);
            Assert.AreEqual(75, restoredShip.CurrentHullStrength);
            Assert.AreEqual(10f, restoredCombatUnit.Position.X);
            Assert.AreEqual(20f, restoredCombatUnit.Position.Y);
            Assert.AreEqual(30f, restoredCombatUnit.Position.Z);
            Assert.AreEqual(0f, restoredCombatUnit.Forward.X);
            Assert.AreEqual(1f, restoredCombatUnit.Forward.Y);
            Assert.AreEqual(0f, restoredCombatUnit.Forward.Z);
            Assert.AreEqual(source.ModelPath, restoredShip.ModelPath);
            Assert.AreEqual(100f, restoredShip.ModelSize.Width);
            Assert.AreEqual(40f, restoredShip.ModelSize.Height);
            Assert.AreEqual(250f, restoredShip.ModelSize.Depth);
            Assert.AreEqual(2, restored.GetParticipants().Count);
            Assert.AreEqual(
                "defender",
                restored
                    .GetParticipants()
                    .Single(participant => participant.FactionInstanceID == "FACTION_2")
                    .BattleMapSlotID
            );
            Assert.AreEqual("SPACE_MAP", restored.BattleMapInstanceID);
            StringAssert.DoesNotContain("DeploymentRegions", xml);
        }

        /// <summary>
        /// Creates a battle with its map identity and battle kind initialized.
        /// </summary>
        /// <param name="kind">The battle environment kind.</param>
        /// <returns>The initialized battle.</returns>
        private static ActiveBattle CreateBattle(BattleKind kind = BattleKind.Space)
        {
            return new ActiveBattle
            {
                BattleMapInstanceID = kind == BattleKind.Space ? "SPACE_MAP" : "GROUND_MAP",
                Kind = kind,
            };
        }
    }
}

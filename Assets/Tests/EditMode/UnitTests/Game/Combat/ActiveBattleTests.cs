using System.Collections.Generic;
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
        public void AddCombatants_CapitalShip_AddsOneIndependentCopyUnderOwner()
        {
            CapitalShip source = new CapitalShip
            {
                InstanceID = "CAPITAL_SHIP_1",
                OwnerInstanceID = "FACTION_1",
                CurrentHullStrength = 75,
            };
            ActiveBattle battle = new ActiveBattle();

            IReadOnlyList<CombatUnit> addedCombatants = battle.AddCombatants(source);

            Assert.AreEqual(1, addedCombatants.Count);
            Assert.AreSame(addedCombatants[0], battle.GetCombatants()["FACTION_1"][0]);
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
            ActiveBattle battle = new ActiveBattle();

            IReadOnlyList<CombatUnit> addedCombatants = battle.AddCombatants(source);

            Assert.AreEqual(3, addedCombatants.Count);
            Assert.AreEqual(3, battle.GetCombatants()["FACTION_1"].Count);
            foreach (CombatUnit combatUnit in addedCombatants)
            {
                Assert.AreEqual(source.InstanceID, combatUnit.SourceUnitInstanceID);
                Starfighter fighter = combatUnit.GetUnit() as Starfighter;
                Assert.IsNotNull(fighter);
                Assert.AreEqual(1, fighter.CurrentSquadronSize);
            }
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
            ActiveBattle battle = new ActiveBattle
            {
                Map = new BattleMap { Kind = BattleKind.Space },
                AttackerOwnerInstanceID = "FACTION_1",
                DefenderOwnerInstanceID = "FACTION_2",
                PlanetInstanceID = "PLANET_1",
            };
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
            CombatUnit restoredCombatUnit = restored.GetCombatants()["FACTION_1"][0];
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
            Assert.AreEqual("FACTION_1", restored.AttackerOwnerInstanceID);
            Assert.AreEqual("FACTION_2", restored.DefenderOwnerInstanceID);
        }
    }
}

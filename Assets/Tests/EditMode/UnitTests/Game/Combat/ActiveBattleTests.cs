using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.Units;
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
                Assert.AreEqual(source.InstanceID, combatUnit.SourceUnitId);
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
            };
            ActiveBattle battle = new ActiveBattle
            {
                Kind = BattleKind.Space,
                PlanetInstanceId = "PLANET_1",
            };
            battle.AddCombatants(source);

            string xml = SerializationHelper.Serialize(battle);
            ActiveBattle restored = SerializationHelper.Deserialize<ActiveBattle>(xml);
            CombatUnit restoredCombatUnit = restored.GetCombatants()["FACTION_1"][0];
            CapitalShip restoredShip = restoredCombatUnit.GetUnit() as CapitalShip;

            Assert.AreEqual(source.InstanceID, restoredCombatUnit.SourceUnitId);
            Assert.IsNotNull(restoredShip);
            Assert.AreEqual(75, restoredShip.CurrentHullStrength);
        }
    }
}

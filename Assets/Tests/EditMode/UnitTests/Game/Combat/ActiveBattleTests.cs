using System;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Combat;
using Rebellion.Game.Units;
using Rebellion.Util.Serialization;

namespace Rebellion.Tests.Game.Combat
{
    [TestFixture]
    public sealed class ActiveBattleTests
    {
        [Test]
        public void AddCombatant_CreatesIndependentCopyForOwningFaction()
        {
            CapitalShip source = new CapitalShip
            {
                InstanceID = "ship",
                OwnerInstanceID = "faction",
                CurrentHullStrength = 75,
            };
            ActiveBattle battle = CreateBattle();

            CombatUnit combatant = battle.AddCombatant(source);

            Assert.AreEqual("faction", battle.GetParticipants().Single().FactionInstanceID);
            Assert.AreSame(combatant, battle.GetParticipants().Single().GetCombatants().Single());
            Assert.AreNotSame(source, combatant.GetUnit());
            ((CapitalShip)combatant.GetUnit()).CurrentHullStrength = 10;
            Assert.AreEqual(75, source.CurrentHullStrength);
        }

        [Test]
        public void AddCombatant_DifferentOwners_CreateSeparateParticipants()
        {
            ActiveBattle battle = CreateBattle();

            battle.AddCombatant(new CapitalShip { OwnerInstanceID = "first" });
            battle.AddCombatant(new CapitalShip { OwnerInstanceID = "second" });

            CollectionAssert.AreEquivalent(
                new[] { "first", "second" },
                battle.GetParticipants().Select(participant => participant.FactionInstanceID)
            );
        }

        [Test]
        public void AddCombatant_DestroyedSquadron_DoesNotCreateParticipant()
        {
            ActiveBattle battle = CreateBattle();

            CombatUnit combatant = battle.AddCombatant(
                new Starfighter { OwnerInstanceID = "faction", CurrentSquadronSize = 0 }
            );

            Assert.IsNull(combatant);
            Assert.IsEmpty(battle.GetParticipants());
        }

        [Test]
        public void AddCombatant_UnsupportedUnit_ThrowsBeforeCreatingParticipant()
        {
            ActiveBattle battle = CreateBattle();

            Assert.Throws<ArgumentException>(() =>
                battle.AddCombatant(new Regiment { OwnerInstanceID = "faction" })
            );
            Assert.IsEmpty(battle.GetParticipants());
        }

        [Test]
        public void Serialize_RoundTripsBattleCopyAndSourceIdentity()
        {
            ActiveBattle battle = CreateBattle();
            battle.PlanetInstanceID = "planet";
            CombatUnit combatant = battle.AddCombatant(
                new CapitalShip
                {
                    InstanceID = "ship",
                    OwnerInstanceID = "faction",
                    CurrentHullStrength = 75,
                }
            );
            combatant.Position.X = 10;

            ActiveBattle restored = SerializationHelper.Deserialize<ActiveBattle>(
                SerializationHelper.Serialize(battle)
            );
            CombatUnit restoredCombatant = restored.GetCombatant("ship");

            Assert.IsNotNull(restoredCombatant);
            Assert.AreEqual("planet", restored.PlanetInstanceID);
            Assert.AreEqual(10, restoredCombatant.Position.X);
            Assert.AreEqual(75, ((CapitalShip)restoredCombatant.GetUnit()).CurrentHullStrength);
        }

        [Test]
        public void GameRootSerialization_RoundTripsActiveBattle()
        {
            GameRoot game = new GameRoot();
            ActiveBattle battle = CreateBattle();
            battle.PlanetInstanceID = "planet";
            game.SetActiveBattle(battle);

            GameRoot restored = SerializationHelper.Deserialize<GameRoot>(
                SerializationHelper.Serialize(game)
            );

            Assert.IsNotNull(restored.GetActiveBattle());
            Assert.AreEqual("planet", restored.GetActiveBattle().PlanetInstanceID);
            Assert.AreEqual(BattleKind.Space, restored.GetActiveBattle().Kind);
        }

        private static ActiveBattle CreateBattle() => new ActiveBattle { Kind = BattleKind.Space };
    }
}

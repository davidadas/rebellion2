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

            Assert.AreEqual("faction", battle.Participants.Single().FactionInstanceID);
            Assert.AreSame(combatant, battle.Participants.Single().Combatants.Single());
            Assert.AreNotSame(source, combatant.Unit);
            ((CapitalShip)combatant.Unit).CurrentHullStrength = 10;
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
                battle.Participants.Select(participant => participant.FactionInstanceID)
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
            Assert.IsEmpty(battle.Participants);
        }

        [Test]
        public void AddCombatant_UnsupportedUnit_ThrowsBeforeCreatingParticipant()
        {
            ActiveBattle battle = CreateBattle();

            Assert.Throws<ArgumentException>(() =>
                battle.AddCombatant(new Regiment { OwnerInstanceID = "faction" })
            );
            Assert.IsEmpty(battle.Participants);
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

            ActiveBattle restored = SerializationHelper.Deserialize<ActiveBattle>(
                SerializationHelper.Serialize(battle)
            );
            CombatUnit restoredCombatant = restored.GetCombatant("ship");

            Assert.IsNotNull(restoredCombatant);
            Assert.AreEqual("planet", restored.PlanetInstanceID);
            Assert.AreEqual(75, ((CapitalShip)restoredCombatant.Unit).CurrentHullStrength);
        }

        [Test]
        public void GameRootSerialization_ActiveBattle_RoundTripsForResume()
        {
            GameRoot game = new GameRoot
            {
                ActiveBattle = new ActiveBattle
                {
                    Kind = BattleKind.Space,
                    PlanetInstanceID = "planet",
                },
            };

            GameRoot restored = SerializationHelper.Deserialize<GameRoot>(
                SerializationHelper.Serialize(game)
            );

            Assert.IsNotNull(restored.ActiveBattle);
            Assert.AreEqual("planet", restored.ActiveBattle.PlanetInstanceID);
            Assert.AreEqual(BattleKind.Space, restored.ActiveBattle.Kind);
        }

        /// <summary>
        /// Creates an empty space battle for a test.
        /// </summary>
        /// <returns>The new battle.</returns>
        private static ActiveBattle CreateBattle() => new ActiveBattle { Kind = BattleKind.Space };
    }
}

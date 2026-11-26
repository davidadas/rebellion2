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

        /// <summary>
        /// Creates an empty space battle for a test.
        /// </summary>
        /// <returns>The new battle.</returns>
        private static ActiveBattle CreateBattle() => new ActiveBattle { Kind = BattleKind.Space };
    }
}

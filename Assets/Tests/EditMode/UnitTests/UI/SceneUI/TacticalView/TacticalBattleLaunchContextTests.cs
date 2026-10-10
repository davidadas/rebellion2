using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.Units;

namespace Rebellion.Tests.UI.SceneUI.TacticalView
{
    [TestFixture]
    public sealed class TacticalBattleLaunchContextTests
    {
        /// <summary>
        /// Restores the launch context before each test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            TacticalBattleLaunchContext.Reset(TestContent.Pack);
        }

        /// <summary>
        /// Restores the launch context after each test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            TacticalBattleLaunchContext.Reset(TestContent.Pack);
        }

        /// <summary>
        /// Verifies that the default battle contains one capital ship for every playable faction.
        /// </summary>
        [Test]
        public void Reset_DefaultContent_CreatesOneCapitalShipForEachPlayableFaction()
        {
            ActiveBattle battle = TacticalBattleLaunchContext.ActiveBattle;

            CollectionAssert.AreEqual(
                TestContent.Pack.Scenario.PlayableFactionIDs,
                battle.GetParticipants().Select(participant => participant.FactionInstanceID)
            );
            Assert.That(
                battle.GetParticipants(),
                Has.All.Matches<BattleParticipant>(participant =>
                    participant.GetCombatants().Count == 1
                    && participant.GetCombatants()[0].GetUnit() is CapitalShip
                )
            );
        }

        /// <summary>
        /// Verifies that default combatants occupy the authored deployment slots and centers.
        /// </summary>
        [Test]
        public void Reset_DefaultContent_AssignsMapSlotsAndDeploymentCenters()
        {
            ActiveBattle battle = TacticalBattleLaunchContext.ActiveBattle;
            BattleMap map = TestContent.Pack.GameData.BattleMaps.Single(candidate =>
                candidate.InstanceID == battle.BattleMapInstanceID
            );

            Assert.AreEqual(BattleKind.Space, battle.Kind);
            Assert.AreEqual(map.GetDeploymentRegions().Count, battle.GetParticipants().Count);
            for (int index = 0; index < battle.GetParticipants().Count; index++)
            {
                BattleParticipant participant = battle.GetParticipants()[index];
                BattleMapDeploymentRegion region = map.GetDeploymentRegions()[index];
                CombatUnit combatant = participant.GetCombatants().Single();
                Assert.AreEqual(region.ParticipantSlotID, participant.BattleMapSlotID);
                Assert.AreEqual(
                    (region.Bounds.MinimumX + region.Bounds.MaximumX) / 2f,
                    combatant.Position.X
                );
                Assert.AreEqual(
                    (region.Bounds.MinimumY + region.Bounds.MaximumY) / 2f,
                    combatant.Position.Y
                );
                Assert.AreEqual(
                    (region.Bounds.MinimumZ + region.Bounds.MaximumZ) / 2f,
                    combatant.Position.Z
                );
            }
        }

        /// <summary>
        /// Verifies that resetting discards the previously materialized default battle.
        /// </summary>
        [Test]
        public void Reset_ExistingBattle_ReplacesBattleWhenNextRequested()
        {
            ActiveBattle original = TacticalBattleLaunchContext.ActiveBattle;

            TacticalBattleLaunchContext.Reset(TestContent.Pack);

            Assert.AreNotSame(original, TacticalBattleLaunchContext.ActiveBattle);
        }
    }
}

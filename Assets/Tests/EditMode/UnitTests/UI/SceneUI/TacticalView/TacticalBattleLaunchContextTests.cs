using System;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

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

        [Test]
        public void Reset_DefaultContent_CreatesEveryConventionalCapitalShipTypeForEachPlayableFaction()
        {
            ActiveBattle battle = TacticalBattleLaunchContext.ActiveBattle;

            CollectionAssert.AreEqual(
                TestContent.Pack.Scenario.PlayableFactionIDs,
                battle.GetParticipants().Select(participant => participant.FactionInstanceID)
            );
            foreach (BattleParticipant participant in battle.GetParticipants())
            {
                string[] expected = TestContent
                    .Data.CapitalShips.Where(ship =>
                        ship.ManufacturingFactionInstanceIDs?.Contains(
                            participant.FactionInstanceID
                        ) == true
                        && !string.IsNullOrWhiteSpace(ship.ModelPath)
                        && !ship.CanDestroyPlanets
                    )
                    .Select(ship => ship.TypeID)
                    .ToArray();
                CollectionAssert.AreEquivalent(
                    expected,
                    participant
                        .GetCombatants()
                        .Select(combatant => combatant.GetUnit())
                        .OfType<CapitalShip>()
                        .Select(ship => ship.TypeID)
                );
            }
        }

        [Test]
        public void Reset_DefaultContent_ExcludesPlanetDestroyingBattleStations()
        {
            Assert.IsTrue(TestContent.Data.CapitalShips.Any(ship => ship.CanDestroyPlanets));

            ActiveBattle battle = TacticalBattleLaunchContext.ActiveBattle;

            Assert.IsFalse(
                battle
                    .GetParticipants()
                    .SelectMany(participant => participant.GetCombatants())
                    .Any(combatant =>
                        combatant.GetUnit() is CapitalShip { CanDestroyPlanets: true }
                    )
            );
        }

        [Test]
        public void Reset_DefaultContent_CreatesEveryFighterSquadronTypeForEachPlayableFaction()
        {
            ActiveBattle battle = TacticalBattleLaunchContext.ActiveBattle;
            foreach (BattleParticipant participant in battle.GetParticipants())
            {
                Starfighter[] expected = TestContent
                    .Data.Starfighters.Where(ship =>
                        ship.ManufacturingFactionInstanceIDs?.Contains(
                            participant.FactionInstanceID
                        ) == true
                        && !string.IsNullOrWhiteSpace(ship.ModelPath)
                    )
                    .ToArray();
                IGrouping<string, CombatUnit>[] groups = participant
                    .GetCombatants()
                    .Where(combatant => combatant.GetUnit() is Starfighter)
                    .GroupBy(combatant => combatant.SourceUnitInstanceID)
                    .ToArray();
                CollectionAssert.AreEquivalent(
                    expected.Select(ship => ship.TypeID),
                    groups.Select(group => group.First().GetUnit().TypeID)
                );
                foreach (Starfighter template in expected)
                {
                    CombatUnit[] squadron = groups
                        .Single(group => group.First().GetUnit().TypeID == template.TypeID)
                        .ToArray();
                    Assert.That(squadron.Length, Is.EqualTo(template.CurrentSquadronSize));
                    Assert.That(
                        squadron.Select(combatant => (Starfighter)combatant.GetUnit()),
                        Has.All.Matches<Starfighter>(fighter => fighter.CurrentSquadronSize == 1)
                    );
                }
            }
        }

        [Test]
        public void Reset_DefaultContent_AssignsMapSlotsAndDeploysEveryShip()
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
                Assert.AreEqual(region.ParticipantSlotID, participant.BattleMapSlotID);
                foreach (CombatUnit combatant in participant.GetCombatants())
                {
                    Assert.That(
                        combatant.Position.X,
                        Is.InRange(region.Bounds.MinimumX, region.Bounds.MaximumX)
                    );
                    Assert.That(
                        combatant.Position.Y,
                        Is.InRange(region.Bounds.MinimumY, region.Bounds.MaximumY)
                    );
                    Assert.That(
                        combatant.Position.Z,
                        Is.InRange(region.Bounds.MinimumZ, region.Bounds.MaximumZ)
                    );
                    Assert.AreEqual(
                        (float)Math.Sin(region.Facing.Y * Math.PI / 180d),
                        combatant.Forward.X,
                        0.0001f
                    );
                    Assert.AreEqual(
                        (float)Math.Cos(region.Facing.Y * Math.PI / 180d),
                        combatant.Forward.Z,
                        0.0001f
                    );
                }
            }
        }

        [Test]
        public void Reset_DefaultContent_PreservesCatalogTemplates()
        {
            BaseSceneNode[] templates = TestContent
                .Data.CapitalShips.Cast<BaseSceneNode>()
                .Concat(TestContent.Data.Starfighters)
                .ToArray();
            string[] identities = templates.Select(ship => ship.InstanceID).ToArray();
            string[] owners = templates.Select(ship => ship.OwnerInstanceID).ToArray();

            _ = TacticalBattleLaunchContext.ActiveBattle;

            CollectionAssert.AreEqual(identities, templates.Select(ship => ship.InstanceID));
            CollectionAssert.AreEqual(owners, templates.Select(ship => ship.OwnerInstanceID));
        }

        [Test]
        public void Reset_ExistingBattle_ReplacesBattleWhenNextRequested()
        {
            ActiveBattle original = TacticalBattleLaunchContext.ActiveBattle;

            TacticalBattleLaunchContext.Reset(TestContent.Pack);

            Assert.AreNotSame(original, TacticalBattleLaunchContext.ActiveBattle);
        }
    }
}

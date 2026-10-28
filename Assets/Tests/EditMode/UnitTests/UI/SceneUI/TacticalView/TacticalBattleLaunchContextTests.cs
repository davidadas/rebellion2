using System;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Tactical;

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
        public void Reset_DefaultContent_CreatesTwoOfEveryConventionalCapitalShipTypeForEachPlayableFaction()
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
                    .SelectMany(ship => Enumerable.Repeat(ship.TypeID, 2))
                    .ToArray();
                CollectionAssert.AreEquivalent(
                    expected,
                    participant
                        .GetCombatants()
                        .Where(ship => ship.Kind == SpaceCombatUnitKind.CapitalShip)
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
                        TestContent.Data.CapitalShips.Any(template =>
                            template.CanDestroyPlanets && template.TypeID == combatant.TypeID
                        )
                    )
            );
        }

        /// <summary>
        /// Checks every modeled fighter type participates and every copy retains its configured squadron strength.
        /// </summary>
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
                SpaceCombatUnit[] squadrons = participant
                    .GetCombatants()
                    .Where(combatant => combatant.Kind == SpaceCombatUnitKind.Starfighter)
                    .ToArray();
                CollectionAssert.AreEquivalent(
                    expected.Select(ship => ship.TypeID),
                    squadrons.Select(combatant => combatant.TypeID).Distinct()
                );
                foreach (Starfighter template in expected)
                {
                    SpaceCombatUnit[] copies = squadrons
                        .Where(combatant => combatant.TypeID == template.TypeID)
                        .ToArray();
                    Assert.That(
                        copies,
                        Has.All.Matches<SpaceCombatUnit>(fighter =>
                            fighter.Condition.CurrentSquadronSize == template.CurrentSquadronSize
                            && fighter.MaxSquadronSize == template.MaxSquadronSize
                        )
                    );
                }
            }
        }

        /// <summary>
        /// Checks every carrier with an authored launch route begins at its actual squadron capacity.
        /// </summary>
        [Test]
        public void Reset_LaunchCapableCarriers_FillsEveryFighterBerth()
        {
            foreach (
                BattleParticipant participant in TacticalBattleLaunchContext.ActiveBattle.GetParticipants()
            )
            {
                SpaceCombatUnit[] combatants = participant.GetCombatants().ToArray();
                foreach (
                    SpaceCombatUnit carrier in combatants.Where(combatant =>
                        combatant is { Kind: SpaceCombatUnitKind.CapitalShip } ship
                        && ship.GetStarfighterCapacity() > 0
                        && ship.GetComponents()
                            .OfType<Rebellion.Game.ShipComponents.StarfighterBay>()
                            .Any(bay => bay.FlightPath.Count > 0)
                    )
                )
                {
                    SpaceCombatUnit ship = carrier;
                    Assert.That(
                        combatants.Count(combatant =>
                            combatant.Kind == SpaceCombatUnitKind.Starfighter
                            && combatant.Flight.CarrierSourceUnitInstanceID
                                == carrier.SourceUnitInstanceID
                        ),
                        Is.EqualTo(ship.GetStarfighterCapacity())
                    );
                }
            }
        }

        /// <summary>
        /// Checks hyperdrive-equipped fighter types retain one deployed squadron in addition to carrier complements.
        /// </summary>
        [Test]
        public void Reset_HyperdriveFighterTypes_CreatesOneDeployedSquadron()
        {
            foreach (
                BattleParticipant participant in TacticalBattleLaunchContext.ActiveBattle.GetParticipants()
            )
            foreach (
                Starfighter template in TestContent.Data.Starfighters.Where(fighter =>
                    fighter.Hyperdrive > 0
                    && !string.IsNullOrWhiteSpace(fighter.ModelPath)
                    && fighter.ManufacturingFactionInstanceIDs?.Contains(
                        participant.FactionInstanceID
                    ) == true
                )
            )
            {
                Assert.That(
                    participant
                        .GetCombatants()
                        .Count(combatant =>
                            combatant.TypeID == template.TypeID
                            && string.IsNullOrWhiteSpace(
                                combatant.Flight.CarrierSourceUnitInstanceID
                            )
                            && combatant.Flight.Phase == FighterFlightState.Deployed
                        ),
                    Is.EqualTo(1)
                );
            }
        }

        /// <summary>
        /// Checks preview squadrons without hyperdrives begin carried by an available friendly ship.
        /// </summary>
        [Test]
        public void Reset_ViewerCarrierSquadrons_StartDockedWithFriendlyCarrier()
        {
            BattleParticipant participant = TacticalBattleLaunchContext
                .ActiveBattle.GetParticipants()
                .First();
            foreach (SpaceCombatUnit fighter in participant.GetCombatants())
            {
                if (string.IsNullOrWhiteSpace(fighter.Flight.CarrierSourceUnitInstanceID))
                    continue;
                Assert.That(fighter.Flight.Phase, Is.EqualTo(FighterFlightState.Docked));
                Assert.That(
                    participant
                        .GetCombatants()
                        .Any(unit =>
                            unit.Kind == SpaceCombatUnitKind.CapitalShip
                            && unit.SourceUnitInstanceID
                                == fighter.Flight.CarrierSourceUnitInstanceID
                        ),
                    Is.True
                );
            }
        }

        [Test]
        public void Reset_OpposingCarrierSquadrons_StartDeployed()
        {
            foreach (
                BattleParticipant participant in TacticalBattleLaunchContext
                    .ActiveBattle.GetParticipants()
                    .Skip(1)
            )
            {
                SpaceCombatUnit[] carried = participant
                    .GetCombatants()
                    .Where(combatant =>
                        !string.IsNullOrWhiteSpace(combatant.Flight.CarrierSourceUnitInstanceID)
                    )
                    .ToArray();
                Assert.That(carried, Is.Not.Empty);
                Assert.That(
                    carried,
                    Has.All.Matches<SpaceCombatUnit>(combatant =>
                        combatant.Flight.Phase == FighterFlightState.Deployed
                    )
                );
            }
        }

        [Test]
        public void Reset_DefaultContent_AssignsMapSlotsBeforeModelLoading()
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
                foreach (SpaceCombatUnit combatant in participant.GetCombatants())
                {
                    Assert.AreEqual(0f, combatant.Position.X);
                    Assert.AreEqual(0f, combatant.Position.Y);
                    Assert.AreEqual(0f, combatant.Position.Z);
                }
            }
        }

        [Test]
        public void Reset_DefaultContent_AttachesThreeFactionOfficersToOneCapitalShip()
        {
            foreach (
                BattleParticipant participant in TacticalBattleLaunchContext.ActiveBattle.GetParticipants()
            )
            {
                string[] expected = TestContent
                    .Data.Officers.Where(officer =>
                        officer.OwnerInstanceID == participant.FactionInstanceID
                    )
                    .Take(3)
                    .Select(officer => officer.InstanceID)
                    .ToArray();
                Officer[] actual = participant
                    .GetCombatants()
                    .Where(combatant => combatant.Kind == SpaceCombatUnitKind.CapitalShip)
                    .SelectMany(combatant => combatant.Officers)
                    .ToArray();

                CollectionAssert.AreEquivalent(
                    expected,
                    actual.Select(officer => officer.InstanceID)
                );
                Assert.That(
                    actual,
                    Has.All.Matches<Officer>(officer =>
                        officer.OwnerInstanceID == participant.FactionInstanceID
                    )
                );
                Assert.That(
                    actual.Select(officer => officer.ParentNode).Distinct().Count(),
                    Is.EqualTo(1)
                );
            }
        }

        [Test]
        public void Reset_DefaultContent_PreservesCatalogTemplates()
        {
            BaseSceneNode[] templates = TestContent
                .Data.CapitalShips.Cast<BaseSceneNode>()
                .Concat(TestContent.Data.Starfighters)
                .Concat(TestContent.Data.Officers)
                .ToArray();
            string[] identities = templates.Select(ship => ship.InstanceID).ToArray();
            string[] owners = templates.Select(ship => ship.OwnerInstanceID).ToArray();
            ISceneNode[] parents = templates.Select(ship => ship.ParentNode).ToArray();

            _ = TacticalBattleLaunchContext.ActiveBattle;

            CollectionAssert.AreEqual(identities, templates.Select(ship => ship.InstanceID));
            CollectionAssert.AreEqual(owners, templates.Select(ship => ship.OwnerInstanceID));
            CollectionAssert.AreEqual(parents, templates.Select(ship => ship.ParentNode));
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

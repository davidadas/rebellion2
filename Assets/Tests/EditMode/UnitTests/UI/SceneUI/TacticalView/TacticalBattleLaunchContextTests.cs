using System;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Combat;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Simulation.Tactical;

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
                CombatUnit[] squadrons = participant
                    .GetCombatants()
                    .Where(combatant => combatant.GetUnit() is Starfighter)
                    .ToArray();
                CollectionAssert.AreEquivalent(
                    expected.Select(ship => ship.TypeID),
                    squadrons.Select(combatant => combatant.GetUnit().TypeID).Distinct()
                );
                foreach (Starfighter template in expected)
                {
                    CombatUnit[] copies = squadrons
                        .Where(combatant => combatant.GetUnit().TypeID == template.TypeID)
                        .ToArray();
                    Assert.That(
                        copies.Select(combatant => (Starfighter)combatant.GetUnit()),
                        Has.All.Matches<Starfighter>(fighter =>
                            fighter.CurrentSquadronSize == template.CurrentSquadronSize
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
                CombatUnit[] combatants = participant.GetCombatants().ToArray();
                foreach (
                    CombatUnit carrier in combatants.Where(combatant =>
                        combatant.GetUnit() is CapitalShip ship
                        && ship.GetStarfighterCapacity() > 0
                        && ship.GetComponents()
                            .OfType<Rebellion.Game.ShipComponents.StarfighterBay>()
                            .Any(bay => bay.FlightPath.Count > 0)
                    )
                )
                {
                    CapitalShip ship = (CapitalShip)carrier.GetUnit();
                    Assert.That(
                        combatants.Count(combatant =>
                            combatant.GetUnit() is Starfighter
                            && combatant.CarrierSourceUnitInstanceID == carrier.SourceUnitInstanceID
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
                            combatant.GetUnit().TypeID == template.TypeID
                            && string.IsNullOrWhiteSpace(combatant.CarrierSourceUnitInstanceID)
                            && combatant.FlightState == FighterFlightState.Deployed
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
            foreach (CombatUnit fighter in participant.GetCombatants())
            {
                if (string.IsNullOrWhiteSpace(fighter.CarrierSourceUnitInstanceID))
                    continue;
                Assert.That(fighter.FlightState, Is.EqualTo(FighterFlightState.Docked));
                Assert.That(
                    participant
                        .GetCombatants()
                        .Any(unit =>
                            unit.GetUnit() is CapitalShip
                            && unit.SourceUnitInstanceID == fighter.CarrierSourceUnitInstanceID
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
                CombatUnit[] carried = participant
                    .GetCombatants()
                    .Where(combatant =>
                        !string.IsNullOrWhiteSpace(combatant.CarrierSourceUnitInstanceID)
                    )
                    .ToArray();
                Assert.That(carried, Is.Not.Empty);
                Assert.That(
                    carried,
                    Has.All.Matches<CombatUnit>(combatant =>
                        combatant.FlightState == FighterFlightState.Deployed
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
                foreach (CombatUnit combatant in participant.GetCombatants())
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
                    .SelectMany(combatant =>
                        combatant.GetUnit() is CapitalShip ship
                            ? ship.GetChildren<Officer>(includeDisabled: true)
                            : Array.Empty<Officer>()
                    )
                    .ToArray();

                CollectionAssert.AreEquivalent(
                    expected,
                    actual.Select(officer => officer.InstanceID)
                );
                Assert.That(
                    actual,
                    Has.All.Matches<Officer>(officer => officer.ParentNode is CapitalShip)
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

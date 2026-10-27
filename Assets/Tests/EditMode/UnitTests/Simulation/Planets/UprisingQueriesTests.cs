using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public class UprisingQueriesTests
    {
        [TestCase(80, 0)]
        [TestCase(60, 0)]
        [TestCase(55, 1)]
        [TestCase(50, 1)]
        [TestCase(40, 2)]
        [TestCase(30, 3)]
        [TestCase(20, 4)]
        [TestCase(10, 5)]
        [TestCase(0, 6)]
        public void CalculateGarrisonRequirement_StandardPlanet_UsesSupportDeficit(
            int support,
            int expectedGarrison
        )
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = TestGame.Create(config);
            Faction faction = new Faction { InstanceID = "empire" };
            game.GetFactions().Add(faction);

            PlanetSector planetSector = new PlanetSector
            {
                InstanceID = "sector1",
                SectorType = PlanetSectorType.OuterRim,
            };
            game.AttachNode(planetSector, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PopularSupport = new Dictionary<string, int> { { "empire", support } },
            };
            game.AttachNode(planet, planetSector);

            int garrison = UprisingQueries.CalculateGarrisonRequirement(
                planet,
                faction,
                config.AI.Garrison
            );

            Assert.AreEqual(expectedGarrison, garrison, $"Garrison for support={support}");
        }

        [Test]
        public void CalculateGarrisonRequirement_CoreWorldWithoutSuppressionSource_NotHalved()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = TestGame.Create(config);
            Faction empire = new Faction
            {
                InstanceID = "empire",
                Settings = new FactionSettings { GarrisonEfficiency = 2 },
            };
            game.GetFactions().Add(empire);

            PlanetSector planetSector = new PlanetSector
            {
                InstanceID = "sector1",
                SectorType = PlanetSectorType.Core,
            };
            game.AttachNode(planetSector, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PopularSupport = new Dictionary<string, int> { { "empire", 20 } },
            };
            game.AttachNode(planet, planetSector);

            // Sector region alone does not reduce the support-based requirement.
            int garrison = UprisingQueries.CalculateGarrisonRequirement(
                planet,
                empire,
                config.AI.Garrison
            );

            Assert.AreEqual(4, garrison);
        }

        [Test]
        public void CalculateGarrisonRequirement_CoreWorldAlliance_NotHalved()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = TestGame.Create(config);
            Faction alliance = new Faction
            {
                InstanceID = "alliance",
                Settings = new FactionSettings { GarrisonEfficiency = 1 },
            };
            game.GetFactions().Add(alliance);

            PlanetSector planetSector = new PlanetSector
            {
                InstanceID = "sector1",
                SectorType = PlanetSectorType.Core,
            };
            game.AttachNode(planetSector, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "alliance",
                IsColonized = true,
                PopularSupport = new Dictionary<string, int> { { "alliance", 20 } },
            };
            game.AttachNode(planet, planetSector);

            // Base: ceil((60-20)/10) = 4. Alliance: no halving.
            int garrison = UprisingQueries.CalculateGarrisonRequirement(
                planet,
                alliance,
                config.AI.Garrison
            );

            Assert.AreEqual(4, garrison, "Alliance core world should NOT halve garrison");
        }

        [Test]
        public void CalculateGarrisonRequirement_EfficientCoreFactionBelowThreshold_RequiresOneTroop()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = TestGame.Create(config);
            Faction faction = new Faction
            {
                InstanceID = "faction",
                Settings = new FactionSettings { GarrisonEfficiency = 2 },
            };
            game.GetFactions().Add(faction);

            PlanetSector planetSector = new PlanetSector
            {
                InstanceID = "sector1",
                SectorType = PlanetSectorType.Core,
            };
            game.AttachNode(planetSector, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = faction.InstanceID,
                IsColonized = true,
                PopularSupport = new Dictionary<string, int> { { faction.InstanceID, 55 } },
            };
            game.AttachNode(planet, planetSector);

            // A zero requirement would allow the last regiment to leave even though support is
            // below the threshold needed to retain control of the planet.
            int garrison = UprisingQueries.CalculateGarrisonRequirement(
                planet,
                faction,
                config.AI.Garrison
            );

            Assert.AreEqual(
                1,
                garrison,
                "A controlled planet below the support threshold must require one troop"
            );
        }

        /// <summary>
        /// Verifies that region, suppression strength, and uprising state compose.
        /// </summary>
        /// <param name="region">The sector's galactic region.</param>
        /// <param name="suppression">The ship's suppression divisor.</param>
        /// <param name="present">Whether a suppression source is deployed.</param>
        /// <param name="uprising">Whether the planet is in uprising.</param>
        /// <param name="support">Popular support for the controller.</param>
        /// <param name="expected">The required regiment count.</param>
        [TestCase(PlanetSectorType.Core, 2, false, false, 0, 6)]
        [TestCase(PlanetSectorType.Core, 2, false, true, 0, 12)]
        [TestCase(PlanetSectorType.OuterRim, 2, false, false, 0, 6)]
        [TestCase(PlanetSectorType.OuterRim, 2, false, true, 0, 12)]
        [TestCase(PlanetSectorType.Core, 2, true, false, 0, 3)]
        [TestCase(PlanetSectorType.Core, 2, true, true, 0, 6)]
        [TestCase(PlanetSectorType.OuterRim, 2, true, false, 0, 3)]
        [TestCase(PlanetSectorType.OuterRim, 2, true, true, 0, 6)]
        [TestCase(PlanetSectorType.Core, 1, true, false, 0, 6)]
        [TestCase(PlanetSectorType.OuterRim, 1, true, true, 0, 12)]
        [TestCase(PlanetSectorType.Core, 2, true, true, 10, 4)]
        [TestCase(PlanetSectorType.Core, 2, true, true, 50, 0)]
        [TestCase(PlanetSectorType.Core, 2, true, false, 55, 0)]
        [TestCase(PlanetSectorType.Core, 2, true, true, 60, 0)]
        [TestCase(PlanetSectorType.Core, 3, true, false, 0, 2)]
        [TestCase(PlanetSectorType.OuterRim, 3, true, true, 0, 4)]
        [TestCase(PlanetSectorType.Core, 0, true, false, 0, 6)]
        public void CalculateGarrisonRequirement_SectorSuppression_UsesAuthoredShipDivisor(
            PlanetSectorType region,
            int suppression,
            bool present,
            bool uprising,
            int support,
            int expected
        )
        {
            var (game, faction, planet, sector) = CreateSuppressionScene();
            sector.SectorType = region;
            planet.SetPopularSupport(faction.InstanceID, support);
            if (present)
                AddSuppressionSource(game, planet).SectorSuppression = suppression;
            if (uprising)
                planet.BeginUprising();
            Assert.AreEqual(expected, Requirement(game, faction, planet));
        }

        /// <summary>
        /// Verifies that suppression strength is independent of planet destruction.
        /// </summary>
        /// <param name="canDestroyPlanets">Whether the ship can destroy planets.</param>
        /// <param name="suppression">The authored sector divisor.</param>
        /// <param name="expected">The required regiment count.</param>
        [TestCase(true, 0, 6)]
        [TestCase(false, 2, 3)]
        [TestCase(true, 3, 2)]
        public void CalculateGarrisonRequirement_IndependentShipCapabilities_UsesOnlySuppression(
            bool canDestroyPlanets,
            int suppression,
            int expected
        )
        {
            var (game, faction, planet, _) = CreateSuppressionScene();
            CapitalShip ship = AddSuppressionSource(game, planet);
            ship.CanDestroyPlanets = canDestroyPlanets;
            ship.SectorSuppression = suppression;
            Assert.AreEqual(expected, Requirement(game, faction, planet));
        }

        /// <summary>
        /// Verifies that the strongest active source sets the divisor without stacking.
        /// </summary>
        [Test]
        public void CalculateGarrisonRequirement_MultipleSuppressionShips_UsesStrongestDivisor()
        {
            var (game, faction, planet, _) = CreateSuppressionScene();
            CapitalShip first = AddSuppressionSource(game, planet);
            CapitalShip stronger = new CapitalShip
            {
                InstanceID = "stronger",
                OwnerInstanceID = faction.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                SectorSuppression = 3,
            };
            game.AttachNode(stronger, first.GetParent());
            Assert.AreEqual(2, Requirement(game, faction, planet));
            game.DeleteNode(stronger);
            Assert.AreEqual(3, Requirement(game, faction, planet));
        }

        /// <summary>
        /// Verifies that unavailable ships and fleets do not provide sector suppression.
        /// </summary>
        /// <param name="unavailable">The state preventing deployment.</param>
        [TestCase("building")]
        [TestCase("disabled-ship")]
        [TestCase("disabled-fleet")]
        [TestCase("moving-ship")]
        [TestCase("moving-fleet")]
        public void CalculateGarrisonRequirement_UnavailableSuppressionSource_DoesNotReduceRequirement(
            string unavailable
        )
        {
            var (game, faction, planet, _) = CreateSuppressionScene();
            CapitalShip ship = AddSuppressionSource(game, planet);
            Fleet fleet = ship.GetParentOfType<Fleet>();
            switch (unavailable)
            {
                case "building":
                    ship.ManufacturingStatus = ManufacturingStatus.Building;
                    break;
                case "disabled-ship":
                    ship.IsEnabled = false;
                    break;
                case "disabled-fleet":
                    fleet.IsEnabled = false;
                    break;
                case "moving-ship":
                    ship.Movement = new MovementState();
                    break;
                case "moving-fleet":
                    fleet.Movement = new MovementState();
                    break;
            }
            Assert.AreEqual(6, Requirement(game, faction, planet));
        }

        /// <summary>
        /// Verifies that moving a carrier updates both sectors without reparenting each ship.
        /// </summary>
        [Test]
        public void CalculateGarrisonRequirement_FleetChangesSector_UpdatesBothRequirements()
        {
            var (game, faction, planet, _) = CreateSuppressionScene();
            CapitalShip ship = AddSuppressionSource(game, planet);
            PlanetSector destinationSector = new PlanetSector { InstanceID = "destination-sector" };
            Planet destination = new Planet
            {
                InstanceID = "destination",
                OwnerInstanceID = faction.InstanceID,
            };
            game.AttachNode(destinationSector, game.Galaxy);
            game.AttachNode(destination, destinationSector);
            Assert.AreEqual(3, Requirement(game, faction, planet));
            Assert.AreEqual(6, Requirement(game, faction, destination));
            game.MoveNode(ship.GetParentOfType<Fleet>(), destination);
            Assert.AreEqual(6, Requirement(game, faction, planet));
            Assert.AreEqual(3, Requirement(game, faction, destination));
        }

        /// <summary>
        /// Verifies that completion, capability changes, and removal affect current eligibility.
        /// </summary>
        [Test]
        public void CalculateGarrisonRequirement_SuppressionSourceLifecycle_UsesCurrentEligibility()
        {
            var (game, faction, planet, _) = CreateSuppressionScene();
            CapitalShip ship = AddSuppressionSource(game, planet);
            ship.ManufacturingStatus = ManufacturingStatus.Building;
            Assert.AreEqual(6, Requirement(game, faction, planet));
            ship.ManufacturingStatus = ManufacturingStatus.Complete;
            Assert.AreEqual(3, Requirement(game, faction, planet));
            ship.SectorSuppression = 0;
            Assert.AreEqual(6, Requirement(game, faction, planet));
            ship.SectorSuppression = 2;
            Assert.AreEqual(3, Requirement(game, faction, planet));
            game.DeleteNode(ship.GetParentOfType<Fleet>());
            Assert.AreEqual(6, Requirement(game, faction, planet));
        }

        /// <summary>
        /// Verifies that an enemy suppression source does not reduce the owner's requirement.
        /// </summary>
        [Test]
        public void CalculateGarrisonRequirement_EnemySuppressionSource_DoesNotReduceRequirement()
        {
            var (game, faction, planet, sector) = CreateSuppressionScene();
            Faction enemy = new Faction { InstanceID = "enemy" };
            Planet enemyPlanet = new Planet
            {
                InstanceID = "enemy-planet",
                OwnerInstanceID = enemy.InstanceID,
            };
            game.GetFactions().Add(enemy);
            game.AttachNode(enemyPlanet, sector);
            AddSuppressionSource(game, enemyPlanet);

            Assert.AreEqual(6, Requirement(game, faction, planet));
        }

        /// <summary>
        /// Verifies that copies rebuild presence from their own ship instances.
        /// </summary>
        [Test]
        public void CalculateGarrisonRequirement_CopiedSector_KeepsIndependentPresence()
        {
            var (game, faction, planet, sector) = CreateSuppressionScene();
            CapitalShip ship = AddSuppressionSource(game, planet);
            PlanetSector copy = (PlanetSector)sector.CreateCopy(recursive: true);
            Planet copiedPlanet = copy.GetChildren<Planet>().Single();
            game.DeleteNode(ship);
            Assert.AreEqual(6, Requirement(game, faction, planet));
            Assert.AreEqual(3, Requirement(game, faction, copiedPlanet));
        }

        /// <summary>
        /// Verifies that restored sector descendants rebuild runtime suppression membership.
        /// </summary>
        [Test]
        public void CalculateGarrisonRequirement_RestoredSector_RebuildsSuppressionPresence()
        {
            var (game, faction, planet, sector) = CreateSuppressionScene();
            AddSuppressionSource(game, planet);
            PlanetSector restored = SerializationHelper.Deserialize<PlanetSector>(
                SerializationHelper.Serialize(sector)
            );
            game.DeleteNode(sector);
            game.AttachNode(restored, game.Galaxy);
            game.RebuildSceneState();
            Assert.AreEqual(3, Requirement(game, faction, restored.GetChildren<Planet>().Single()));
        }

        /// <summary>
        /// Verifies that faction views retain owner-scoped suppression from their own ships.
        /// </summary>
        [Test]
        public void CalculateGarrisonRequirement_FactionViewOwnSuppression_PreservesRequirement()
        {
            var (game, faction, planet, _) = CreateSuppressionScene();
            AddSuppressionSource(game, planet);
            GalaxyMap view = new FogOfWarQueries(game).BuildFactionView(faction);
            Planet observed = view.GetChildren<PlanetSector>()
                .Single()
                .GetChildren<Planet>()
                .Single(p => p.InstanceID == planet.InstanceID);
            Assert.AreEqual(3, Requirement(game, faction, observed));
        }

        /// <summary>
        /// Verifies that a projected enemy suppression source does not reduce the requirement.
        /// </summary>
        [Test]
        public void CalculateGarrisonRequirement_FactionViewEnemySuppression_DoesNotReduceRequirement()
        {
            var (game, faction, planet, sector) = CreateSuppressionScene();
            Planet other = new Planet { InstanceID = "other", OwnerInstanceID = "other-faction" };
            game.GetFactions().Add(new Faction { InstanceID = "other-faction" });
            game.AttachNode(other, sector);
            AddSuppressionSource(game, other);
            GalaxyMap view = new FogOfWarQueries(game).BuildFactionView(faction);
            Planet observed = view.GetChildren<PlanetSector>()
                .Single()
                .GetChildren<Planet>()
                .Single(p => p.InstanceID == planet.InstanceID);
            Assert.AreEqual(6, Requirement(game, faction, observed));
        }

        /// <summary>
        /// Builds an owned zero-support planet with no suppression sources.
        /// </summary>
        /// <returns>The game, faction, target planet, and sector.</returns>
        private static (
            GameRoot game,
            Faction faction,
            Planet planet,
            PlanetSector sector
        ) CreateSuppressionScene()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            Faction faction = new Faction
            {
                InstanceID = "empire",
                Settings = new FactionSettings { GarrisonEfficiency = 2 },
            };
            game.GetFactions().Add(faction);
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            Planet planet = new Planet
            {
                InstanceID = "target",
                OwnerInstanceID = faction.InstanceID,
                IsColonized = true,
                PopularSupport = new Dictionary<string, int> { { faction.InstanceID, 0 } },
            };
            game.AttachNode(sector, game.Galaxy);
            game.AttachNode(planet, sector);
            return (game, faction, planet, sector);
        }

        /// <summary>
        /// Deploys an operational ship capable of destroying planets.
        /// </summary>
        /// <param name="game">The owning scene.</param>
        /// <param name="planet">The deployment location.</param>
        /// <returns>The deployed ship.</returns>
        private static CapitalShip AddSuppressionSource(GameRoot game, Planet planet)
        {
            Fleet fleet = new Fleet
            {
                InstanceID = "fleet",
                OwnerInstanceID = planet.OwnerInstanceID,
            };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "ship",
                OwnerInstanceID = planet.OwnerInstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                SectorSuppression = 2,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            return ship;
        }

        /// <summary>
        /// Evaluates the shared public requirement query.
        /// </summary>
        /// <param name="game">The game supplying the rule configuration.</param>
        /// <param name="faction">The controlling faction.</param>
        /// <param name="planet">The planet to evaluate.</param>
        /// <returns>The required regiment count.</returns>
        private static int Requirement(GameRoot game, Faction faction, Planet planet)
        {
            return UprisingQueries.CalculateGarrisonRequirement(
                planet,
                faction,
                game.Config.AI.Garrison
            );
        }
    }
}

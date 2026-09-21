using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Simulation;
using Rebellion.Util.Random;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public class UprisingObserverTests
    {
        /// <summary>Verifies an absent garrison-result batch produces no reactions.</summary>
        [Test]
        public void HandleResults_NullBatch_ReturnsNoReactions()
        {
            (_, _, UprisingObserver system) = BuildScene(rng: new ThrowingRNG());

            Assert.IsEmpty(system.HandleResults(null));
        }

        /// <summary>Verifies a malformed later result does not undo the earlier uprising start.</summary>
        [Test]
        public void HandleResults_NullEntryAfterValidPlanet_ThrowsAfterStartingUprising()
        {
            (_, Planet planet, UprisingObserver system) = BuildScene(troopCount: 4);

            Assert.Throws<System.NullReferenceException>(() =>
                system.HandleResults(
                    new PlanetGarrisonChangedResult[]
                    {
                        new PlanetGarrisonChangedResult { Planet = planet },
                        null,
                    }
                )
            );

            Assert.IsTrue(planet.IsInUprising);
        }

        /// <summary>Verifies starts uprising when garrison deficit.</summary>
        [Test]
        public void HandleResults_GarrisonDeficit_StartsUprising()
        {
            (GameRoot game, Planet planet, UprisingObserver system) = BuildScene(
                ownerSupport: 10,
                troopCount: 5
            );
            Regiment departingRegiment = planet.GetChildren<Regiment>()[0];
            game.DetachNode(departingRegiment);

            List<GameResult> results = system.HandleResults(
                new PlanetGarrisonChangedResult[]
                {
                    new PlanetGarrisonChangedResult { Planet = planet },
                }
            );

            Assert.IsTrue(planet.IsInUprising);
            Assert.AreEqual(1, results.OfType<PlanetUprisingStartedResult>().Count());
        }

        /// <summary>
        /// Builds scene.
        /// </summary>
        /// <param name="ownerSupport">The owner support.</param>
        /// <param name="opposingSupport">The opposing support.</param>
        /// <param name="troopCount">The troop count.</param>
        /// <param name="isCoreSector">Whether is core sector.</param>
        /// <param name="rng">The rng.</param>
        /// <returns>The constructed scene.</returns>
        private (GameRoot game, Planet planet, UprisingObserver system) BuildScene(
            int ownerSupport = 10,
            int opposingSupport = 50,
            int troopCount = 0,
            bool isCoreSector = false,
            IRandomNumberProvider rng = null
        )
        {
            GameConfig config = TestConfig.Create();
            config.Uprising.ActiveSupportDriftMinTicks = 1;
            config.Uprising.ActiveSupportDriftMaxTicks = 1;
            config.Uprising.IncidentPulseMinTicks = 1;
            config.Uprising.IncidentPulseMaxTicks = 1;
            config.Uprising.ClearUprisingMinTicks = 1;
            config.Uprising.ClearUprisingMaxTicks = 1;
            GameRoot game = new GameRoot(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });

            PlanetSector planetSector = new PlanetSector
            {
                InstanceID = "sector1",
                SectorType = isCoreSector ? PlanetSectorType.Core : PlanetSectorType.OuterRim,
            };
            game.AttachNode(planetSector, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PopularSupport = new Dictionary<string, int>
                {
                    { "empire", ownerSupport },
                    { "rebels", opposingSupport },
                },
            };
            game.AttachNode(planet, planetSector);

            // Add garrison troops
            for (int i = 0; i < troopCount; i++)
            {
                Regiment regiment = EntityFactory.CreateRegiment($"r{i}", "empire");
                regiment.ManufacturingStatus = ManufacturingStatus.Complete;
                game.AttachNode(regiment, planet);
            }

            MovementCommands movementSystem = new MovementCommands(
                game,
                new FogOfWarCommands(game),
                new FleetCommands(game),
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            PlanetaryControlCommands planetaryControl = new PlanetaryControlCommands(
                game,
                movementSystem,
                new ManufacturingCommands(
                    game,
                    new FleetCommands(game),
                    new ManufacturingQueries(game)
                ),
                new FogOfWarCommands(game),
                new PlanetaryControlQueries(game),
                new FogOfWarQueries(game)
            );
            UprisingCommands uprisingSystem = new UprisingCommands(
                game,
                rng ?? new StubRNG(),
                planetaryControl
            );
            return (game, planet, new UprisingObserver(uprisingSystem));
        }
    }
}

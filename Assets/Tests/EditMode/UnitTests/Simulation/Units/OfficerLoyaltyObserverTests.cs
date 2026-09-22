using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public class OfficerLoyaltyObserverTests
    {
        [Test]
        public void HandleResults_MultipleOwnershipChanges_AppliesRollsInBatchOrder()
        {
            GameRoot game = BuildScene(out Planet planet, out Officer officer);
            Faction owner = game.GetFactions().Single();
            Faction opponent = new Faction { InstanceID = "opponent" };
            game.GetFactions().Add(opponent);
            officer.Loyalty = 99;
            SequenceRNG random = new SequenceRNG(new[] { 5, 2, 4 });
            OfficerLoyaltyObserver observer = new OfficerLoyaltyObserver(
                new OfficerLoyaltyCommands(game, random)
            );

            List<GameResult> results = observer.HandleResults(
                new[]
                {
                    new PlanetOwnershipChangedResult { Planet = planet, NewOwner = owner },
                    null,
                    new PlanetOwnershipChangedResult { Planet = planet, NewOwner = null },
                    new PlanetOwnershipChangedResult { Planet = planet, NewOwner = opponent },
                }
            );

            Assert.AreEqual(98, officer.Loyalty);
            Assert.AreEqual(4, random.NextInt(0, 6));
            Assert.IsEmpty(results);
        }

        [Test]
        public void HandleResults_NullBatch_DoesNotConsumeRandomRoll()
        {
            GameRoot game = BuildScene(out _, out _);
            SequenceRNG random = new SequenceRNG(new[] { 5 });
            OfficerLoyaltyObserver observer = new OfficerLoyaltyObserver(
                new OfficerLoyaltyCommands(game, random)
            );

            Assert.IsEmpty(observer.HandleResults(null));
            Assert.AreEqual(5, random.NextInt(0, 6));
        }

        /// <summary>
        /// Builds scene.
        /// </summary>
        /// <param name="planet">Receives the planet.</param>
        /// <param name="officer">Receives the officer.</param>
        /// <returns>The constructed scene.</returns>
        private static GameRoot BuildScene(out Planet planet, out Officer officer)
        {
            GameConfig config = TestConfig.Create();
            config.OfficerLoyalty.PlanetAcquisitionLoyaltyShift.Minimum = 0;
            config.OfficerLoyalty.PlanetAcquisitionLoyaltyShift.Maximum = 5;
            GameRoot game = new GameRoot(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            game.AttachNode(sector, game.Galaxy);
            planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = "empire",
                IsColonized = true,
            };
            game.AttachNode(planet, sector);
            officer = EntityFactory.CreateOfficer("officer", "empire");
            game.AttachNode(officer, planet);
            return game;
        }
    }
}

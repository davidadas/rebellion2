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
        public void HandleResults_OwnershipChange_UsesChangedPlanetSupport()
        {
            GameRoot game = BuildScene(
                out Planet planet,
                out Officer officer,
                canBetray: true,
                loyalty: 50
            );
            Faction owner = game.GetFactions().Single();
            Faction opponent = new Faction { InstanceID = "opponent" };
            game.GetFactions().Add(opponent);
            planet.PopularSupport = new Dictionary<string, int>
            {
                { owner.InstanceID, 0 },
                { opponent.InstanceID, 100 },
            };
            OfficerLoyaltyObserver observer = new OfficerLoyaltyObserver(
                new OfficerLoyaltyCommands(game, new ThrowingRNG())
            );

            List<GameResult> results = observer.HandleResults(
                new[]
                {
                    new PlanetOwnershipChangedResult
                    {
                        Planet = planet,
                        PreviousOwner = owner,
                        NewOwner = opponent,
                    },
                }
            );

            Assert.AreEqual(48, officer.Loyalty);
            Assert.IsEmpty(results);
        }

        [Test]
        public void HandleResults_NullBatch_DoesNotChangeLoyalty()
        {
            GameRoot game = BuildScene(out _, out Officer officer, canBetray: true, loyalty: 50);
            OfficerLoyaltyObserver observer = new OfficerLoyaltyObserver(
                new OfficerLoyaltyCommands(game, new ThrowingRNG())
            );

            Assert.IsEmpty(observer.HandleResults(null));
            Assert.AreEqual(50, officer.Loyalty);
        }

        [Test]
        public void HandleResults_OwnerDoesNotChange_DoesNotChangeLoyalty()
        {
            GameRoot game = BuildScene(
                out Planet planet,
                out Officer officer,
                canBetray: true,
                loyalty: 50
            );
            Faction owner = game.GetFactions().Single();
            Faction opponent = new Faction { InstanceID = "opponent" };
            game.GetFactions().Add(opponent);
            planet.PopularSupport = new Dictionary<string, int>
            {
                { owner.InstanceID, 100 },
                { opponent.InstanceID, 0 },
            };
            OfficerLoyaltyObserver observer = new OfficerLoyaltyObserver(
                new OfficerLoyaltyCommands(game, new ThrowingRNG())
            );

            observer.HandleResults(
                new[]
                {
                    new PlanetOwnershipChangedResult
                    {
                        Planet = planet,
                        PreviousOwner = owner,
                        NewOwner = owner,
                    },
                }
            );

            Assert.AreEqual(50, officer.Loyalty);
        }

        /// <summary>
        /// Builds scene.
        /// </summary>
        /// <param name="planet">Receives the planet.</param>
        /// <param name="officer">Receives the officer.</param>
        /// <param name="canBetray">Whether the officer's loyalty can change and permit betrayal.</param>
        /// <param name="loyalty">The officer's starting loyalty.</param>
        /// <returns>The constructed scene.</returns>
        private static GameRoot BuildScene(
            out Planet planet,
            out Officer officer,
            bool canBetray = false,
            int loyalty = 100
        )
        {
            GameConfig config = TestConfig.Create();
            config.OfficerLoyalty.PlanetAcquisitionSupportDivisor = 80;
            GameRoot game = TestGame.Create(config);
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
            officer = EntityFactory.CreateOfficer("officer", "empire", canBetray, loyalty);
            game.AttachNode(officer, planet);
            return game;
        }
    }
}

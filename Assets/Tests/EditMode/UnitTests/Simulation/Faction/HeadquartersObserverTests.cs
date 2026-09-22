using System.Collections.Generic;
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
    public class HeadquartersObserverTests
    {
        /// <summary>Verifies that a missing arrival batch has no consequences.</summary>
        [Test]
        public void HandleResults_NullArrivals_ReturnsNoResults()
        {
            (GameRoot game, _, _, _, _) = CreateGame(isMobile: true);
            HeadquartersObserver observer = new HeadquartersObserver(CreateCommands(game));

            Assert.IsEmpty(observer.HandleResults((IReadOnlyList<UnitArrivedResult>)null));
        }

        /// <summary>Verifies that a missing ownership batch has no consequences.</summary>
        [Test]
        public void HandleResults_NullOwnershipChanges_ReturnsNoResults()
        {
            (GameRoot game, _, _, _, _) = CreateGame(isMobile: true);
            HeadquartersObserver observer = new HeadquartersObserver(CreateCommands(game));

            Assert.IsEmpty(
                observer.HandleResults((IReadOnlyList<PlanetOwnershipChangedResult>)null)
            );
        }

        /// <summary>Verifies that the last arrival determines the headquarters location.</summary>
        [Test]
        public void HandleResults_MultipleArrivals_AppliesInBatchOrder()
        {
            (GameRoot game, Faction faction, Planet origin, Planet destination, Building hq) =
                CreateGame(isMobile: true);
            HeadquartersObserver observer = new HeadquartersObserver(CreateCommands(game));

            List<GameResult> results = observer.HandleResults(
                new List<UnitArrivedResult>
                {
                    new UnitArrivedResult { Unit = hq, Destination = destination },
                    null,
                    new UnitArrivedResult { Unit = hq, Destination = origin },
                }
            );

            Assert.IsTrue(origin.IsHeadquarters);
            Assert.IsFalse(destination.IsHeadquarters);
            Assert.AreEqual(origin.InstanceID, faction.HQInstanceID);
            Assert.IsEmpty(results);
        }

        /// <summary>Verifies that capture and recapture consequences retain the input batch order.</summary>
        [Test]
        public void HandleResults_RepeatedOwnershipChanges_ReturnsOrderedCaptureResults()
        {
            (GameRoot game, Faction faction, Planet origin, _, _) = CreateGame(isMobile: false);
            Faction firstAttacker = new Faction { InstanceID = "first" };
            Faction secondAttacker = new Faction { InstanceID = "second" };
            game.GetFactions().Add(firstAttacker);
            game.GetFactions().Add(secondAttacker);
            game.CurrentTick = 42;
            HeadquartersObserver observer = new HeadquartersObserver(CreateCommands(game));

            List<GameResult> results = observer.HandleResults(
                new List<PlanetOwnershipChangedResult>
                {
                    new PlanetOwnershipChangedResult
                    {
                        Planet = origin,
                        PreviousOwner = faction,
                        NewOwner = firstAttacker,
                        Tick = 10,
                    },
                    null,
                    new PlanetOwnershipChangedResult
                    {
                        Planet = origin,
                        PreviousOwner = firstAttacker,
                        NewOwner = faction,
                        Tick = 11,
                    },
                    new PlanetOwnershipChangedResult
                    {
                        Planet = origin,
                        PreviousOwner = faction,
                        NewOwner = secondAttacker,
                        Tick = 12,
                    },
                }
            );

            Assert.AreEqual(2, results.Count);
            Assert.AreSame(firstAttacker, ((HeadquartersCapturedResult)results[0]).Attacker);
            Assert.AreSame(secondAttacker, ((HeadquartersCapturedResult)results[1]).Attacker);
            Assert.AreEqual(42, results[0].Tick);
            Assert.AreEqual(42, results[1].Tick);
            Assert.IsFalse(origin.IsHeadquarters);
            Assert.AreEqual(origin.InstanceID, faction.HQInstanceID);
        }

        /// <summary>Creates headquarters operations with the game's movement dependencies.</summary>
        /// <param name="game">The active game graph.</param>
        /// <returns>The headquarters command implementation.</returns>
        private static HeadquartersCommands CreateCommands(GameRoot game)
        {
            MovementQueries movementQueries = new MovementQueries(game);
            MovementCommands movement = new MovementCommands(
                game,
                new FogOfWarCommands(game),
                new FleetCommands(game),
                new FogOfWarQueries(game),
                movementQueries
            );
            HeadquartersQueries queries = new HeadquartersQueries(game);
            movementQueries.SetCompletedBuildingMovementPolicy(queries.CanMove);
            return new HeadquartersCommands(game, movement, queries);
        }

        /// <summary>Creates a faction with headquarters and two registered planets.</summary>
        /// <param name="isMobile">Whether the headquarters may relocate.</param>
        /// <returns>The game, faction, origin, destination and headquarters building.</returns>
        private static (GameRoot, Faction, Planet, Planet, Building) CreateGame(bool isMobile)
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction faction = new Faction
            {
                InstanceID = "alliance",
                HQInstanceID = "origin",
                Settings = new FactionSettings
                {
                    Headquarters = new HeadquartersSettings
                    {
                        FacilityTypeID = "BDHQ01",
                        IsMobile = isMobile,
                    },
                },
            };
            game.GetFactions().Add(faction);

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector" };
            game.AttachNode(planetSector, game.GetGalaxyMap());
            Planet origin = new Planet
            {
                InstanceID = "origin",
                OwnerInstanceID = faction.InstanceID,
                IsColonized = true,
                IsHeadquarters = true,
                EnergyCapacity = 1,
            };
            Planet destination = new Planet
            {
                InstanceID = "destination",
                OwnerInstanceID = faction.InstanceID,
                IsColonized = true,
                EnergyCapacity = 2,
                PositionX = 100,
            };
            game.AttachNode(origin, planetSector);
            game.AttachNode(destination, planetSector);

            Building headquarters = new Building
            {
                InstanceID = "headquarters",
                TypeID = "BDHQ01",
                OwnerInstanceID = faction.InstanceID,
                BuildingType = BuildingType.Headquarters,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(headquarters, origin);
            return (game, faction, origin, destination, headquarters);
        }
    }
}

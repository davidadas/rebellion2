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
    public class HeadquartersCommandsTests
    {
        [Test]
        public void TryRelocate_MobileHeadquarters_DepartsAndClearsPlanetMarker()
        {
            (GameRoot game, Faction faction, Planet origin, Planet destination, Building hq) =
                CreateGame(isMobile: true);
            HeadquartersCommands system = CreateCommands(game);

            Assert.IsTrue(system.TryRelocate(hq, destination));
            Assert.IsNotNull(hq.Movement);
            Assert.IsFalse(origin.IsHeadquarters);
            Assert.IsNull(faction.HQInstanceID);
        }

        [Test]
        public void TryRelocate_FixedHeadquarters_IsRejected()
        {
            (GameRoot game, Faction faction, Planet origin, Planet destination, Building hq) =
                CreateGame(isMobile: false);

            Assert.IsFalse(CreateCommands(game).TryRelocate(hq, destination));
            Assert.AreSame(origin, hq.GetParent());
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
            GameRoot game = TestGame.Create(TestConfig.Create());
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

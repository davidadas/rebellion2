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
        /// <summary>Verifies that departure clears the mobile headquarters marker.</summary>
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

        /// <summary>Verifies that a fixed headquarters remains at its origin.</summary>
        [Test]
        public void TryRelocate_FixedHeadquarters_IsRejected()
        {
            (GameRoot game, Faction faction, Planet origin, Planet destination, Building hq) =
                CreateGame(isMobile: false);

            Assert.IsFalse(CreateCommands(game).TryRelocate(hq, destination));
            Assert.AreSame(origin, hq.GetParent());
            Assert.AreEqual(origin.InstanceID, faction.HQInstanceID);
        }

        /// <summary>Verifies that arrival assigns the mobile headquarters destination.</summary>
        [Test]
        public void Arrive_HeadquartersArrival_AssignsDestination()
        {
            (GameRoot game, Faction faction, Planet origin, Planet destination, Building hq) =
                CreateGame(isMobile: true);
            HeadquartersCommands system = CreateCommands(game);
            Assert.IsTrue(system.TryRelocate(hq, destination));

            system.Arrive(hq, destination);

            Assert.IsTrue(destination.IsHeadquarters);
            Assert.AreEqual(destination.InstanceID, faction.HQInstanceID);
        }

        /// <summary>Verifies that capture clears a fixed headquarters marker without losing its location.</summary>
        [Test]
        public void UpdateOwnership_FixedHeadquartersCaptured_ClearsMarkerAndPreservesLocation()
        {
            (GameRoot game, Faction faction, Planet origin, _, _) = CreateGame(isMobile: false);
            Faction attacker = new Faction { InstanceID = "empire" };
            game.GetFactions().Add(attacker);

            List<GameResult> results = CreateCommands(game)
                .UpdateOwnership(origin, faction, attacker);

            Assert.IsFalse(origin.IsHeadquarters);
            Assert.AreEqual(origin.InstanceID, faction.HQInstanceID);
            HeadquartersCapturedResult captured = results[0] as HeadquartersCapturedResult;
            Assert.IsNotNull(captured);
            Assert.AreSame(origin, captured.Planet);
            Assert.AreSame(faction, captured.Defender);
            Assert.AreSame(attacker, captured.Attacker);
        }

        /// <summary>Verifies that recapture restores the fixed headquarters marker.</summary>
        [Test]
        public void UpdateOwnership_FixedHeadquartersRecaptured_RestoresMarker()
        {
            (GameRoot game, Faction faction, Planet origin, _, _) = CreateGame(isMobile: false);
            Faction attacker = new Faction { InstanceID = "empire" };
            game.GetFactions().Add(attacker);
            origin.IsHeadquarters = false;

            List<GameResult> results = CreateCommands(game)
                .UpdateOwnership(origin, attacker, faction);

            Assert.IsTrue(origin.IsHeadquarters);
            Assert.AreEqual(origin.InstanceID, faction.HQInstanceID);
            Assert.IsEmpty(results);
        }

        /// <summary>Verifies that a hostile takeover destroys mobile headquarters.</summary>
        [Test]
        public void UpdateOwnership_HostilePlanetCapture_DestroysMobileHeadquarters()
        {
            (GameRoot game, Faction faction, Planet origin, _, Building hq) = CreateGame(
                isMobile: true
            );
            Faction attacker = new Faction { InstanceID = "empire" };
            game.GetFactions().Add(attacker);

            List<GameResult> results = CreateCommands(game)
                .UpdateOwnership(origin, faction, attacker);

            Assert.IsNull(game.GetSceneNodeByInstanceID<Building>(hq.InstanceID));
            Assert.IsFalse(origin.IsHeadquarters);
            Assert.IsNull(faction.HQInstanceID);
            HeadquartersDestroyedResult destroyed = results[0] as HeadquartersDestroyedResult;
            Assert.IsNotNull(destroyed);
            Assert.AreSame(hq, destroyed.Headquarters);
            Assert.AreSame(faction, destroyed.Defender);
            Assert.AreSame(attacker, destroyed.Attacker);
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
            return new HeadquartersCommands(
                game,
                movement,
                new HeadquartersQueries(game),
                movementQueries
            );
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

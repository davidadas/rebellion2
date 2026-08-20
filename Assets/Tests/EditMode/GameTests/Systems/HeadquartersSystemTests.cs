using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Systems;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class HeadquartersSystemTests
    {
        [Test]
        public void TryRelocate_MobileHeadquarters_DepartsAndClearsPlanetMarker()
        {
            (GameRoot game, Faction faction, Planet origin, Planet destination, Building hq) =
                CreateGame(isMobile: true);
            HeadquartersSystem system = CreateSystem(game);

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

            Assert.IsFalse(CreateSystem(game).TryRelocate(hq, destination));
            Assert.AreSame(origin, hq.GetParent());
            Assert.AreEqual(origin.InstanceID, faction.HQInstanceID);
        }

        [Test]
        public void HandleResults_HeadquartersArrival_AssignsDestination()
        {
            (GameRoot game, Faction faction, Planet origin, Planet destination, Building hq) =
                CreateGame(isMobile: true);
            HeadquartersSystem system = CreateSystem(game);
            Assert.IsTrue(system.TryRelocate(hq, destination));

            system.HandleResults(
                new List<UnitArrivedResult>
                {
                    new UnitArrivedResult { Unit = hq, Destination = destination },
                }
            );

            Assert.IsTrue(destination.IsHeadquarters);
            Assert.AreEqual(destination.InstanceID, faction.HQInstanceID);
        }

        [Test]
        public void HandleResults_FixedHeadquartersCaptured_ClearsMarkerAndPreservesLocation()
        {
            (GameRoot game, Faction faction, Planet origin, _, _) = CreateGame(isMobile: false);
            Faction attacker = new Faction { InstanceID = "empire" };
            game.Factions.Add(attacker);

            List<GameResult> results = CreateSystem(game)
                .HandleResults(
                    new List<PlanetOwnershipChangedResult>
                    {
                        new PlanetOwnershipChangedResult
                        {
                            Planet = origin,
                            PreviousOwner = faction,
                            NewOwner = attacker,
                        },
                    }
                );

            Assert.IsFalse(origin.IsHeadquarters);
            Assert.AreEqual(origin.InstanceID, faction.HQInstanceID);
            HeadquartersCapturedResult captured = results[0] as HeadquartersCapturedResult;
            Assert.IsNotNull(captured);
            Assert.AreSame(origin, captured.Planet);
            Assert.AreSame(faction, captured.Defender);
            Assert.AreSame(attacker, captured.Attacker);
        }

        [Test]
        public void HandleResults_FixedHeadquartersRecaptured_RestoresMarker()
        {
            (GameRoot game, Faction faction, Planet origin, _, _) = CreateGame(isMobile: false);
            Faction attacker = new Faction { InstanceID = "empire" };
            game.Factions.Add(attacker);
            origin.IsHeadquarters = false;

            List<GameResult> results = CreateSystem(game)
                .HandleResults(
                    new List<PlanetOwnershipChangedResult>
                    {
                        new PlanetOwnershipChangedResult
                        {
                            Planet = origin,
                            PreviousOwner = attacker,
                            NewOwner = faction,
                        },
                    }
                );

            Assert.IsTrue(origin.IsHeadquarters);
            Assert.AreEqual(origin.InstanceID, faction.HQInstanceID);
            Assert.IsEmpty(results);
        }

        [Test]
        public void HandleResults_HostilePlanetCapture_DestroysMobileHeadquarters()
        {
            (GameRoot game, Faction faction, Planet origin, _, Building hq) = CreateGame(
                isMobile: true
            );
            Faction attacker = new Faction { InstanceID = "empire" };
            game.Factions.Add(attacker);

            List<GameResult> results = CreateSystem(game)
                .HandleResults(
                    new List<PlanetOwnershipChangedResult>
                    {
                        new PlanetOwnershipChangedResult
                        {
                            Planet = origin,
                            PreviousOwner = faction,
                            NewOwner = attacker,
                        },
                    }
                );

            Assert.IsNull(game.GetSceneNodeByInstanceID<Building>(hq.InstanceID));
            Assert.IsFalse(origin.IsHeadquarters);
            Assert.IsNull(faction.HQInstanceID);
            HeadquartersDestroyedResult destroyed = results[0] as HeadquartersDestroyedResult;
            Assert.IsNotNull(destroyed);
            Assert.AreSame(hq, destroyed.Headquarters);
            Assert.AreSame(faction, destroyed.Defender);
            Assert.AreSame(attacker, destroyed.Attacker);
        }

        private static HeadquartersSystem CreateSystem(GameRoot game)
        {
            MovementSystem movement = new MovementSystem(
                game,
                new FogOfWarSystem(game),
                new FleetSystem(game)
            );
            return new HeadquartersSystem(game, movement);
        }

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
            game.Factions.Add(faction);

            PlanetSystem planetSystem = new PlanetSystem { InstanceID = "system" };
            game.AttachNode(planetSystem, game.GetGalaxyMap());
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
            game.AttachNode(origin, planetSystem);
            game.AttachNode(destination, planetSystem);

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

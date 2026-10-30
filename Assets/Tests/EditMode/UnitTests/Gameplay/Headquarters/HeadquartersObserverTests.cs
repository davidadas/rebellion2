using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public class HeadquartersObserverTests
    {
        [Test]
        public void HandleResults_NullArrivals_ReturnsNoResults()
        {
            (GameRoot game, _, _, _, _) = CreateGame(isMobile: true);
            HeadquartersObserver observer = new HeadquartersObserver(game);

            Assert.IsEmpty(observer.HandleResults((IReadOnlyList<UnitArrivedResult>)null));
        }

        [Test]
        public void HandleResults_NullOwnershipChanges_ReturnsNoResults()
        {
            (GameRoot game, _, _, _, _) = CreateGame(isMobile: true);
            HeadquartersObserver observer = new HeadquartersObserver(game);

            Assert.IsEmpty(
                observer.HandleResults((IReadOnlyList<PlanetOwnershipChangedResult>)null)
            );
        }

        [Test]
        public void HandleResults_MultipleArrivals_AppliesInBatchOrder()
        {
            (GameRoot game, Faction faction, Planet origin, Planet destination, Building hq) =
                CreateGame(isMobile: true);
            HeadquartersObserver observer = new HeadquartersObserver(game);

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

        [Test]
        public void HandleResults_RepeatedOwnershipChanges_ReturnsOrderedCaptureResults()
        {
            (GameRoot game, Faction faction, Planet origin, _, _) = CreateGame(isMobile: false);
            Faction firstAttacker = new Faction { InstanceID = "first" };
            Faction secondAttacker = new Faction { InstanceID = "second" };
            game.GetFactions().Add(firstAttacker);
            game.GetFactions().Add(secondAttacker);
            game.CurrentTick = 42;
            HeadquartersObserver observer = new HeadquartersObserver(game);

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

        [Test]
        public void HandleResults_HostilePlanetCapture_DestroysMobileHeadquarters()
        {
            (GameRoot game, Faction faction, Planet origin, _, Building headquarters) = CreateGame(
                isMobile: true
            );
            Faction attacker = new Faction { InstanceID = "empire" };
            game.GetFactions().Add(attacker);

            List<GameResult> results = new HeadquartersObserver(game).HandleResults(
                new[]
                {
                    new PlanetOwnershipChangedResult
                    {
                        Planet = origin,
                        PreviousOwner = faction,
                        NewOwner = attacker,
                        Tick = 12,
                    },
                }
            );

            Assert.IsNull(game.GetSceneNodeByInstanceID<Building>(headquarters.InstanceID));
            Assert.IsFalse(origin.IsHeadquarters);
            Assert.IsNull(faction.HQInstanceID);
            HeadquartersDestroyedResult destroyed = results
                .OfType<HeadquartersDestroyedResult>()
                .Single();
            Assert.AreSame(headquarters, destroyed.Headquarters);
            Assert.AreSame(faction, destroyed.Defender);
            Assert.AreSame(attacker, destroyed.Attacker);
        }

        [Test]
        public void HandleResults_OtherFactionHeadquartersDestroyed_PreservesOwnHeadquarters()
        {
            (GameRoot game, Faction first, Planet firstPlanet, _, Building firstHeadquarters) =
                CreateGame(isMobile: true);
            Faction second = new Faction
            {
                InstanceID = "second",
                HQInstanceID = "second-planet",
                Settings = new FactionSettings
                {
                    Headquarters = new HeadquartersSettings
                    {
                        FacilityTypeID = firstHeadquarters.TypeID,
                        IsMobile = true,
                    },
                },
            };
            game.GetFactions().Add(second);
            Planet secondPlanet = new Planet
            {
                InstanceID = second.HQInstanceID,
                OwnerInstanceID = second.InstanceID,
                IsColonized = true,
                IsHeadquarters = true,
                EnergyCapacity = 1,
            };
            game.AttachNode(secondPlanet, firstPlanet.GetParent());
            Building secondHeadquarters = new Building
            {
                InstanceID = "second-headquarters",
                TypeID = firstHeadquarters.TypeID,
                OwnerInstanceID = second.InstanceID,
                BuildingType = BuildingType.Headquarters,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(secondHeadquarters, secondPlanet);
            second.HeadquartersUnitInstanceID = secondHeadquarters.InstanceID;
            HeadquartersObserver observer = new HeadquartersObserver(game);

            game.ChangeOwnership(firstHeadquarters, second.InstanceID);
            observer.HandleResults(
                new[]
                {
                    new UnitOwnershipChangedResult
                    {
                        Unit = firstHeadquarters,
                        PreviousOwner = first,
                        NewOwner = second,
                    },
                }
            );
            List<GameResult> results = observer.HandleResults(
                new[]
                {
                    new GameObjectDestroyedResult
                    {
                        DestroyedObject = firstHeadquarters,
                        Context = firstPlanet,
                    },
                }
            );

            Assert.IsEmpty(results);
            Assert.IsTrue(first.HasLostHeadquarters);
            Assert.IsFalse(second.HasLostHeadquarters);
            Assert.AreEqual(secondPlanet.InstanceID, second.HQInstanceID);
            Assert.IsTrue(secondPlanet.IsHeadquarters);
        }

        [Test]
        public void HandleResults_HeadquartersReturnedAfterLoss_DoesNotRestoreHeadquarters()
        {
            (GameRoot game, Faction faction, _, _, Building headquarters) = CreateGame(
                isMobile: true
            );
            Faction attacker = new Faction { InstanceID = "attacker" };
            game.GetFactions().Add(attacker);
            HeadquartersObserver observer = new HeadquartersObserver(game);
            game.ChangeOwnership(headquarters, attacker.InstanceID);
            observer.HandleResults(
                new[]
                {
                    new UnitOwnershipChangedResult
                    {
                        Unit = headquarters,
                        PreviousOwner = faction,
                        NewOwner = attacker,
                    },
                }
            );

            game.ChangeOwnership(headquarters, faction.InstanceID);
            List<GameResult> results = observer.HandleResults(
                new[]
                {
                    new UnitOwnershipChangedResult
                    {
                        Unit = headquarters,
                        PreviousOwner = attacker,
                        NewOwner = faction,
                    },
                }
            );

            Assert.IsEmpty(results);
            Assert.IsTrue(faction.HasLostHeadquarters);
            Assert.IsNull(faction.HQInstanceID);
        }

        [Test]
        public void HandleResults_HeadquartersScrapped_RecordsHeadquartersLoss()
        {
            (GameRoot game, Faction faction, Planet origin, _, Building headquarters) = CreateGame(
                isMobile: true
            );

            List<GameResult> results = new HeadquartersObserver(game).HandleResults(
                new[]
                {
                    new GameObjectScrappedResult
                    {
                        ScrappedObject = headquarters,
                        Context = origin,
                        Tick = 12,
                    },
                }
            );

            HeadquartersDestroyedResult destroyed = results
                .OfType<HeadquartersDestroyedResult>()
                .Single();
            Assert.AreSame(headquarters, destroyed.Headquarters);
            Assert.AreSame(faction, destroyed.Defender);
            Assert.IsTrue(faction.HasLostHeadquarters);
            Assert.IsNull(faction.HQInstanceID);
        }

        [Test]
        public void ReconcileState_LegacyHeadquartersInTransit_RestoresUnitIdentity()
        {
            (GameRoot game, Faction faction, _, _, Building headquarters) = CreateGame(
                isMobile: true
            );
            faction.HeadquartersUnitInstanceID = null;
            faction.HQInstanceID = null;
            headquarters.Movement = new MovementState { TransitTicks = 10, TicksElapsed = 1 };

            new HeadquartersObserver(game).ReconcileState();

            Assert.AreEqual(headquarters.InstanceID, faction.HeadquartersUnitInstanceID);
            Assert.IsFalse(faction.HasLostHeadquarters);
        }

        /// <summary>
        /// Creates a faction with headquarters and two registered planets.
        /// </summary>
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
            faction.HeadquartersUnitInstanceID = headquarters.InstanceID;
            return (game, faction, origin, destination, headquarters);
        }
    }
}

using System;
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
    public class BombardmentQueriesTests
    {
        /// <summary>
        /// Verifies can execute neutral planet with active capital ship returns true.
        /// </summary>
        [Test]
        public void CanExecute_NeutralPlanetWithActiveCapitalShip_ReturnsTrue()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", owner: null, energy: 10);
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);

            bool canExecute = new BombardmentQueries(game).CanExecute(
                new List<Fleet> { fleet },
                planet,
                BombardmentType.General
            );

            Assert.IsTrue(canExecute);
        }

        /// <summary>
        /// Verifies can execute damaged low bombardment ship returns true.
        /// </summary>
        [Test]
        public void CanExecute_DamagedLowBombardmentShip_ReturnsTrue()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            Fleet fleet = AddBombardmentFleet(
                game,
                planet,
                "alliance",
                bombardment: 1,
                currentHull: 53
            );

            bool canExecute = new BombardmentQueries(game).CanExecute(
                new List<Fleet> { fleet },
                planet,
                BombardmentType.General
            );

            Assert.IsTrue(canExecute);
        }

        /// <summary>
        /// Verifies can execute ordinary bombardment without effective strength returns false.
        /// </summary>
        /// <param name="type">The type.</param>
        [TestCase(BombardmentType.Military)]
        [TestCase(BombardmentType.Civilian)]
        [TestCase(BombardmentType.General)]
        public void CanExecute_OrdinaryBombardmentWithoutEffectiveStrength_ReturnsFalse(
            BombardmentType type
        )
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 0);

            bool canExecute = new BombardmentQueries(game).CanExecute(
                new List<Fleet> { fleet },
                planet,
                type
            );

            Assert.IsFalse(canExecute);
        }

        /// <summary>
        /// Verifies can execute embarked fighter supplies bombardment strength returns true.
        /// </summary>
        [Test]
        public void CanExecute_EmbarkedFighterSuppliesBombardmentStrength_ReturnsTrue()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "empire", energy: 10);
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 0);
            CapitalShip ship = fleet.GetChildren<CapitalShip>()[0];
            ship.StarfighterCapacity = 1;
            game.AttachNode(
                new Starfighter
                {
                    InstanceID = "fighter",
                    OwnerInstanceID = "alliance",
                    Bombardment = 1,
                    MaxSquadronSize = 1,
                    CurrentSquadronSize = 1,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                ship
            );

            bool canExecute = new BombardmentQueries(game).CanExecute(
                new List<Fleet> { fleet },
                planet,
                BombardmentType.General
            );

            Assert.IsTrue(canExecute);
        }

        /// <summary>
        /// Verifies the raw eligibility query rejects a null fleet in the selection.
        /// </summary>
        [Test]
        public void CanExecute_NullFleetAlongsideReadyFleet_ReturnsFalse()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "target", "empire", energy: 1);
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);

            bool accepted = new BombardmentQueries(game).CanExecute(
                new List<Fleet> { null, fleet },
                planet,
                BombardmentType.General
            );

            Assert.IsFalse(accepted);
        }

        /// <summary>
        /// Verifies planet destruction requires a capable ship even when ordinary bombardment is possible.
        /// </summary>
        [Test]
        public void CanExecute_NoPlanetDestroyingShip_ReturnsFalse()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "target", "empire", energy: 1);
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);

            bool accepted = new BombardmentQueries(game).CanExecute(
                new[] { fleet },
                planet,
                BombardmentType.DestroyPlanet
            );

            Assert.IsFalse(accepted);
        }

        /// <summary>
        /// Verifies eligibility inspection does not clear a planned route or enter combat.
        /// </summary>
        [Test]
        public void CanExecute_ReadyFleet_DoesNotChangeFleetState()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "target", "empire", energy: 1);
            Fleet fleet = AddBombardmentFleet(game, planet, "alliance", bombardment: 1);
            fleet.Waypoints.Add("next-planet");

            Assert.IsTrue(
                new BombardmentQueries(game).CanExecute(
                    new[] { fleet },
                    planet,
                    BombardmentType.General
                )
            );

            CollectionAssert.AreEqual(new[] { "next-planet" }, fleet.Waypoints);
            Assert.IsFalse(fleet.IsInCombat);
            Assert.AreEqual(1, planet.EnergyCapacity);
        }

        /// <summary>
        /// Creates game.
        /// </summary>
        /// <returns>The created game.</returns>
        private static GameRoot CreateGame()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.GetFactions()
                .Add(
                    new Faction
                    {
                        InstanceID = "empire",
                        Settings = new FactionSettings
                        {
                            InvertSupportShift = true,
                            SupportResistance = SupportChange.Decrease,
                            CivilianBombardmentCoreSupportPenalty = -3,
                            CivilianBombardmentOuterRimSupportPenalty = -1,
                            Headquarters = new HeadquartersSettings { IsBombardable = false },
                        },
                    }
                );
            game.GetFactions()
                .Add(
                    new Faction
                    {
                        InstanceID = "alliance",
                        Settings = new FactionSettings
                        {
                            InvertSupportShift = false,
                            SupportResistance = SupportChange.Increase,
                            CivilianBombardmentCoreSupportPenalty = -4,
                            CivilianBombardmentOuterRimSupportPenalty = -2,
                            Headquarters = new HeadquartersSettings { IsBombardable = true },
                        },
                    }
                );
            return game;
        }

        /// <summary>
        /// Creates planet.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="id">The id.</param>
        /// <param name="owner">The owner.</param>
        /// <param name="energy">The energy.</param>
        /// <returns>The created planet.</returns>
        private static (Planet planet, PlanetSector planetSector) CreatePlanet(
            GameRoot game,
            string id,
            string owner = null,
            int energy = 5
        )
        {
            PlanetSector planetSector = new PlanetSector { InstanceID = $"sector_{id}" };
            game.AttachNode(planetSector, game.Galaxy);
            Planet planet = new Planet
            {
                InstanceID = id,
                OwnerInstanceID = owner,
                IsColonized = true,
                EnergyCapacity = energy,
                PopularSupport = new Dictionary<string, int>
                {
                    { "empire", 50 },
                    { "alliance", 50 },
                },
            };
            game.AttachNode(planet, planetSector);
            return (planet, planetSector);
        }

        /// <summary>
        /// Adds bombardment fleet.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
        /// <param name="ownerId">The owner id.</param>
        /// <param name="bombardment">The bombardment.</param>
        /// <param name="currentHull">The current hull.</param>
        /// <param name="typeId">The type id.</param>
        /// <returns>The result of add bombardment fleet.</returns>
        private static Fleet AddBombardmentFleet(
            GameRoot game,
            Planet planet,
            string ownerId,
            int bombardment,
            int currentHull = 100,
            string typeId = null
        )
        {
            Fleet fleet = new Fleet
            {
                InstanceID = Guid.NewGuid().ToString(),
                OwnerInstanceID = ownerId,
            };
            game.AttachNode(fleet, planet);
            CapitalShip ship = new CapitalShip
            {
                InstanceID = Guid.NewGuid().ToString(),
                TypeID = typeId,
                OwnerInstanceID = ownerId,
                Bombardment = bombardment,
                MaxHullStrength = 100,
                CurrentHullStrength = currentHull,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(ship, fleet);
            return fleet;
        }
    }
}

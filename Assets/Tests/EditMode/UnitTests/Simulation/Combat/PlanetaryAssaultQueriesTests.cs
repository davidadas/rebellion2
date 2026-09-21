using System;
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
    public sealed class PlanetaryAssaultQueriesTests
    {
        /// <summary>Verifies that assault eligibility requires an active game.</summary>
        [Test]
        public void Constructor_NullGame_ThrowsArgumentNullException()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
                new PlanetaryAssaultQueries(null)
            );

            Assert.AreEqual("game", exception.ParamName);
        }

        /// <summary>Verifies that direct eligibility rejects a fleet collection containing null.</summary>
        [Test]
        public void CanExecute_NullFleetAlongsideReadyFleet_ReturnsFalse()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance");
            Fleet fleet = AddAssaultFleet(game, planet, "empire", regimentCount: 1);

            Assert.IsFalse(
                new PlanetaryAssaultQueries(game).CanExecute(new[] { null, fleet }, planet)
            );
        }

        /// <summary>Verifies can execute two ready and six moving regiments uses ready regiments.</summary>
        [Test]
        public void CanExecute_TwoReadyAndSixMovingRegiments_UsesReadyRegiments()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 10);
            Fleet fleet = AddAssaultFleet(game, planet, "empire", regimentCount: 8);
            foreach (
                Regiment regiment in fleet
                    .GetChildren<CapitalShip>()[0]
                    .GetChildren<Regiment>()
                    .Skip(2)
            )
                regiment.Movement = new MovementState();
            PlanetaryAssaultQueries system = new PlanetaryAssaultQueries(game);

            Assert.IsTrue(system.CanExecute(new List<Fleet> { fleet }, planet));
        }

        /// <summary>Verifies can execute shielded target or no ready regiments returns false.</summary>
        [Test]
        public void CanExecute_ShieldedTargetOrNoReadyRegiments_ReturnsFalse()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", "alliance", energy: 10);
            AddDefenseBuilding(game, planet, "shield1", shieldStrength: 1);
            AddDefenseBuilding(game, planet, "shield2", shieldStrength: 1);
            Fleet fleet = AddAssaultFleet(game, planet, "empire", regimentCount: 1);
            PlanetaryAssaultQueries system = new PlanetaryAssaultQueries(game);

            bool shielded = system.CanExecute(new List<Fleet> { fleet }, planet);
            foreach (Building building in planet.GetAllBuildings())
                building.ManufacturingStatus = ManufacturingStatus.Building;
            fleet.GetChildren<CapitalShip>()[0].GetChildren<Regiment>()[0].Movement =
                new MovementState();
            bool noReadyRegiments = system.CanExecute(new List<Fleet> { fleet }, planet);

            Assert.IsFalse(shielded);
            Assert.IsFalse(noReadyRegiments);
        }

        /// <summary>Verifies can execute neutral planet with ready regiment returns true.</summary>
        [Test]
        public void CanExecute_NeutralPlanetWithReadyRegiment_ReturnsTrue()
        {
            GameRoot game = CreateGame();
            (Planet planet, _) = CreatePlanet(game, "p1", owner: null, energy: 10);
            Fleet fleet = AddAssaultFleet(game, planet, "empire", regimentCount: 1);

            bool canExecute = new PlanetaryAssaultQueries(game).CanExecute(
                new List<Fleet> { fleet },
                planet
            );

            Assert.IsTrue(canExecute);
        }

        /// <summary>
        /// Adds assault fleet.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
        /// <param name="ownerId">The owner id.</param>
        /// <param name="regimentCount">The regiment count.</param>
        /// <returns>The result of add assault fleet.</returns>
        private static Fleet AddAssaultFleet(
            GameRoot game,
            Planet planet,
            string ownerId,
            int regimentCount
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
                OwnerInstanceID = ownerId,
                MaxHullStrength = 100,
                CurrentHullStrength = 100,
                RegimentCapacity = regimentCount,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(ship, fleet);

            for (int index = 0; index < regimentCount; index++)
            {
                game.AttachNode(
                    new Regiment
                    {
                        InstanceID = Guid.NewGuid().ToString(),
                        OwnerInstanceID = ownerId,
                        ManufacturingStatus = ManufacturingStatus.Complete,
                    },
                    ship
                );
            }

            return fleet;
        }

        /// <summary>
        /// Adds defense building.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="shieldStrength">The shield strength.</param>
        /// <param name="weaponEffect">The weapon effect.</param>
        /// <param name="protectedUnitTypeId">The protected unit type id.</param>
        /// <returns>The result of add defense building.</returns>
        private static Building AddDefenseBuilding(
            GameRoot game,
            Planet planet,
            string instanceId,
            int shieldStrength = 0,
            DefenseWeaponEffect weaponEffect = DefenseWeaponEffect.HullDamage,
            string protectedUnitTypeId = null
        )
        {
            Building building = new Building
            {
                InstanceID = instanceId,
                OwnerInstanceID = planet.GetOwnerInstanceID(),
                BuildingType =
                    shieldStrength > 0 || protectedUnitTypeId != null
                        ? BuildingType.Defense
                        : BuildingType.Weapon,
                ShieldStrength = shieldStrength,
                DefenseWeaponEffect = weaponEffect,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            if (protectedUnitTypeId != null)
                building.ProtectedUnitTypeIDs.Add(protectedUnitTypeId);
            game.AttachNode(building, planet);
            return building;
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
    }
}

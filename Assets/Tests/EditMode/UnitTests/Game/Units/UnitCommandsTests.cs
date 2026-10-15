using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Units
{
    [TestFixture]
    public sealed class UnitCommandsTests
    {
        private const string _ownerId = "owner";

        private GameRoot _game;
        private Planet _planet;
        private UnitCommands _commands;

        /// <summary>
        /// Creates a registered planet and unit commands for each test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _game = TestGame.Create(TestConfig.Create());
            _game.GetFactions().Add(new Faction { InstanceID = _ownerId });
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            _planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = _ownerId,
                EnergyCapacity = 2,
                IsColonized = true,
            };
            _game.AttachNode(sector, _game.Galaxy);
            _game.AttachNode(_planet, sector);
            _commands = new UnitCommands(_game);
        }

        [Test]
        public void Destroy_OrdinaryBuilding_PreservesRegisteredRubble()
        {
            Building building = CreateBuilding("mine", BuildingType.Mine);

            _commands.Destroy(building);

            Assert.AreSame(_planet, building.GetParent());
            Assert.AreSame(building, _game.GetSceneNodeByInstanceID<Building>(building.InstanceID));
            Assert.IsTrue(building.IsDestroyed);
        }

        [Test]
        public void Destroy_QueuedBuilding_RemovesProducerQueueReference()
        {
            Building building = CreateBuilding("mine", BuildingType.Mine);
            building.ManufacturingStatus = ManufacturingStatus.Delivering;
            building.ProducerPlanetID = _planet.InstanceID;
            _planet.AddToManufacturingQueue(building);

            _commands.Destroy(building);

            Assert.IsFalse(_planet.GetManufacturingQueue().ContainsKey(ManufacturingType.Building));
            Assert.IsTrue(building.IsDestroyed);
        }

        [Test]
        public void Destroy_UpgradingBuilding_RemovesQueueAndPreservesSourceRubble()
        {
            Building building = CreateBuilding("shipyard", BuildingType.Shipyard);
            building.TypeID = "basic-shipyard";
            building.Upgrades.Add("advanced-shipyard");
            building.BeginUpgrade(
                new Building { TypeID = "advanced-shipyard", BuildingType = BuildingType.Shipyard }
            );
            building.ProducerPlanetID = _planet.InstanceID;
            _planet.AddToManufacturingQueue(building);

            _commands.Destroy(building);

            Assert.IsFalse(_planet.GetManufacturingQueue().ContainsKey(ManufacturingType.Building));
            Assert.AreEqual("basic-shipyard", building.TypeID);
            Assert.IsTrue(building.IsRubble());
        }

        [Test]
        public void Destroy_FacilityWithMaterialRequests_ClearsRequests()
        {
            Building building = CreateBuilding("mine", BuildingType.Mine);
            Faction faction = _game.GetFactionByOwnerInstanceID(_ownerId);
            faction.PendingRawMaterialFacilityIDs.Add(building.InstanceID);
            faction.PendingRefinedMaterialFacilityIDs.Add(building.InstanceID);

            _commands.Destroy(building);

            CollectionAssert.DoesNotContain(
                faction.PendingRawMaterialFacilityIDs,
                building.InstanceID
            );
            CollectionAssert.DoesNotContain(
                faction.PendingRefinedMaterialFacilityIDs,
                building.InstanceID
            );
        }

        [Test]
        public void Destroy_Headquarters_RemovesBuildingPermanently()
        {
            Building headquarters = CreateBuilding("headquarters", BuildingType.Headquarters);
            _planet.IsHeadquarters = true;
            _game.GetFactionByOwnerInstanceID(_ownerId).HQInstanceID = _planet.InstanceID;

            _commands.Destroy(headquarters);

            Assert.IsNull(headquarters.GetParent());
            Assert.IsNull(_game.GetSceneNodeByInstanceID<Building>(headquarters.InstanceID));
            Assert.IsFalse(_planet.IsHeadquarters);
            Assert.IsNull(_game.GetFactionByOwnerInstanceID(_ownerId).HQInstanceID);
        }

        [Test]
        public void RemovePermanently_BuildingWithMaterialRequests_ClearsRequests()
        {
            Building building = CreateBuilding("mine", BuildingType.Mine);
            Faction faction = _game.GetFactionByOwnerInstanceID(_ownerId);
            faction.PendingRawMaterialFacilityIDs.Add(building.InstanceID);
            faction.PendingRefinedMaterialFacilityIDs.Add(building.InstanceID);

            _commands.RemovePermanently(building);

            Assert.IsNull(_game.GetSceneNodeByInstanceID<Building>(building.InstanceID));
            CollectionAssert.DoesNotContain(
                faction.PendingRawMaterialFacilityIDs,
                building.InstanceID
            );
            CollectionAssert.DoesNotContain(
                faction.PendingRefinedMaterialFacilityIDs,
                building.InstanceID
            );
        }

        /// <summary>
        /// Creates and registers a completed building on the test planet.
        /// </summary>
        /// <param name="instanceId">The building instance identifier.</param>
        /// <param name="buildingType">The building role.</param>
        /// <returns>The registered building.</returns>
        private Building CreateBuilding(string instanceId, BuildingType buildingType)
        {
            Building building = new Building
            {
                InstanceID = instanceId,
                OwnerInstanceID = _ownerId,
                BuildingType = buildingType,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(building, _planet);
            return building;
        }
    }
}

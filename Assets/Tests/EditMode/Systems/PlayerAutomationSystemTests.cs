using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Systems;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class PlayerAutomationSystemTests
    {
        private GameRoot _game;
        private Faction _faction;
        private Planet _producer;
        private Planet _destination;
        private PlayerAutomationSystem _automation;

        [SetUp]
        public void SetUp()
        {
            _game = new GameRoot(TestContent.Data.GameConfig);
            _faction = new Faction
            {
                InstanceID = "FNALL1",
                ManageGarrisons = true,
                ManageProduction = true,
            };
            _game.Factions.Add(_faction);

            PlanetSystem system = new PlanetSystem { InstanceID = "SYSTEM" };
            _game.AttachNode(system, _game.Galaxy);
            _producer = CreatePlanet("PRODUCER", 100, 10);
            _destination = CreatePlanet("DESTINATION", 10, 10);
            _game.AttachNode(_producer, system);
            _game.AttachNode(_destination, system);

            AddProductionFacility(_producer, "TRAINING", ManufacturingType.Troop);
            AddProductionFacility(_producer, "CONSTRUCTION", ManufacturingType.Building);
            AddProductionFacility(_producer, "CONSTRUCTION_2", ManufacturingType.Building);
            AddResourcePairs(_producer, 10);

            ManufacturingSystem manufacturing = new ManufacturingSystem(
                _game,
                new FleetSystem(_game)
            );
            _automation = new PlayerAutomationSystem(_game, TestContent.Data, manufacturing);
        }

        [Test]
        public void ProcessTick_ManageGarrisons_QueuesTroopForUnguardedPlanet()
        {
            _faction.ManageProduction = false;
            _destination.SetFullPopularSupport(_faction.InstanceID);
            AddCompletedRegiment(_producer, "GARRISON_1");
            AddCompletedRegiment(_producer, "GARRISON_2");

            _automation.ProcessTick();

            Assert.AreEqual(1, _destination.GetAllRegiments().Count);
            Assert.AreEqual(
                ManufacturingStatus.Building,
                _destination.GetAllRegiments().Single().ManufacturingStatus
            );
            Assert.AreEqual("REAL002", _destination.GetAllRegiments().Single().TypeID);
        }

        [Test]
        public void ProcessTick_ManageProduction_FillsCapacityWithMatchedPair()
        {
            _faction.ManageGarrisons = false;

            _automation.ProcessTick();

            Assert.AreEqual(11, CountResourceFacilities(BuildingType.Mine));
            Assert.AreEqual(11, CountResourceFacilities(BuildingType.Refinery));
        }

        [Test]
        public void ProcessTick_ManageProductionWithoutMineCapacity_DoesNotAddRefinery()
        {
            _faction.ManageGarrisons = false;
            _destination.NumRawResourceNodes = 0;
            int refineryCount = CountResourceFacilities(BuildingType.Refinery);

            _automation.ProcessTick();

            Assert.AreEqual(refineryCount, CountResourceFacilities(BuildingType.Refinery));
        }

        [Test]
        public void ProcessTick_ManageProduction_UsesClosestAvailableResourceSlot()
        {
            _faction.ManageGarrisons = false;
            _destination.PositionX = 1;
            Planet distant = CreatePlanet("DISTANT", 50, 50);
            distant.PositionX = 100;
            _game.AttachNode(distant, _destination.GetParent());

            _automation.ProcessTick();

            Assert.AreEqual(1, _destination.GetTotalBuildingTypeCount(BuildingType.Mine));
            Assert.AreEqual(0, distant.GetTotalBuildingTypeCount(BuildingType.Mine));
        }

        [Test]
        public void ProcessTick_ManageGarrisons_PrioritizesUprising()
        {
            _faction.ManageProduction = false;
            AddCompletedRegiment(_producer, "GARRISON_1");
            AddCompletedRegiment(_producer, "GARRISON_2");
            Planet uprising = CreatePlanet("UPRISING", 10, 0);
            uprising.IsInUprising = true;
            _game.AttachNode(uprising, _destination.GetParent());

            _automation.ProcessTick();

            Assert.AreEqual(1, uprising.GetAllRegiments().Count);
            Assert.IsEmpty(_destination.GetAllRegiments());
        }

        [Test]
        public void ProcessTick_DisabledAutomation_DoesNotQueueWork()
        {
            _faction.ManageGarrisons = false;
            _faction.ManageProduction = false;

            _automation.ProcessTick();

            Assert.IsEmpty(_destination.GetAllRegiments());
            Assert.AreEqual(0, _destination.GetTotalBuildingTypeCount(BuildingType.Mine));
        }

        private Planet CreatePlanet(string instanceId, int energy, int resources)
        {
            Planet planet = new Planet
            {
                InstanceID = instanceId,
                OwnerInstanceID = _faction.InstanceID,
                IsColonized = true,
                EnergyCapacity = energy,
                NumRawResourceNodes = resources,
            };
            planet.SetFullPopularSupport(_faction.InstanceID);
            return planet;
        }

        private void AddProductionFacility(Planet planet, string instanceId, ManufacturingType type)
        {
            _game.AttachNode(
                new Building
                {
                    InstanceID = instanceId,
                    OwnerInstanceID = _faction.InstanceID,
                    BuildingType =
                        type == ManufacturingType.Troop
                            ? BuildingType.TrainingFacility
                            : BuildingType.ConstructionFacility,
                    ProductionType = type,
                    ProcessRate = 1,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );
        }

        private void AddResourcePairs(Planet planet, int count)
        {
            for (int index = 0; index < count; index++)
            {
                AddResourceFacility(planet, $"MINE_{index}", BuildingType.Mine);
                AddResourceFacility(planet, $"REFINERY_{index}", BuildingType.Refinery);
            }
        }

        private void AddCompletedRegiment(Planet planet, string instanceId)
        {
            _game.AttachNode(
                new Regiment
                {
                    InstanceID = instanceId,
                    OwnerInstanceID = _faction.InstanceID,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );
        }

        private void AddResourceFacility(
            Planet planet,
            string instanceId,
            BuildingType buildingType
        )
        {
            _game.AttachNode(
                new Building
                {
                    InstanceID = instanceId,
                    OwnerInstanceID = _faction.InstanceID,
                    BuildingType = buildingType,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );
        }

        private int CountResourceFacilities(BuildingType buildingType)
        {
            return new[] { _producer, _destination }.Sum(planet =>
                planet.GetTotalBuildingTypeCount(buildingType)
            );
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Systems;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class ResourceIncomeSystemTests
    {
        private GameRoot _game;
        private ResourceIncomeSystem _system;
        private Faction _empire;
        private Planet _planet;

        [SetUp]
        public void SetUp()
        {
            GameConfig config = ResourceManager.GetConfig<GameConfig>();
            _game = new GameRoot(config);

            _empire = new Faction { InstanceID = "EMPIRE" };
            _game.Factions.Add(_empire);
            _game.Factions.Add(new Faction { InstanceID = "rebels" });

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "SYSTEM1",
                PositionX = 0,
                PositionY = 0,
            };
            _game.AttachNode(system, _game.Galaxy);

            _planet = new Planet
            {
                InstanceID = "CORUSCANT",
                OwnerInstanceID = "EMPIRE",
                PositionX = 0,
                PositionY = 0,
                IsColonized = true,
                EnergyCapacity = 10,
                NumRawResourceNodes = 5,
                PopularSupport = new Dictionary<string, int> { { "EMPIRE", 100 } },
            };
            _game.AttachNode(_planet, system);

            _system = new ResourceIncomeSystem(_game);
        }

        private void AddCompleteBuilding(Planet planet, BuildingType type)
        {
            Building building = new Building
            {
                OwnerInstanceID = planet.GetOwnerInstanceID(),
                BuildingType = type,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(building, planet);
        }

        [Test]
        public void ProcessTick_OwnedPlanetWithProduction_AccumulatesStockpile()
        {
            AddCompleteBuilding(_planet, BuildingType.Mine);
            AddCompleteBuilding(_planet, BuildingType.Refinery);

            _system.ProcessTick();

            Assert.Greater(_empire.RawMaterialStockpile, 0);
            Assert.Greater(_empire.RefinedMaterialStockpile, 0);
        }

        [Test]
        public void ProcessTick_FactionWithUnitMaintenance_DeductsFromRefinedStockpile()
        {
            AddCompleteBuilding(_planet, BuildingType.Mine);
            AddCompleteBuilding(_planet, BuildingType.Refinery);
            Regiment unit = new Regiment
            {
                OwnerInstanceID = "EMPIRE",
                MaintenanceCost = 5,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(unit, _planet);

            _system.ProcessTick();
            int afterOneTick = _empire.RefinedMaterialStockpile;
            _empire.RefinedMaterialStockpile = 0;
            _empire.RawMaterialStockpile = 0;

            _system.ProcessTick();
            int afterSecondTick = _empire.RefinedMaterialStockpile;

            Assert.AreEqual(afterOneTick, afterSecondTick);
            // Sanity: per-tick refined income minus 5 maintenance is what should accumulate.
            int multiplier = _game.GetConfig().Production.RefinementMultiplier;
            Assert.AreEqual(1 * multiplier - 5, afterSecondTick);
        }

        [Test]
        public void ProcessTick_PlanetWithFullSupport_ProducesFullIncome()
        {
            AddCompleteBuilding(_planet, BuildingType.Mine);
            _planet.PopularSupport["EMPIRE"] = 100;

            _system.ProcessTick();

            Assert.AreEqual(1, _empire.RawMaterialStockpile);
        }

        [Test]
        public void ProcessTick_PlanetWithZeroSupport_ProducesNoIncome()
        {
            AddCompleteBuilding(_planet, BuildingType.Mine);
            _planet.PopularSupport["EMPIRE"] = 0;

            _system.ProcessTick();

            Assert.AreEqual(0, _empire.RawMaterialStockpile);
            Assert.AreEqual(0, _empire.RefinedMaterialStockpile);
        }

        [Test]
        public void ProcessTick_BlockadedPlanetWithoutKDY_AppliesBlockadeModifier()
        {
            AddCompleteBuilding(_planet, BuildingType.Mine);
            AddCompleteBuilding(_planet, BuildingType.Refinery);
            Fleet hostileFleet = new Fleet { OwnerInstanceID = "rebels" };
            _game.AttachNode(hostileFleet, _planet);
            CapitalShip hostileShip = new CapitalShip
            {
                OwnerInstanceID = "rebels",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(hostileShip, hostileFleet);

            _system.ProcessTick();

            int multiplier = _game.GetConfig().Production.RefinementMultiplier;
            int penaltyPerShip = _game.GetConfig().Production.BlockadeCapitalShipPenalty;
            int expectedModifier = 100 - penaltyPerShip;
            Assert.AreEqual(1 * expectedModifier / 100, _empire.RawMaterialStockpile);
            Assert.AreEqual(
                1 * expectedModifier / 100 * multiplier,
                _empire.RefinedMaterialStockpile
            );
        }

        [Test]
        public void ProcessTick_BlockadedPlanetWithKDY_IgnoresBlockade()
        {
            AddCompleteBuilding(_planet, BuildingType.Mine);
            AddCompleteBuilding(_planet, BuildingType.Refinery);
            Building kdy = new Building
            {
                OwnerInstanceID = "EMPIRE",
                DefenseFacilityClass = DefenseFacilityClass.KDY,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(kdy, _planet);
            Fleet hostileFleet = new Fleet { OwnerInstanceID = "rebels" };
            _game.AttachNode(hostileFleet, _planet);
            CapitalShip hostileShip = new CapitalShip
            {
                OwnerInstanceID = "rebels",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(hostileShip, hostileFleet);

            _system.ProcessTick();

            int multiplier = _game.GetConfig().Production.RefinementMultiplier;
            Assert.AreEqual(1, _empire.RawMaterialStockpile);
            Assert.AreEqual(1 * multiplier, _empire.RefinedMaterialStockpile);
        }
    }
}

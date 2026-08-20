using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Movement;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.Systems;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class ResearchSystemTests
    {
        private GameRoot _game;
        private Faction _faction;
        private Planet _planet;
        private ResearchSystem _system;

        [SetUp]
        public void SetUp()
        {
            GameConfig config = TestConfig.Create();
            _game = new GameRoot(config);

            _faction = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
            _game.GetFactions().Add(_faction);

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            _game.AttachNode(planetSector, _game.Galaxy);

            _planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "FNALL1",
                IsColonized = true,
                EnergyCapacity = 20,
            };
            _game.AttachNode(_planet, planetSector);

            _system = new ResearchSystem(_game, new StubRNG());
        }

        [Test]
        public void ProcessTick_PulseNotReached_DoesNotAddCapacity()
        {
            Building shipyard = CreateShipyard("SY1");
            _game.AttachNode(shipyard, _planet);

            _system.ProcessTick();

            Assert.AreEqual(
                0,
                _faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign)
            );
        }

        [Test]
        public void ProcessTick_OneCoreSectorShipyard_AddsOneCapacity()
        {
            Building shipyard = CreateShipyard("SY1");
            _game.AttachNode(shipyard, _planet);

            _game.CurrentTick = 30;

            int before = _faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign);
            _system.ProcessTick();
            int after = _faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign);

            Assert.AreEqual(1, after - before, "One core-system shipyard should add 1 capacity");
        }

        [Test]
        public void ProcessTick_MultipleCoreSectorFacilities_AddsAll()
        {
            _game.AttachNode(CreateShipyard("SY1"), _planet);
            _game.AttachNode(CreateShipyard("SY2"), _planet);
            _game.AttachNode(CreateShipyard("SY3"), _planet);
            _game.CurrentTick = 30;

            _system.ProcessTick();

            Assert.AreEqual(
                3,
                _faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign)
            );
        }

        [Test]
        public void ProcessTick_BusyFacility_StillAddsCapacity()
        {
            Building shipyard = CreateShipyard("SY1");
            _game.AttachNode(shipyard, _planet);

            // Put something in the manufacturing queue to make the facility busy
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "SHIP1",
                OwnerInstanceID = "FNALL1",
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            _planet.ManufacturingQueue[ManufacturingType.Ship] = new List<IManufacturable> { ship };
            _game.CurrentTick = 30;

            _system.ProcessTick();

            Assert.AreEqual(
                1,
                _faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign),
                "Completed facilities should contribute even when production is queued"
            );
        }

        [Test]
        public void ProcessTick_FacilityUnderConstruction_DoesNotAddCapacity()
        {
            Building shipyard = CreateShipyard("SY1");
            shipyard.ManufacturingStatus = ManufacturingStatus.Building;
            _game.AttachNode(shipyard, _planet);
            _game.CurrentTick = 30;

            _system.ProcessTick();

            Assert.AreEqual(
                0,
                _faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign),
                "Facility under construction should not contribute to research capacity"
            );
        }

        [Test]
        public void ProcessTick_FacilityInTransit_DoesNotAddCapacity()
        {
            Building shipyard = CreateShipyard("SY1");
            shipyard.Movement = new MovementState();
            _game.AttachNode(shipyard, _planet);
            _game.CurrentTick = 30;

            _system.ProcessTick();

            Assert.AreEqual(
                0,
                _faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign),
                "Facility in transit should not contribute to research capacity"
            );
        }

        [Test]
        public void ProcessTick_NoFacilities_NoCapacity()
        {
            _game.CurrentTick = 30;
            _system.ProcessTick();

            Assert.AreEqual(
                0,
                _faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign)
            );
            Assert.AreEqual(
                0,
                _faction.GetResearchCapacityRemaining(ResearchDiscipline.FacilityDesign)
            );
            Assert.AreEqual(
                0,
                _faction.GetResearchCapacityRemaining(ResearchDiscipline.TroopTraining)
            );
        }

        [Test]
        public void ProcessTick_OuterRimFacility_DoesNotAddCapacity()
        {
            PlanetSector outerRimSector = new PlanetSector
            {
                InstanceID = "sector-outer",
                SectorType = PlanetSectorType.OuterRim,
            };
            _game.AttachNode(outerRimSector, _game.Galaxy);

            Planet outerRimPlanet = new Planet
            {
                InstanceID = "p-outer",
                OwnerInstanceID = "FNALL1",
                IsColonized = true,
                EnergyCapacity = 20,
            };
            _game.AttachNode(outerRimPlanet, outerRimSector);
            _game.AttachNode(CreateShipyard("SY-OUTER"), outerRimPlanet);
            _game.CurrentTick = 30;

            _system.ProcessTick();

            Assert.AreEqual(
                0,
                _faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign)
            );
        }

        [Test]
        public void ProcessTick_CoreSectorFacilityAcrossMultiplePulses_AccumulatesCapacity()
        {
            SetupShipResearchCatalog(("Dreadnaught", 0, 0), ("Frigate", 1, 12));
            _game.AttachNode(CreateShipyard("SY1"), _planet);

            for (int i = 1; i <= 360; i++)
            {
                _game.CurrentTick = i;
                _system.ProcessTick();
            }

            Assert.AreEqual(
                1,
                _faction.GetHighestUnlockedOrder(ResearchDiscipline.ShipDesign),
                "Repeated pulses of +1 capacity should reach Frigate's difficulty of 12"
            );
        }

        [Test]
        public void ProcessTick_MultipleFactions_IndependentCapacity()
        {
            Faction empire = new Faction { InstanceID = "FNEMP1", DisplayName = "Empire" };
            _game.GetFactions().Add(empire);

            PlanetSector sector2 = new PlanetSector { InstanceID = "sector2" };
            _game.AttachNode(sector2, _game.Galaxy);
            Planet empirePlanet = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "FNEMP1",
                IsColonized = true,
                EnergyCapacity = 20,
            };
            _game.AttachNode(empirePlanet, sector2);

            // Alliance gets 1 shipyard, Empire gets 3
            _game.AttachNode(CreateShipyard("SY1"), _planet);

            Building firstEmpireShipyard = CreateShipyard("ESY1");
            firstEmpireShipyard.OwnerInstanceID = "FNEMP1";
            Building secondEmpireShipyard = CreateShipyard("ESY2");
            secondEmpireShipyard.OwnerInstanceID = "FNEMP1";
            Building thirdEmpireShipyard = CreateShipyard("ESY3");
            thirdEmpireShipyard.OwnerInstanceID = "FNEMP1";
            _game.AttachNode(firstEmpireShipyard, empirePlanet);
            _game.AttachNode(secondEmpireShipyard, empirePlanet);
            _game.AttachNode(thirdEmpireShipyard, empirePlanet);
            _game.CurrentTick = 30;

            _system.ProcessTick();

            Assert.AreEqual(
                1,
                _faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign)
            );
            Assert.AreEqual(3, empire.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign));
        }

        private Building CreateShipyard(string id)
        {
            return new Building
            {
                InstanceID = id,
                OwnerInstanceID = "FNALL1",
                BuildingType = BuildingType.Shipyard,

                ProductionType = ManufacturingType.Ship,
                ProcessRate = 10,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }

        private void SetupShipResearchCatalog(
            params (string name, int order, int difficulty)[] techs
        )
        {
            IManufacturable[] templates = techs
                .Select(t =>
                    (IManufacturable)
                        new CapitalShip
                        {
                            DisplayName = t.name,
                            ResearchOrder = t.order,
                            ResearchDifficulty = t.difficulty,
                            ManufacturingFactionInstanceIDs = new List<string> { "FNALL1" },
                        }
                )
                .ToArray();
            _faction.RebuildResearchCatalog(templates);
        }
    }
}

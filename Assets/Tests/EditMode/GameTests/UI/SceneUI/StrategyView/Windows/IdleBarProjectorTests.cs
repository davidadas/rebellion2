using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Movement;
using Rebellion.Game.Units;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Windows
{
    [TestFixture]
    public class IdleBarProjectorTests
    {
        private const string _playerFactionId = "player";

        private Faction _playerFaction;

        [SetUp]
        public void SetUp()
        {
            _playerFaction = new Faction { InstanceID = _playerFactionId };
        }

        [Test]
        public void Project_AvailableEntities_ReturnsEveryEntityGroupedAndSorted()
        {
            Officer movingOfficer = CreateOfficer("A Moving Officer");
            movingOfficer.Movement = new MovementState();
            Officer secondOfficer = CreateOfficer("B Officer");
            Officer firstOfficer = CreateOfficer("A Officer");
            SpecialForces buildingSpecialForces = CreateSpecialForces("A Building Unit");
            buildingSpecialForces.ManufacturingStatus = ManufacturingStatus.Building;
            SpecialForces secondSpecialForces = CreateSpecialForces("B Unit");
            SpecialForces firstSpecialForces = CreateSpecialForces("A Unit");
            Planet busyPlanet = CreateManufacturingPlanet("A Busy Planet");
            busyPlanet.ManufacturingQueue[ManufacturingType.Ship] = new List<IManufacturable>
            {
                new CapitalShip(),
            };
            Planet secondPlanet = CreateManufacturingPlanet("B Planet");
            Planet firstPlanet = CreateManufacturingPlanet("A Planet");

            IdleBarRenderData result = IdleBarProjector.Project(
                _playerFaction,
                null,
                new RectInt(10, 20, 300, 200)
            );

            Assert.AreEqual(6, result.Entries.Count);
            CollectionAssert.AreEqual(
                new object[]
                {
                    firstOfficer,
                    secondOfficer,
                    firstSpecialForces,
                    secondSpecialForces,
                    firstPlanet,
                    secondPlanet,
                },
                result.Entries.Select(entry => entry.Entity)
            );
            Assert.AreEqual(new RectInt(10, 20, 300, 200), result.DesktopBounds);
        }

        [Test]
        public void Project_NoAvailableEntities_ReturnsEmptyStrip()
        {
            IdleBarRenderData result = IdleBarProjector.Project(
                _playerFaction,
                null,
                new RectInt()
            );

            Assert.IsEmpty(result.Entries);
        }

        [Test]
        public void Project_ParticipantsWithUnavailableStatus_AreExcluded()
        {
            Officer injuredOfficer = CreateOfficer("Injured");
            injuredOfficer.InjuryPoints = 1;
            Officer retiredOfficer = CreateOfficer("Retired");
            retiredOfficer.IsRetired = true;
            SpecialForces retiredSpecialForces = CreateSpecialForces("Retired Unit");
            retiredSpecialForces.IsRetired = true;

            IdleBarRenderData result = IdleBarProjector.Project(
                _playerFaction,
                null,
                new RectInt()
            );

            Assert.IsEmpty(result.Entries);
        }

        private Officer CreateOfficer(string displayName)
        {
            Officer officer = new Officer
            {
                InstanceID = displayName,
                DisplayName = displayName,
                OwnerInstanceID = _playerFactionId,
            };
            _playerFaction.AddOwnedUnit(officer);
            return officer;
        }

        private SpecialForces CreateSpecialForces(string displayName)
        {
            SpecialForces specialForces = new SpecialForces
            {
                InstanceID = displayName,
                DisplayName = displayName,
                OwnerInstanceID = _playerFactionId,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            specialForces.AllowedMissionTypeIDs.Add("mission");
            _playerFaction.AddOwnedUnit(specialForces);
            return specialForces;
        }

        private Planet CreateManufacturingPlanet(string displayName)
        {
            Planet planet = new Planet
            {
                InstanceID = displayName,
                DisplayName = displayName,
                OwnerInstanceID = _playerFactionId,
            };
            planet.AddTestChild(
                new Building
                {
                    ProductionType = ManufacturingType.Ship,
                    ProcessRate = 1,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                }
            );
            _playerFaction.AddOwnedUnit(planet);
            return planet;
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using UnityEngine;
using GalaxyMapModel = Rebellion.Game.Galaxy.GalaxyMap;
using GalaxyPlanetSector = Rebellion.Game.Galaxy.PlanetSector;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.IdleBar
{
    [TestFixture]
    public class IdleBarProjectorTests
    {
        private const string _playerFactionId = "player";

        private GalaxyMapModel _factionView;
        private GalaxyPlanetSector _sector;
        private Planet _unitPlanet;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _factionView = new GalaxyMapModel();
            _sector = new GalaxyPlanetSector();
            _unitPlanet = new Planet { OwnerInstanceID = _playerFactionId };
            _factionView.AddChild(_sector);
            _sector.SetParent(_factionView);
            _sector.AddChild(_unitPlanet);
            _unitPlanet.SetParent(_sector);
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

            IdleBarRenderData result = new IdleBarProjector(() => null).Project(
                _factionView,
                _playerFactionId,
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
            IdleBarRenderData result = new IdleBarProjector(() => null).Project(
                _factionView,
                _playerFactionId,
                new RectInt()
            );

            Assert.IsEmpty(result.Entries);
        }

        [Test]
        public void Project_MainCharacters_PrecedeOtherOfficers()
        {
            Officer firstNamedOfficer = CreateOfficer("A Officer");
            Officer mainCharacter = CreateOfficer("Z Main Character");
            mainCharacter.IsMain = true;

            IdleBarRenderData result = new IdleBarProjector(() => null).Project(
                _factionView,
                _playerFactionId,
                new RectInt()
            );

            CollectionAssert.AreEqual(
                new[] { mainCharacter, firstNamedOfficer },
                result.Entries.Select(entry => entry.Entity)
            );
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

            IdleBarRenderData result = new IdleBarProjector(() => null).Project(
                _factionView,
                _playerFactionId,
                new RectInt()
            );

            Assert.IsEmpty(result.Entries);
        }

        [Test]
        public void Project_CommandingOfficer_ExcludesOfficerUntilCommandEnds()
        {
            Officer officer = CreateOfficer("Commander");
            officer.CurrentRank = OfficerRank.Commander;
            IdleBarProjector projector = new IdleBarProjector(() => null);

            IdleBarRenderData commanding = projector.Project(
                _factionView,
                _playerFactionId,
                new RectInt()
            );
            officer.CurrentRank = OfficerRank.None;
            IdleBarRenderData resigned = projector.Project(
                _factionView,
                _playerFactionId,
                new RectInt()
            );

            Assert.IsFalse(commanding.Entries.Any(entry => entry.Entity == officer));
            Assert.IsTrue(resigned.Entries.Any(entry => entry.Entity == officer));
        }

        /// <summary>
        /// Creates officer.
        /// </summary>
        /// <param name="displayName">The display name.</param>
        /// <returns>The created officer.</returns>
        private Officer CreateOfficer(string displayName)
        {
            Officer officer = new Officer
            {
                InstanceID = displayName,
                DisplayName = displayName,
                OwnerInstanceID = _playerFactionId,
            };
            _unitPlanet.AddChild(officer);
            officer.SetParent(_unitPlanet);
            return officer;
        }

        /// <summary>
        /// Creates special forces.
        /// </summary>
        /// <param name="displayName">The display name.</param>
        /// <returns>The created special forces.</returns>
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
            _unitPlanet.AddChild(specialForces);
            specialForces.SetParent(_unitPlanet);
            return specialForces;
        }

        /// <summary>
        /// Creates manufacturing planet.
        /// </summary>
        /// <param name="displayName">The display name.</param>
        /// <returns>The created manufacturing planet.</returns>
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
            _sector.AddChild(planet);
            planet.SetParent(_sector);
            return planet;
        }
    }
}

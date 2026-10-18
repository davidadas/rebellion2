using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.IdleBar
{
    [TestFixture]
    public class IdleBarProjectorTests
    {
        private const string _playerFactionId = "player";

        private Faction _playerFaction;

        /// <summary>
        /// Sets up.
        /// </summary>
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

            IdleBarRenderData result = new IdleBarProjector(() => null).Project(
                _playerFaction,
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
                _playerFaction,
                new RectInt()
            );

            Assert.IsEmpty(result.Entries);
        }

        [Test]
        public void Project_MultipleFacilities_ReturnsOneEntryPerIdleManufacturingLane()
        {
            Planet planet = CreateManufacturingPlanet("Coruscant");
            for (int index = 0; index < 4; index++)
                planet.AddTestChild(
                    new Building
                    {
                        ProductionType = ManufacturingType.Ship,
                        ProcessRate = 1,
                        ManufacturingStatus = ManufacturingStatus.Complete,
                    }
                );
            planet.AddTestChild(
                new Building
                {
                    ProductionType = ManufacturingType.Building,
                    ProcessRate = 1,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                }
            );

            IdleBarRenderData result = new IdleBarProjector(() => null).Project(
                _playerFaction,
                new RectInt()
            );

            CollectionAssert.AreEqual(
                new[] { "Coruscant (Construction Yard)", "Coruscant (Shipyard)" },
                result.Entries.Select(entry => entry.Name)
            );
        }

        [Test]
        public void Project_IdleLane_CapturesCompletedStationaryFacilityCounts()
        {
            Planet planet = CreateManufacturingPlanet("Coruscant");
            planet.AddTestChild(
                new Building
                {
                    ProductionType = ManufacturingType.Ship,
                    ProcessRate = 1,
                    ManufacturingStatus = ManufacturingStatus.Building,
                }
            );
            planet.AddTestChild(
                new Building
                {
                    ProductionType = ManufacturingType.Ship,
                    ProcessRate = 1,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                    Movement = new MovementState(),
                }
            );

            IdleBarEntry entry = new IdleBarProjector(() => null)
                .Project(_playerFaction, new RectInt())
                .Entries.Single();

            Assert.AreEqual(1, entry.IdleFacilityCount);
            Assert.AreEqual(1, entry.TotalFacilityCount);
        }

        [Test]
        public void Project_MultipleLaneTypes_OrdersByLaneThenDescendingFacilityCount()
        {
            Officer officer = CreateOfficer("Officer");
            Planet smallPlanet = CreateManufacturingPlanet("A Small Planet");
            Planet largePlanet = CreateManufacturingPlanet("Z Large Planet");
            foreach (Planet planet in new[] { smallPlanet, largePlanet })
            {
                foreach (
                    ManufacturingType type in new[]
                    {
                        ManufacturingType.Building,
                        ManufacturingType.Troop,
                    }
                )
                    planet.AddTestChild(
                        new Building
                        {
                            ProductionType = type,
                            ProcessRate = 1,
                            ManufacturingStatus = ManufacturingStatus.Complete,
                        }
                    );
            }
            foreach (
                ManufacturingType type in new[]
                {
                    ManufacturingType.Building,
                    ManufacturingType.Ship,
                    ManufacturingType.Troop,
                }
            )
                largePlanet.AddTestChild(
                    new Building
                    {
                        ProductionType = type,
                        ProcessRate = 1,
                        ManufacturingStatus = ManufacturingStatus.Complete,
                    }
                );

            IdleBarRenderData result = new IdleBarProjector(() => null).Project(
                _playerFaction,
                new RectInt()
            );

            CollectionAssert.AreEqual(
                new[]
                {
                    officer.GetDisplayName(),
                    "Z Large Planet (Construction Yard)",
                    "A Small Planet (Construction Yard)",
                    "Z Large Planet (Shipyard)",
                    "A Small Planet (Shipyard)",
                    "Z Large Planet (Training Facility)",
                    "A Small Planet (Training Facility)",
                },
                result.Entries.Select(entry => entry.Name)
            );
        }

        [Test]
        public void Project_BusyShipyard_KeepsIdleConstructionLane()
        {
            Planet planet = CreateManufacturingPlanet("Coruscant");
            planet.ManufacturingQueue[ManufacturingType.Ship] = new List<IManufacturable>
            {
                new CapitalShip(),
            };
            planet.AddTestChild(
                new Building
                {
                    ProductionType = ManufacturingType.Building,
                    ProcessRate = 1,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                }
            );

            IdleBarRenderData result = new IdleBarProjector(() => null).Project(
                _playerFaction,
                new RectInt()
            );

            Assert.AreEqual("Coruscant (Construction Yard)", result.Entries.Single().Name);
        }

        [TestCase(ManufacturingType.Ship, "shipyard")]
        [TestCase(ManufacturingType.Troop, "training")]
        [TestCase(ManufacturingType.Building, "construction")]
        public void Project_IdleManufacturingLane_UsesThemedIcon(
            ManufacturingType type,
            string expectedPath
        )
        {
            Planet planet = new Planet { InstanceID = "planet", DisplayName = "Coruscant" };
            planet.AddTestChild(
                new Building
                {
                    ProductionType = type,
                    ProcessRate = 1,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                }
            );
            _playerFaction.AddOwnedUnit(planet);
            List<string> requestedPaths = new List<string>();
            StatusWindowTheme theme = new StatusWindowTheme
            {
                ShipyardImagePath = "shipyard",
                TrainingImagePath = "training",
                ConstructionImagePath = "construction",
            };
            Texture2D texture = new Texture2D(2, 2);
            try
            {
                UIContext uiContext = new UIContext(
                    TestGame.Create(TestConfig.Create()),
                    new FactionThemeLibrary(
                        new FactionThemes
                        {
                            new FactionTheme { FactionInstanceID = "DEFAULT" },
                            new FactionTheme
                            {
                                FactionInstanceID = _playerFactionId,
                                StrategyWindows = new StrategyWindowsTheme { Status = theme },
                            },
                        }
                    ),
                    new EncyclopediaCatalog(System.Array.Empty<EncyclopediaEntry>()),
                    path =>
                    {
                        requestedPaths.Add(path);
                        return texture;
                    }
                );

                IdleBarRenderData result = new IdleBarProjector(() => uiContext).Project(
                    _playerFaction,
                    new RectInt()
                );

                Assert.AreSame(texture, result.Entries.Single().Texture);
                CollectionAssert.AreEqual(new[] { expectedPath }, requestedPaths);
                CollectionAssert.AreEqual(
                    new[] { expectedPath },
                    IdleBarProjector.GetTexturePaths(_playerFaction, theme)
                );
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void Project_MainCharacters_PrecedeOtherOfficers()
        {
            Officer firstNamedOfficer = CreateOfficer("A Officer");
            Officer mainCharacter = CreateOfficer("Z Main Character");
            mainCharacter.IsMain = true;

            IdleBarRenderData result = new IdleBarProjector(() => null).Project(
                _playerFaction,
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
                _playerFaction,
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

            IdleBarRenderData commanding = projector.Project(_playerFaction, new RectInt());
            officer.CurrentRank = OfficerRank.None;
            IdleBarRenderData resigned = projector.Project(_playerFaction, new RectInt());

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
            _playerFaction.AddOwnedUnit(officer);
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
            _playerFaction.AddOwnedUnit(specialForces);
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
            _playerFaction.AddOwnedUnit(planet);
            return planet;
        }
    }
}

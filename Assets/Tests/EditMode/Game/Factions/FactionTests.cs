using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Movement;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Tests.Game.Factions
{
    [TestFixture]
    public class FactionTests
    {
        private Faction _faction;
        private Planet _planet1;
        private Planet _planet2;
        private Fleet _fleet;
        private Officer _officer;
        private Building _building;
        private Technology _technology;

        [SetUp]
        public void SetUp()
        {
            _faction = new Faction
            {
                InstanceID = "FACTION1",
                DisplayName = "Rebel Alliance",
                PlayerID = "PLAYER1",
            };

            _planet1 = new Planet { InstanceID = "PLANET1", OwnerInstanceID = "FACTION1" };

            _planet2 = new Planet { InstanceID = "PLANET2", OwnerInstanceID = "FACTION1" };

            _fleet = new Fleet { InstanceID = "FLEET1", OwnerInstanceID = "FACTION1" };

            _officer = new Officer
            {
                InstanceID = "OFFICER1",
                OwnerInstanceID = "FACTION1",
                Movement = null,
            };

            _building = new Building
            {
                InstanceID = "BUILDING1",
                DisplayName = "Mine",
                ConstructionCost = 100,
                ResearchOrder = 1,
                ResearchDifficulty = 24,
            };

            _technology = new Technology(_building);
        }

        [Test]
        public void IsAIControlled_WithPlayerID_ReturnsFalse()
        {
            bool isAI = _faction.IsAIControlled();

            Assert.IsFalse(isAI, "Faction with PlayerID should not be AI controlled");
        }

        [Test]
        public void IsAIControlled_WithoutPlayerID_ReturnsTrue()
        {
            _faction.PlayerID = null;

            bool isAI = _faction.IsAIControlled();

            Assert.IsTrue(isAI, "Faction without PlayerID should be AI controlled");
        }

        [Test]
        public void AddOwnedUnit_ValidPlanet_AddsPlanetToOwnedNodes()
        {
            _faction.AddOwnedUnit(_planet1);

            List<Planet> planets = _faction.GetOwnedUnitsByType<Planet>();

            Assert.Contains(_planet1, planets, "Faction should contain the added planet");
        }

        [Test]
        public void RemoveOwnedUnit_OwnedPlanet_RemovesFromOwnedNodes()
        {
            _faction.AddOwnedUnit(_planet1);

            _faction.RemoveOwnedUnit(_planet1);

            List<Planet> planets = _faction.GetOwnedUnitsByType<Planet>();

            Assert.IsFalse(planets.Contains(_planet1), "Faction should not contain removed planet");
        }

        [Test]
        public void GetOwnedUnitsByType_FactionWithMixedUnits_ReturnsUnitsOfType()
        {
            _faction.AddOwnedUnit(_planet1);
            _faction.AddOwnedUnit(_planet2);
            _faction.AddOwnedUnit(_fleet);

            List<Planet> planets = _faction.GetOwnedUnitsByType<Planet>();
            List<Fleet> fleets = _faction.GetOwnedUnitsByType<Fleet>();

            Assert.AreEqual(2, planets.Count, "Should return correct number of planets");
            Assert.Contains(_planet1, planets, "Should contain planet1");
            Assert.Contains(_planet2, planets, "Should contain planet2");
            Assert.AreEqual(1, fleets.Count, "Should return correct number of fleets");
            Assert.Contains(_fleet, fleets, "Should contain fleet");
        }

        [Test]
        public void GetOwnedColonizedPlanets_FactionWithUncolonizedOwnedPlanet_ReturnsOnlyColonizedPlanets()
        {
            _planet1.IsColonized = true;
            _planet2.IsColonized = false;
            _faction.AddOwnedUnit(_planet1);
            _faction.AddOwnedUnit(_planet2);

            List<Planet> planets = _faction.GetOwnedColonizedPlanets();

            Assert.AreEqual(1, planets.Count);
            Assert.Contains(_planet1, planets);
            Assert.IsFalse(planets.Contains(_planet2));
        }

        [Test]
        public void GetUnlockedTechnologies_FactionBelowOrder_ReturnsOnlyUnlockedTechnologies()
        {
            _faction.SetHighestUnlockedOrder(ResearchDiscipline.FacilityDesign, 2);

            Building advancedBuilding = new Building
            {
                DisplayName = "Advanced Mine",
                ResearchOrder = 2,
                ResearchDifficulty = 40,
            };

            Building futureBuilding = new Building
            {
                DisplayName = "Future Building",
                ResearchOrder = 3,
                ResearchDifficulty = 60,
            };

            IManufacturable[] templates = new IManufacturable[]
            {
                _building,
                advancedBuilding,
                futureBuilding,
            };
            _faction.RebuildResearchCatalog(templates);

            List<Technology> unlocked = _faction.GetUnlockedTechnologies(
                ResearchDiscipline.FacilityDesign
            );

            Assert.AreEqual(
                2,
                unlocked.Count,
                "Should only return technologies at or below unlocked order"
            );
            Assert.IsFalse(
                unlocked.Exists(t => t.GetReference().GetDisplayName() == "Future Building"),
                "Should not contain order 3 technology"
            );
        }

        [Test]
        public void GetCurrentResearchTarget_WithUnresearched_ReturnsNextUnlocked()
        {
            _faction.SetHighestUnlockedOrder(ResearchDiscipline.FacilityDesign, 0);

            IManufacturable[] templates = new IManufacturable[] { _building };
            _faction.RebuildResearchCatalog(templates);

            Technology target = _faction.GetCurrentResearchTarget(
                ResearchDiscipline.FacilityDesign
            );

            Assert.IsNotNull(target, "Should return the next technology to research");
            Assert.AreEqual(1, target.GetResearchOrder());
        }

        [Test]
        public void GetCurrentResearchTarget_AllUnlocked_ReturnsNull()
        {
            _faction.SetHighestUnlockedOrder(ResearchDiscipline.FacilityDesign, 99);

            IManufacturable[] templates = new IManufacturable[] { _building };
            _faction.RebuildResearchCatalog(templates);

            Technology target = _faction.GetCurrentResearchTarget(
                ResearchDiscipline.FacilityDesign
            );

            Assert.IsNull(target, "Should return null when all technologies are unlocked");
        }

        [Test]
        public void GetHighestUnlockedOrder_WithSetOrder_ReturnsCorrectOrder()
        {
            _faction.SetHighestUnlockedOrder(ResearchDiscipline.ShipDesign, 5);

            int order = _faction.GetHighestUnlockedOrder(ResearchDiscipline.ShipDesign);

            Assert.AreEqual(5, order, "Should return the correct unlocked order");
        }

        [Test]
        public void SetHighestUnlockedOrder_ValidOrder_SetsOrder()
        {
            _faction.SetHighestUnlockedOrder(ResearchDiscipline.TroopTraining, 3);

            Assert.AreEqual(
                3,
                _faction.GetHighestUnlockedOrder(ResearchDiscipline.TroopTraining),
                "Should set the unlocked order correctly"
            );
        }

        [Test]
        public void RebuildResearchCatalog_WithRestrictedBuilding_FiltersOwnership()
        {
            Building restrictedBuilding = new Building
            {
                DisplayName = "Restricted Building",
                ResearchOrder = 1,
                ResearchDifficulty = 24,
                AllowedOwnerInstanceIDs = new List<string> { "FACTION2" },
            };

            _building.AllowedOwnerInstanceIDs = new List<string> { "FACTION1" };

            IManufacturable[] templates = new IManufacturable[] { _building, restrictedBuilding };
            _faction.RebuildResearchCatalog(templates);

            List<ResearchCatalogEntry> entries = _faction.ResearchCatalog[
                ResearchDiscipline.FacilityDesign
            ];
            Assert.AreEqual(1, entries.Count, "Should only include technologies for this faction");
        }

        [Test]
        public void RebuildResearchCatalog_WithMultipleBuildings_SortsByResearchOrder()
        {
            Building b1 = new Building
            {
                DisplayName = "B1",
                ResearchOrder = 3,
                ResearchDifficulty = 60,
                AllowedOwnerInstanceIDs = new List<string> { "FACTION1" },
            };
            Building b2 = new Building
            {
                DisplayName = "B2",
                ResearchOrder = 1,
                ResearchDifficulty = 24,
                AllowedOwnerInstanceIDs = new List<string> { "FACTION1" },
            };
            Building b3 = new Building
            {
                DisplayName = "B3",
                ResearchOrder = 0,
                ResearchDifficulty = 0,
                AllowedOwnerInstanceIDs = new List<string> { "FACTION1" },
            };

            _faction.RebuildResearchCatalog(new IManufacturable[] { b1, b2, b3 });

            List<ResearchCatalogEntry> entries = _faction.ResearchCatalog[
                ResearchDiscipline.FacilityDesign
            ];
            Assert.AreEqual(0, entries[0].Order);
            Assert.AreEqual(1, entries[1].Order);
            Assert.AreEqual(3, entries[2].Order);
        }

        private void SetupShipCatalog(params (string name, int order, int difficulty)[] techs)
        {
            IManufacturable[] templates = techs
                .Select(t =>
                    (IManufacturable)
                        new CapitalShip
                        {
                            DisplayName = t.name,
                            ResearchOrder = t.order,
                            ResearchDifficulty = t.difficulty,
                            AllowedOwnerInstanceIDs = new List<string> { "FACTION1" },
                        }
                )
                .ToArray();
            _faction.RebuildResearchCatalog(templates);
        }

        [Test]
        public void ApplyResearchProgress_MeetsDifficulty_ReturnsUnlockedTechnology()
        {
            SetupShipCatalog(("Dreadnaught", 0, 0), ("Frigate", 1, 12));

            Technology unlocked = _faction.ApplyResearchProgress(ResearchDiscipline.ShipDesign, 12);

            Assert.IsNotNull(unlocked, "Apply should return the technology that was unlocked");
            Assert.AreEqual("Frigate", unlocked.GetReference().DisplayName);
            Assert.AreEqual(1, _faction.GetHighestUnlockedOrder(ResearchDiscipline.ShipDesign));
            Assert.AreEqual(
                0,
                _faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign),
                "Cost should be subtracted from capacity"
            );
        }

        [Test]
        public void ApplyResearchProgress_ExcessCapacity_AdvancesOnceAndCarriesRemainder()
        {
            SetupShipCatalog(("Dreadnaught", 0, 0), ("Frigate", 1, 12), ("Cruiser", 2, 24));

            Technology unlocked = _faction.ApplyResearchProgress(ResearchDiscipline.ShipDesign, 40);

            Assert.AreEqual("Frigate", unlocked.GetReference().DisplayName);
            Assert.AreEqual(1, _faction.GetHighestUnlockedOrder(ResearchDiscipline.ShipDesign));
            Assert.AreEqual(
                28,
                _faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign),
                "One call should advance only one order and carry the remainder"
            );
        }

        [Test]
        public void ApplyResearchProgress_BelowDifficulty_ReturnsNullAndAccumulates()
        {
            SetupShipCatalog(("Dreadnaught", 0, 0), ("Frigate", 1, 12));

            Technology unlocked = _faction.ApplyResearchProgress(ResearchDiscipline.ShipDesign, 5);

            Assert.IsNull(unlocked, "Below-difficulty progress should not unlock anything");
            Assert.AreEqual(0, _faction.GetHighestUnlockedOrder(ResearchDiscipline.ShipDesign));
            Assert.AreEqual(
                5,
                _faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign)
            );
        }

        [Test]
        public void ApplyResearchProgress_AllUnlocked_ReturnsNull()
        {
            SetupShipCatalog(("Dreadnaught", 0, 0), ("Frigate", 1, 12));
            _faction.SetHighestUnlockedOrder(ResearchDiscipline.ShipDesign, 1);

            Technology unlocked = _faction.ApplyResearchProgress(
                ResearchDiscipline.ShipDesign,
                9999
            );

            Assert.IsNull(unlocked, "Exhausted disciplines should never unlock anything");
            Assert.AreEqual(1, _faction.GetHighestUnlockedOrder(ResearchDiscipline.ShipDesign));
        }

        [Test]
        public void ApplyResearchProgress_WithRealTemplates_UnlocksNextTechnology(
            [Values(
                ResearchDiscipline.ShipDesign,
                ResearchDiscipline.FacilityDesign,
                ResearchDiscipline.TroopTraining
            )]
                ResearchDiscipline discipline
        )
        {
            // Real game-data templates restrict ownership to in-game faction IDs;
            // use a known faction so RebuildResearchCatalog retains entries.
            Faction alliance = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };

            IManufacturable[] templates = ResourceManager
                .GetGameData<Building>()
                .Cast<IManufacturable>()
                .Concat(ResourceManager.GetGameData<CapitalShip>())
                .Concat(ResourceManager.GetGameData<Starfighter>())
                .Concat(ResourceManager.GetGameData<Regiment>())
                .Concat(ResourceManager.GetGameData<SpecialForces>())
                .ToArray();
            alliance.RebuildResearchCatalog(templates);

            alliance.SetHighestUnlockedOrder(discipline, 0);
            int techsBefore = alliance.GetUnlockedTechnologies(discipline).Count;

            Technology target = alliance.GetCurrentResearchTarget(discipline);
            if (target == null)
                Assert.Ignore(
                    $"No researchable {discipline} technologies for {alliance.InstanceID}"
                );

            Technology unlocked = alliance.ApplyResearchProgress(
                discipline,
                target.GetResearchDifficulty()
            );

            Assert.AreEqual(target.GetResearchOrder(), unlocked.GetResearchOrder());
            Assert.AreEqual(
                target.GetResearchOrder(),
                alliance.GetHighestUnlockedOrder(discipline)
            );
            Assert.Greater(
                alliance.GetUnlockedTechnologies(discipline).Count,
                techsBefore,
                $"Unlocking {discipline} technology should increase available technologies"
            );
        }

        [Test]
        public void AddMessage_WithAnyMessageType_AddsToMatchingBucket()
        {
            foreach (
                MessageType messageType in Enum.GetValues(typeof(MessageType)).Cast<MessageType>()
            )
            {
                Message message = new Message(messageType, "Message text");

                _faction.AddMessage(message);

                Assert.Contains(
                    message,
                    _faction.Messages[messageType],
                    "Should add message to correct type list"
                );
            }
        }

        [Test]
        public void RemoveMessage_ExistingMessage_RemovesFromList()
        {
            Message message = new Message(MessageType.Mission, "Mission completed");
            _faction.AddMessage(message);

            _faction.RemoveMessage(message);

            Assert.IsFalse(
                _faction.Messages[MessageType.Mission].Contains(message),
                "Should remove message from list"
            );
        }

        [Test]
        public void GetAvailableMissionParticipants_MixedParticipantStates_ReturnsOnlyAvailableParticipants()
        {
            Officer availableOfficer = new Officer
            {
                InstanceID = "OFFICER1",
                OwnerInstanceID = "FACTION1",
                Movement = null,
            };

            Officer unavailableOfficer = new Officer
            {
                InstanceID = "OFFICER2",
                OwnerInstanceID = "FACTION1",
                Movement = new MovementState(),
            };

            SpecialForces availableSpecialForces = new SpecialForces
            {
                InstanceID = "SPECOPS1",
                OwnerInstanceID = "FACTION1",
                ManufacturingStatus = ManufacturingStatus.Complete,
                AllowedMissionTypes = new List<MissionType> { MissionType.Sabotage },
            };

            SpecialForces buildingSpecialForces = new SpecialForces
            {
                InstanceID = "SPECOPS2",
                OwnerInstanceID = "FACTION1",
                ManufacturingStatus = ManufacturingStatus.Building,
                AllowedMissionTypes = new List<MissionType> { MissionType.Sabotage },
            };

            SpecialForces unqualifiedSpecialForces = new SpecialForces
            {
                InstanceID = "SPECOPS3",
                OwnerInstanceID = "FACTION1",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };

            _faction.AddOwnedUnit(availableOfficer);
            _faction.AddOwnedUnit(unavailableOfficer);
            _faction.AddOwnedUnit(availableSpecialForces);
            _faction.AddOwnedUnit(buildingSpecialForces);
            _faction.AddOwnedUnit(unqualifiedSpecialForces);

            List<IMissionParticipant> available = _faction.GetAvailableMissionParticipants();

            Assert.AreEqual(2, available.Count, "Should return only available participants");
            Assert.Contains(availableOfficer, available, "Should contain available officer");
            Assert.Contains(
                availableSpecialForces,
                available,
                "Should contain available special forces"
            );
            Assert.IsFalse(
                available.Contains(unavailableOfficer),
                "Should not contain unavailable officer"
            );
            Assert.IsFalse(
                available.Contains(buildingSpecialForces),
                "Should not contain incomplete special forces"
            );
            Assert.IsFalse(
                available.Contains(unqualifiedSpecialForces),
                "Should not contain special forces with no allowed missions"
            );
        }

        [Test]
        public void GetTotalRawResourceNodes_FactionWithMultiplePlanets_ReturnsSumAcrossPlanets()
        {
            _planet1.NumRawResourceNodes = 10;
            _planet2.NumRawResourceNodes = 15;

            _faction.AddOwnedUnit(_planet1);
            _faction.AddOwnedUnit(_planet2);

            int total = _faction.GetTotalRawResourceNodes();

            Assert.AreEqual(25, total, "Should sum raw resource nodes across all planets");
        }

        [Test]
        public void GetTotalAvailableResourceNodes_FactionWithBlockadedPlanet_ReturnsSumAcrossPlanets()
        {
            _planet1.NumRawResourceNodes = 10;
            // planet1 is not blockaded by default (no enemy fleets)

            _planet2.NumRawResourceNodes = 15;
            // Add an enemy fleet to planet2 to blockade it
            Fleet enemyFleet = new Fleet
            {
                InstanceID = "ENEMYFLEET1",
                OwnerInstanceID = "FACTION2",
            };
            _planet2.Fleets.Add(enemyFleet);

            _faction.AddOwnedUnit(_planet1);
            _faction.AddOwnedUnit(_planet2);

            int total = _faction.GetTotalAvailableResourceNodes();

            Assert.AreEqual(10, total, "Should only count non-blockaded planets");
        }

        [Test]
        public void SerializeAndDeserialize_MaintainsState()
        {
            _faction.SetHighestUnlockedOrder(ResearchDiscipline.ShipDesign, 3);
            _faction.AddOwnedUnit(_planet1);
            _faction.AddMessage(new Message(MessageType.Resource, "Test message"));

            string serialized = SerializationHelper.Serialize(_faction);
            Console.WriteLine("=== SERIALIZED XML ===");
            Console.WriteLine(serialized);
            Console.WriteLine("=== END ===");
            Faction deserialized = SerializationHelper.Deserialize<Faction>(serialized);

            Assert.AreEqual(
                _faction.InstanceID,
                deserialized.InstanceID,
                "InstanceID should be correctly deserialized."
            );
            Assert.AreEqual(
                _faction.DisplayName,
                deserialized.DisplayName,
                "DisplayName should be correctly deserialized."
            );
            Assert.AreEqual(
                _faction.PlayerID,
                deserialized.PlayerID,
                "PlayerID should be correctly deserialized."
            );
            Assert.AreEqual(
                _faction.GetHighestUnlockedOrder(ResearchDiscipline.ShipDesign),
                deserialized.GetHighestUnlockedOrder(ResearchDiscipline.ShipDesign),
                "Research orders should be correctly deserialized."
            );
        }

        [Test]
        public void SerializeAndDeserialize_ResearchState_RetainsAllDisciplinesAndIgnoresDerivedCatalogs()
        {
            _faction.ResearchState.CostScalePercent = 125;
            _faction.ResearchState.NextRefreshTick = 47;

            _faction.SetHighestUnlockedOrder(ResearchDiscipline.ShipDesign, 2);
            _faction.SetHighestUnlockedOrder(ResearchDiscipline.FacilityDesign, 4);
            _faction.SetHighestUnlockedOrder(ResearchDiscipline.TroopTraining, 6);

            _faction.ApplyResearchProgress(ResearchDiscipline.ShipDesign, 11);
            _faction.ApplyResearchProgress(ResearchDiscipline.FacilityDesign, 22);
            _faction.ApplyResearchProgress(ResearchDiscipline.TroopTraining, 33);

            Building buildingTechnology = new Building
            {
                DisplayName = "Advanced Mine",
                ResearchOrder = 4,
                ResearchDifficulty = 60,
                AllowedOwnerInstanceIDs = new List<string> { "FACTION1" },
            };
            CapitalShip shipTechnology = new CapitalShip
            {
                DisplayName = "Cruiser",
                ResearchOrder = 2,
                ResearchDifficulty = 24,
                AllowedOwnerInstanceIDs = new List<string> { "FACTION1" },
            };
            Regiment troopTechnology = new Regiment
            {
                DisplayName = "Elite Troopers",
                ResearchOrder = 6,
                ResearchDifficulty = 48,
                AllowedOwnerInstanceIDs = new List<string> { "FACTION1" },
            };

            _faction.RebuildResearchCatalog(
                new IManufacturable[] { buildingTechnology, shipTechnology, troopTechnology }
            );

            string serialized = SerializationHelper.Serialize(_faction);
            Faction deserialized = SerializationHelper.Deserialize<Faction>(serialized);

            Assert.AreEqual(125, deserialized.ResearchState.CostScalePercent);
            Assert.AreEqual(47, deserialized.ResearchState.NextRefreshTick);

            Assert.AreEqual(2, deserialized.GetHighestUnlockedOrder(ResearchDiscipline.ShipDesign));
            Assert.AreEqual(
                4,
                deserialized.GetHighestUnlockedOrder(ResearchDiscipline.FacilityDesign)
            );
            Assert.AreEqual(
                6,
                deserialized.GetHighestUnlockedOrder(ResearchDiscipline.TroopTraining)
            );

            Assert.AreEqual(
                11,
                deserialized.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign)
            );
            Assert.AreEqual(
                22,
                deserialized.GetResearchCapacityRemaining(ResearchDiscipline.FacilityDesign)
            );
            Assert.AreEqual(
                33,
                deserialized.GetResearchCapacityRemaining(ResearchDiscipline.TroopTraining)
            );

            Assert.AreEqual(
                0,
                deserialized.ResearchCatalog.Count,
                "ResearchCatalog should be rebuilt after load, not serialized."
            );
        }

        [Test]
        public void GetHQInstanceID_FactionWithHQ_ReturnsHQInstanceID()
        {
            _faction.HQInstanceID = "HQ1";

            string hqID = _faction.GetHQInstanceID();

            Assert.AreEqual("HQ1", hqID, "Should return the HQ instance ID");
        }

        [Test]
        public void GetHQInstanceID_WithNullHQ_ReturnsNull()
        {
            _faction.HQInstanceID = null;

            string hqID = _faction.GetHQInstanceID();

            Assert.IsNull(hqID, "Should return null when HQ is not set");
        }

        [Test]
        public void GetTotalRawMinedResources_FactionWithMultiplePlanets_ReturnsSumAcrossPlanets()
        {
            _planet1.NumRawResourceNodes = 25;
            _planet1.IsColonized = true;
            _planet1.EnergyCapacity = 50;
            for (int i = 0; i < 20; i++)
            {
                Building mine = new Building
                {
                    BuildingType = BuildingType.Mine,
                    OwnerInstanceID = "FACTION1",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                _planet1.AddChild(mine);
            }

            _planet2.NumRawResourceNodes = 35;
            _planet2.IsColonized = true;
            _planet2.EnergyCapacity = 50;
            for (int i = 0; i < 30; i++)
            {
                Building mine = new Building
                {
                    BuildingType = BuildingType.Mine,
                    OwnerInstanceID = "FACTION1",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                _planet2.AddChild(mine);
            }

            _faction.AddOwnedUnit(_planet1);
            _faction.AddOwnedUnit(_planet2);

            int total = _faction.GetTotalRawMinedResources();

            Assert.AreEqual(50, total, "Should sum raw mined resources across all planets");
        }

        [Test]
        public void GetTotalAvailableMinedResources_FactionWithBlockadedPlanet_ReturnsSumAcrossPlanets()
        {
            _planet1.NumRawResourceNodes = 25;
            _planet1.IsColonized = true;
            _planet1.EnergyCapacity = 50;
            for (int i = 0; i < 20; i++)
            {
                Building mine = new Building
                {
                    BuildingType = BuildingType.Mine,
                    OwnerInstanceID = "FACTION1",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                _planet1.AddChild(mine);
            }

            _planet2.NumRawResourceNodes = 35;
            _planet2.IsColonized = true;
            _planet2.EnergyCapacity = 50;
            for (int i = 0; i < 30; i++)
            {
                Building mine = new Building
                {
                    BuildingType = BuildingType.Mine,
                    OwnerInstanceID = "FACTION1",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                _planet2.AddChild(mine);
            }
            Fleet enemyFleet = new Fleet
            {
                InstanceID = "ENEMYFLEET1",
                OwnerInstanceID = "FACTION2",
            };
            _planet2.Fleets.Add(enemyFleet);

            _faction.AddOwnedUnit(_planet1);
            _faction.AddOwnedUnit(_planet2);

            int total = _faction.GetTotalAvailableMinedResources();

            Assert.AreEqual(20, total, "Should only count non-blockaded planets");
        }

        [Test]
        public void GetTotalRawRefinementCapacity_FactionWithMultiplePlanets_ReturnsSumAcrossPlanets()
        {
            _planet1.IsColonized = true;
            _planet1.EnergyCapacity = 50;
            for (int i = 0; i < 5; i++)
            {
                Building refinery = new Building
                {
                    BuildingType = BuildingType.Refinery,
                    OwnerInstanceID = "FACTION1",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                _planet1.AddChild(refinery);
            }

            _planet2.IsColonized = true;
            _planet2.EnergyCapacity = 50;
            for (int i = 0; i < 10; i++)
            {
                Building refinery = new Building
                {
                    BuildingType = BuildingType.Refinery,
                    OwnerInstanceID = "FACTION1",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                _planet2.AddChild(refinery);
            }

            _faction.AddOwnedUnit(_planet1);
            _faction.AddOwnedUnit(_planet2);

            int total = _faction.GetTotalRawRefinementCapacity();

            Assert.AreEqual(15, total, "Should sum raw refinement capacity across all planets");
        }

        [Test]
        public void GetTotalAvailableRefinementCapacity_FactionWithBlockadedPlanet_ReturnsSumAcrossPlanets()
        {
            _planet1.IsColonized = true;
            _planet1.EnergyCapacity = 50;
            for (int i = 0; i < 5; i++)
            {
                Building refinery = new Building
                {
                    BuildingType = BuildingType.Refinery,
                    OwnerInstanceID = "FACTION1",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                _planet1.AddChild(refinery);
            }

            _planet2.IsColonized = true;
            _planet2.EnergyCapacity = 50;
            for (int i = 0; i < 10; i++)
            {
                Building refinery = new Building
                {
                    BuildingType = BuildingType.Refinery,
                    OwnerInstanceID = "FACTION1",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                _planet2.AddChild(refinery);
            }
            Fleet enemyFleet = new Fleet
            {
                InstanceID = "ENEMYFLEET1",
                OwnerInstanceID = "FACTION2",
            };
            _planet2.Fleets.Add(enemyFleet);

            _faction.AddOwnedUnit(_planet1);
            _faction.AddOwnedUnit(_planet2);

            int total = _faction.GetTotalAvailableRefinementCapacity();

            Assert.AreEqual(5, total, "Should only count non-blockaded planets");
        }

        [Test]
        public void GetTotalAvailableMaterials_FactionWithMultiplePlanets_CalculatesAvailableTotal()
        {
            _planet1.NumRawResourceNodes = 10;
            _planet1.IsColonized = true;
            _planet1.EnergyCapacity = 50;
            for (int i = 0; i < 8; i++)
            {
                Building mine = new Building
                {
                    BuildingType = BuildingType.Mine,
                    OwnerInstanceID = "FACTION1",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                _planet1.AddChild(mine);
            }
            for (int i = 0; i < 5; i++)
            {
                Building refinery = new Building
                {
                    BuildingType = BuildingType.Refinery,
                    OwnerInstanceID = "FACTION1",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                _planet1.AddChild(refinery);
            }

            _faction.AddOwnedUnit(_planet1);

            int total = _faction.GetTotalAvailableMaterialsRaw();

            // Min(8, 10) = 8, Min(8, 5) = 5 (raw count before multiplier)
            Assert.AreEqual(5, total, "Should calculate available materials correctly");
        }

        [Test]
        public void GetTotalAvailableMaterials_FactionWithBlockadedPlanet_ExcludesBlockadedPlanets()
        {
            _planet1.NumRawResourceNodes = 10;
            _planet1.IsColonized = true;
            _planet1.EnergyCapacity = 50;
            for (int i = 0; i < 8; i++)
            {
                Building mine = new Building
                {
                    BuildingType = BuildingType.Mine,
                    OwnerInstanceID = "FACTION1",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                _planet1.AddChild(mine);
            }
            for (int i = 0; i < 5; i++)
            {
                Building refinery = new Building
                {
                    BuildingType = BuildingType.Refinery,
                    OwnerInstanceID = "FACTION1",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                _planet1.AddChild(refinery);
            }

            _planet2.NumRawResourceNodes = 15;
            _planet2.IsColonized = true;
            _planet2.EnergyCapacity = 50;
            for (int i = 0; i < 12; i++)
            {
                Building mine = new Building
                {
                    BuildingType = BuildingType.Mine,
                    OwnerInstanceID = "FACTION1",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                _planet2.AddChild(mine);
            }
            for (int i = 0; i < 8; i++)
            {
                Building refinery = new Building
                {
                    BuildingType = BuildingType.Refinery,
                    OwnerInstanceID = "FACTION1",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                _planet2.AddChild(refinery);
            }
            Fleet enemyFleet = new Fleet
            {
                InstanceID = "ENEMYFLEET1",
                OwnerInstanceID = "FACTION2",
            };
            _planet2.Fleets.Add(enemyFleet);

            _faction.AddOwnedUnit(_planet1);
            _faction.AddOwnedUnit(_planet2);

            int total = _faction.GetTotalAvailableMaterialsRaw();

            // Only planet1 should count: Min(8, 10) = 8, Min(8, 5) = 5 (raw count before multiplier)
            Assert.AreEqual(5, total, "Should exclude blockaded planets from calculation");
        }

        [Test]
        public void GetNearestFriendlyPlanetTo_MultipleFriendlyPlanets_ReturnsClosestPlanet()
        {
            Planet planet3 = new Planet
            {
                InstanceID = "PLANET3",
                OwnerInstanceID = "FACTION1",
                PositionX = 50,
                PositionY = 50,
            };

            _planet1.IsColonized = true;
            _planet1.EnergyCapacity = 10;
            _planet1.PositionX = 5;
            _planet1.PositionY = 0;
            _planet2.PositionX = 20;
            _planet2.PositionY = 0;

            Building testBuilding = new Building
            {
                InstanceID = "TESTBUILDING",
                OwnerInstanceID = "FACTION1",
            };
            _planet1.AddChild(testBuilding);
            testBuilding.SetParent(_planet1);

            _faction.AddOwnedUnit(_planet1);
            _faction.AddOwnedUnit(_planet2);
            _faction.AddOwnedUnit(planet3);

            Planet nearest = _faction.GetNearestFriendlyPlanetTo(testBuilding);

            Assert.AreEqual("PLANET1", nearest.InstanceID, "Should return the nearest planet");
        }

        [Test]
        public void GetNearestOwnedPlanetTo_MultipleOwnedPlanets_ReturnsClosestPlanet()
        {
            _planet1.PositionX = 20;
            _planet1.PositionY = 0;
            _planet2.PositionX = 5;
            _planet2.PositionY = 0;

            _faction.AddOwnedUnit(_planet1);
            _faction.AddOwnedUnit(_planet2);

            Planet nearest = _faction.GetNearestOwnedPlanetTo(new Point(0, 0));

            Assert.AreEqual("PLANET2", nearest.InstanceID, "Should return the nearest planet");
        }

        [Test]
        public void GetNearestOwnedPlanetTo_WithExcludedClosestPlanet_ReturnsNextClosestPlanet()
        {
            _planet1.PositionX = 5;
            _planet1.PositionY = 0;
            _planet2.PositionX = 20;
            _planet2.PositionY = 0;

            _faction.AddOwnedUnit(_planet1);
            _faction.AddOwnedUnit(_planet2);

            Planet nearest = _faction.GetNearestOwnedPlanetTo(new Point(0, 0), _planet1);

            Assert.AreEqual("PLANET2", nearest.InstanceID, "Should skip the excluded planet");
        }

        [Test]
        public void GetNearestOwnedPlanetTo_WithStaleOwnershipIndex_ReturnsCurrentOwnerPlanet()
        {
            _planet1.PositionX = 5;
            _planet1.PositionY = 0;
            _planet2.PositionX = 20;
            _planet2.PositionY = 0;

            _faction.AddOwnedUnit(_planet1);
            _faction.AddOwnedUnit(_planet2);
            _planet1.OwnerInstanceID = "FACTION2";

            Planet nearest = _faction.GetNearestOwnedPlanetTo(new Point(0, 0));

            Assert.AreEqual("PLANET2", nearest.InstanceID, "Should ignore stale owned entities");
        }

        [Test]
        public void GetNearestFriendlyPlanetTo_WithNodeNotOnPlanet_ThrowsException()
        {
            Fleet floatingFleet = new Fleet { InstanceID = "FLEET2", OwnerInstanceID = "FACTION1" };

            _faction.AddOwnedUnit(_planet1);

            Assert.Throws<ArgumentException>(
                () => _faction.GetNearestFriendlyPlanetTo(floatingFleet),
                "Should throw exception when node is not on a planet"
            );
        }

        [Test]
        public void GetTotalMaintenanceCost_MixedCompleteAndBuilding_SumsCompleteOnly()
        {
            Regiment completeUnit = new Regiment
            {
                OwnerInstanceID = "FACTION1",
                MaintenanceCost = 10,
                ConstructionCost = 50,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Regiment buildingUnit = new Regiment
            {
                OwnerInstanceID = "FACTION1",
                MaintenanceCost = 7,
                ConstructionCost = 70,
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            _faction.AddOwnedUnit(completeUnit);
            _faction.AddOwnedUnit(buildingUnit);

            Assert.AreEqual(10, _faction.GetTotalMaintenanceCost());
        }

        [Test]
        public void GetTotalInProgressConstructionCost_MixedCompleteAndBuilding_SumsBuildingOnly()
        {
            Regiment completeUnit = new Regiment
            {
                OwnerInstanceID = "FACTION1",
                MaintenanceCost = 10,
                ConstructionCost = 50,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Regiment buildingUnit = new Regiment
            {
                OwnerInstanceID = "FACTION1",
                MaintenanceCost = 7,
                ConstructionCost = 70,
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            _faction.AddOwnedUnit(completeUnit);
            _faction.AddOwnedUnit(buildingUnit);

            Assert.AreEqual(70, _faction.GetTotalInProgressConstructionCost());
        }
    }
}

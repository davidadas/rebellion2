using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Movement;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using GameFleet = Rebellion.Game.Units.Fleet;
using GamePlanetSystem = Rebellion.Game.Galaxy.PlanetSystem;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Status
{
    [TestFixture]
    public class StrategyStatusInfoBuilderTests
    {
        private const string _ownerId = "owner";
        private const string _opponentId = "opponent";

        private GameRoot _game;
        private GamePlanetSystem _system;
        private Planet _planet;
        private GalaxyMapPlanet _mapPlanet;
        private GalaxyMapSector _sector;
        private StrategyStatusInfoBuilder _builder;

        [SetUp]
        public void SetUp()
        {
            _game = new GameRoot(TestConfig.Create()) { CurrentTick = 100 };
            _game.Factions.Add(new Faction { InstanceID = _ownerId });
            _game.Factions.Add(new Faction { InstanceID = _opponentId });
            _game.Summary.PlayerFactionID = _ownerId;

            _system = new GamePlanetSystem { InstanceID = "system", DisplayName = "Core System" };
            _planet = new Planet
            {
                InstanceID = "planet",
                DisplayName = "Corellia",
                OwnerInstanceID = _ownerId,
                IsColonized = true,
                EnergyCapacity = 12,
            };
            _planet.SetPopularSupport(_ownerId, 63);
            _game.AttachNode(_system, _game.GetGalaxyMap());
            _game.AttachNode(_planet, _system);

            _mapPlanet = new GalaxyMapPlanet(_system, _planet, "planet/icon");
            _sector = new GalaxyMapSector(_system, new[] { _mapPlanet });
            _builder = CreateBuilder(_game, _sector);
        }

        [Test]
        public void Constructor_NullGame_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyStatusInfoBuilder(null, Array.Empty<GalaxyMapSector>(), _ => null)
            );
        }

        [Test]
        public void Constructor_NullSectors_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyStatusInfoBuilder(_game, null, _ => null)
            );
        }

        [Test]
        public void Constructor_NullNodeResolver_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyStatusInfoBuilder(_game, Array.Empty<GalaxyMapSector>(), null)
            );
        }

        [Test]
        public void Build_NullTarget_ReturnsNull()
        {
            StrategyStatusInfo info = _builder.Build(null);

            Assert.IsNull(info);
        }

        [Test]
        public void Build_UnsupportedItemWithoutPlanet_ReturnsNull()
        {
            StrategyStatusInfo info = _builder.Build(
                new StrategyStatusTarget(null, _game.GetGalaxyMap())
            );

            Assert.IsNull(info);
        }

        [Test]
        public void Build_Planet_ReturnsPlanetStatus()
        {
            StrategyStatusInfo info = _builder.Build(new StrategyStatusTarget(_mapPlanet, _planet));

            Assert.AreEqual(_ownerId, info.OwnerFactionId);
            Assert.AreEqual("Planet Status", info.Header);
            Assert.AreEqual("Corellia", info.Label);
            Assert.IsTrue(info.CenterImage);
            CollectionAssert.AreEqual(new[] { _planet }, info.ImageItems);
            CollectionAssert.AreEqual(
                new[]
                {
                    "Location:|Core System",
                    "Status:|Active",
                    "Popular Support:|63",
                    "Energy:|12",
                },
                info.Rows.Select(row => row.Left + "|" + row.Right)
            );
        }

        [Test]
        public void Build_PlanetTargetWithoutItem_ReturnsPlanetStatus()
        {
            StrategyStatusInfo info = _builder.Build(new StrategyStatusTarget(_mapPlanet, null));

            Assert.AreEqual("Planet Status", info.Header);
            Assert.AreEqual("Corellia", info.Label);
            CollectionAssert.AreEqual(new[] { _planet }, info.ImageItems);
        }

        [Test]
        public void Build_NeutralPlanet_ReturnsNeutralStatus()
        {
            Planet neutralPlanet = new Planet
            {
                InstanceID = "neutral-planet",
                DisplayName = "Neutral World",
                EnergyCapacity = 4,
            };
            _game.AttachNode(neutralPlanet, _system);
            GalaxyMapPlanet mapPlanet = new GalaxyMapPlanet(_system, neutralPlanet, string.Empty);

            StrategyStatusInfo info = _builder.Build(
                new StrategyStatusTarget(mapPlanet, neutralPlanet)
            );

            Assert.IsNull(info.OwnerFactionId);
            Assert.AreEqual("Neutral", info.Rows.Single(row => row.Left == "Status:").Right);
            Assert.AreEqual("0", info.Rows.Single(row => row.Left == "Popular Support:").Right);
        }

        [Test]
        public void Build_ManufacturingLaneWithoutFacility_ReturnsNoFacilitiesStatus()
        {
            StrategyStatusInfo info = _builder.Build(
                new StrategyStatusTarget(_mapPlanet, null, ManufacturingType.Ship)
            );

            Assert.AreEqual("Ship Construction", info.Header);
            Assert.AreEqual("Shipyards", info.Label);
            Assert.IsFalse(info.CenterImage);
            CollectionAssert.AreEqual(new[] { StatusWindowImage.Shipyard }, info.Images);
            CollectionAssert.AreEqual(
                new[] { "Location:|Corellia", "Status:|No Facilities" },
                info.Rows.Select(row => row.Left + "|" + row.Right)
            );
        }

        [Test]
        public void Build_IdleTroopManufacturingLane_ReturnsIdleTrainingStatus()
        {
            AttachBuilding(
                new Building
                {
                    InstanceID = "training-facility",
                    DisplayName = "Training Facility",
                    OwnerInstanceID = _ownerId,
                    BuildingType = BuildingType.TrainingFacility,
                    ProductionType = ManufacturingType.Troop,
                    ProcessRate = 2,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                }
            );

            StrategyStatusInfo info = _builder.Build(
                new StrategyStatusTarget(_mapPlanet, null, ManufacturingType.Troop)
            );

            Assert.AreEqual("Troops in Training", info.Header);
            Assert.AreEqual("Training Facilities", info.Label);
            CollectionAssert.AreEqual(new[] { StatusWindowImage.Training }, info.Images);
            Assert.AreEqual("Idle", info.Rows.Single(row => row.Left == "Status:").Right);
            Assert.IsFalse(info.Rows.Any(row => row.Left == "Items to Build:"));
        }

        [Test]
        public void Build_QueuedBuildingManufacturingLane_ReturnsQueueCompletionStatus()
        {
            AttachBuilding(
                new Building
                {
                    InstanceID = "construction-facility",
                    DisplayName = "Construction Yard",
                    OwnerInstanceID = _ownerId,
                    BuildingType = BuildingType.ConstructionFacility,
                    ProductionType = ManufacturingType.Building,
                    ProcessRate = 2,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                }
            );
            _planet.ManufacturingQueue[ManufacturingType.Building] = new List<IManufacturable>
            {
                new Building
                {
                    OwnerInstanceID = _ownerId,
                    ConstructionCost = 25,
                    ManufacturingProgress = 5,
                },
            };

            StrategyStatusInfo info = _builder.Build(
                new StrategyStatusTarget(_mapPlanet, null, ManufacturingType.Building)
            );

            Assert.AreEqual("Facilities Under Construction", info.Header);
            Assert.AreEqual("Construction Yards", info.Label);
            CollectionAssert.AreEqual(new[] { StatusWindowImage.Construction }, info.Images);
            Assert.AreEqual("Building", info.Rows.Single(row => row.Left == "Status:").Right);
            Assert.AreEqual("1", info.Rows.Single(row => row.Left == "Items to Build:").Right);
            Assert.AreEqual(
                "140",
                info.Rows.Single(row => row.Left == "Estimated Day of Completion:").Right
            );
        }

        [Test]
        public void Build_QueuedTroopManufacturingLane_ReturnsTrainingStatus()
        {
            AttachBuilding(
                new Building
                {
                    InstanceID = "training-facility",
                    DisplayName = "Training Facility",
                    OwnerInstanceID = _ownerId,
                    BuildingType = BuildingType.TrainingFacility,
                    ProductionType = ManufacturingType.Troop,
                    ProcessRate = 1,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                }
            );
            _planet.ManufacturingQueue[ManufacturingType.Troop] = new List<IManufacturable>
            {
                new Regiment { OwnerInstanceID = _ownerId, ConstructionCost = 3 },
            };

            StrategyStatusInfo info = _builder.Build(
                new StrategyStatusTarget(_mapPlanet, null, ManufacturingType.Troop)
            );

            Assert.AreEqual("Training", info.Rows.Single(row => row.Left == "Status:").Right);
            Assert.AreEqual(
                "103",
                info.Rows.Single(row => row.Left == "Estimated Day of Completion:").Right
            );
        }

        [Test]
        public void Build_ManufacturingBuilding_ReturnsManufacturingFacilityStatus()
        {
            Building building = new Building
            {
                InstanceID = "shipyard",
                DisplayName = "Orbital Shipyard",
                OwnerInstanceID = _ownerId,
                BuildingType = BuildingType.Shipyard,
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaintenanceCost = 7,
                ProcessRate = 4,
                Bombardment = 2,
            };
            AttachBuilding(building);

            StrategyStatusInfo info = _builder.Build(
                new StrategyStatusTarget(_mapPlanet, building)
            );

            Assert.AreEqual("Manufacturing Status", info.Header);
            Assert.AreEqual("Orbital Shipyard", info.Label);
            CollectionAssert.AreEqual(new[] { building }, info.ImageItems);
            CollectionAssert.AreEqual(
                new[]
                {
                    "Location:|Corellia",
                    "Status:|Active",
                    "Maintenance Cost:|7",
                    "Standard Processing Rate:|4",
                    "Bombardment Value:|2",
                },
                info.Rows.Select(row => row.Left + "|" + row.Right)
            );
        }

        [Test]
        public void Build_DefenseBuilding_ReturnsDefenseFacilityStatus()
        {
            Building building = new Building
            {
                InstanceID = "shield",
                DisplayName = "Shield Generator",
                OwnerInstanceID = _ownerId,
                BuildingType = BuildingType.Defense,
                ManufacturingStatus = ManufacturingStatus.Building,
                MaintenanceCost = 8,
                WeaponStrength = 13,
                ShieldStrength = 21,
                Bombardment = 5,
            };
            AttachBuilding(building);

            StrategyStatusInfo info = _builder.Build(
                new StrategyStatusTarget(_mapPlanet, building)
            );

            Assert.AreEqual("Defense Facility Status", info.Header);
            Assert.AreEqual(
                "Under Construction",
                info.Rows.Single(row => row.Left == "Status:").Right
            );
            Assert.AreEqual("13", info.Rows.Single(row => row.Left == "Weapons Rating:").Right);
            Assert.AreEqual("21", info.Rows.Single(row => row.Left == "Shield Strength:").Right);
            Assert.AreEqual("5", info.Rows.Single(row => row.Left == "Bombardment Defense:").Right);
            Assert.IsFalse(info.Rows.Any(row => row.Left == "Standard Processing Rate:"));
        }

        [Test]
        public void Build_Starfighter_ReturnsCalculatedSquadronRatings()
        {
            Starfighter starfighter = new Starfighter
            {
                InstanceID = "fighter",
                DisplayName = "X-wing Squadron",
                OwnerInstanceID = _ownerId,
                ManufacturingStatus = ManufacturingStatus.Complete,
                CurrentSquadronSize = 8,
                MaxSquadronSize = 12,
                LaserCannon = 3,
                IonCannon = 2,
                Torpedoes = 4,
                MaintenanceCost = 6,
                Hyperdrive = 1,
                ShieldStrength = 9,
                SublightSpeed = 7,
                Agility = 10,
                DetectionRating = 11,
                Bombardment = 5,
                DamagedImagePath = "fighter/damaged",
            };
            _game.AttachNode(starfighter, _planet);

            StrategyStatusInfo info = _builder.Build(
                new StrategyStatusTarget(_mapPlanet, starfighter)
            );

            Assert.AreEqual("Fighter Squadron Status", info.Header);
            Assert.AreEqual("8:12", info.Rows.Single(row => row.Left == "Squadron Size:").Right);
            Assert.AreEqual("24:36", info.Rows.Single(row => row.Left == "Laser Rating:").Right);
            Assert.AreEqual("16:24", info.Rows.Single(row => row.Left == "Ion Cannon:").Right);
            Assert.AreEqual("32:48", info.Rows.Single(row => row.Left == "Torpedoes:").Right);
            CollectionAssert.AreEqual(new[] { starfighter }, info.StatusImageItems);
            CollectionAssert.IsEmpty(info.Images);
        }

        [Test]
        public void Build_StarfighterWithNegativeSquadronValues_ClampsRatiosToZero()
        {
            Starfighter starfighter = new Starfighter
            {
                DisplayName = "Damaged Squadron",
                OwnerInstanceID = _ownerId,
                CurrentSquadronSize = -2,
                MaxSquadronSize = -1,
                LaserCannon = 3,
                IonCannon = 2,
                Torpedoes = 4,
            };

            StrategyStatusInfo info = _builder.Build(
                new StrategyStatusTarget(_mapPlanet, starfighter)
            );

            Assert.AreEqual("0:0", info.Rows.Single(row => row.Left == "Squadron Size:").Right);
            Assert.AreEqual("0:0", info.Rows.Single(row => row.Left == "Laser Rating:").Right);
            Assert.AreEqual("0:0", info.Rows.Single(row => row.Left == "Ion Cannon:").Right);
            Assert.AreEqual("0:0", info.Rows.Single(row => row.Left == "Torpedoes:").Right);
        }

        [Test]
        public void Build_Regiment_ReturnsTrooperStatus()
        {
            Regiment regiment = new Regiment
            {
                InstanceID = "regiment",
                DisplayName = "Assault Regiment",
                OwnerInstanceID = _ownerId,
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaintenanceCost = 4,
                AttackRating = 12,
                DefenseRating = 15,
                BombardmentDefense = 9,
                DetectionRating = 6,
            };
            _game.AttachNode(regiment, _planet);

            StrategyStatusInfo info = _builder.Build(
                new StrategyStatusTarget(_mapPlanet, regiment)
            );

            Assert.AreEqual("Trooper Regiment Status", info.Header);
            Assert.AreEqual(
                "Awaiting Orders",
                info.Rows.Single(row => row.Left == "Status:").Right
            );
            Assert.AreEqual("12", info.Rows.Single(row => row.Left == "Attack Strength:").Right);
            Assert.AreEqual("15", info.Rows.Single(row => row.Left == "Defense Strength:").Right);
            Assert.AreEqual("9", info.Rows.Single(row => row.Left == "Bombardment Value:").Right);
            Assert.AreEqual("6", info.Rows.Single(row => row.Left == "Detection Value:").Right);
        }

        [Test]
        public void Build_SpecialForces_ReturnsMissionRatings()
        {
            SpecialForces specialForces = new SpecialForces
            {
                InstanceID = "special-forces",
                DisplayName = "Infiltrator Team",
                OwnerInstanceID = _ownerId,
                ManufacturingStatus = ManufacturingStatus.Building,
                MaintenanceCost = 5,
            };
            specialForces.SetBaseRating(OfficerRating.Diplomacy, 11);
            specialForces.SetBaseRating(OfficerRating.Espionage, 22);
            specialForces.SetBaseRating(OfficerRating.Combat, 33);
            specialForces.SetBaseRating(OfficerRating.Leadership, 44);
            _game.AttachNode(specialForces, _planet);

            StrategyStatusInfo info = _builder.Build(
                new StrategyStatusTarget(_mapPlanet, specialForces)
            );

            Assert.AreEqual("Trooper Regiment Status", info.Header);
            Assert.AreEqual("Training", info.Rows.Single(row => row.Left == "Status:").Right);
            Assert.AreEqual("11", info.Rows.Single(row => row.Left == "Diplomacy Rating:").Right);
            Assert.AreEqual("22", info.Rows.Single(row => row.Left == "Espionage Rating:").Right);
            Assert.AreEqual("33", info.Rows.Single(row => row.Left == "Combat Rating:").Right);
            Assert.AreEqual("44", info.Rows.Single(row => row.Left == "Leadership Rating:").Right);
        }

        [Test]
        public void Build_Officer_ReturnsCommandForceResearchAndRankStatus()
        {
            Officer officer = new Officer
            {
                InstanceID = "officer",
                DisplayName = "Commander Antilles",
                OwnerInstanceID = _ownerId,
                IsJedi = true,
                ForceValue = _game.Config.Jedi.RankLabelForceMaster,
                ShipResearch = 1,
                TroopResearch = 0,
                FacilityResearch = 2,
                AllowedRanks = new[] { OfficerRank.Admiral, OfficerRank.Commander },
            };
            officer.SetBaseRating(OfficerRating.Diplomacy, 10);
            officer.SetBaseRating(OfficerRating.Espionage, 20);
            officer.SetBaseRating(OfficerRating.Combat, 30);
            officer.SetBaseRating(OfficerRating.Leadership, 40);
            _game.AttachNode(officer, _planet);

            StrategyStatusInfo info = _builder.Build(new StrategyStatusTarget(_mapPlanet, officer));

            Assert.AreEqual("Character Status", info.Header);
            Assert.AreEqual("None", info.Rows.Single(row => row.Left == "Commanding:").Right);
            Assert.AreEqual(
                "Awaiting Orders",
                info.Rows.Single(row => row.Left == "Status:").Right
            );
            Assert.AreEqual(
                "Jedi Master",
                info.Rows.Single(row => row.Left == "Force Ranking:").Right
            );
            Assert.AreEqual("Yes", info.Rows.Single(row => row.Left == "Ship Design:").Right);
            Assert.AreEqual("No", info.Rows.Single(row => row.Left == "Troop Training:").Right);
            Assert.AreEqual("Yes", info.Rows.Single(row => row.Left == "Facility Design:").Right);
            Assert.AreEqual("Yes", info.Rows.Single(row => row.Left == "Admiral:").Right);
            Assert.AreEqual("No", info.Rows.Single(row => row.Left == "General:").Right);
            Assert.AreEqual("Yes", info.Rows.Single(row => row.Left == "Commander:").Right);
        }

        [Test]
        public void Build_CapturedOfficer_ReturnsCapturedOverlayStatus()
        {
            Officer officer = new Officer
            {
                InstanceID = "captured-officer",
                DisplayName = "Captured Officer",
                OwnerInstanceID = _ownerId,
                IsCaptured = true,
                CapturedOverlayImagePath = "officer/captured-overlay",
            };
            _game.AttachNode(officer, _planet);

            StrategyStatusInfo info = _builder.Build(new StrategyStatusTarget(_mapPlanet, officer));

            Assert.AreEqual("Captured", info.Rows.Single(row => row.Left == "Status:").Right);
            CollectionAssert.AreEqual(new[] { officer }, info.OverlayImageItems);
            CollectionAssert.AreEqual(new[] { officer }, info.ImageItems);
        }

        [Test]
        public void Build_MovingOfficerWithTransitImage_ReturnsEntityStatusImage()
        {
            Officer officer = new Officer
            {
                DisplayName = "Traveling Officer",
                OwnerInstanceID = _ownerId,
                Movement = new MovementState { TransitTicks = 9, TicksElapsed = 4 },
                InTransitImagePath = "officer/in-transit",
            };

            StrategyStatusInfo info = _builder.Build(new StrategyStatusTarget(_mapPlanet, officer));

            Assert.AreEqual("Enroute", info.Rows.Single(row => row.Left == "Status:").Right);
            Assert.AreEqual(
                "Day 105",
                info.Rows.Single(row => row.Left == "ETA Destination:").Right
            );
            CollectionAssert.AreEqual(new[] { officer }, info.StatusImageItems);
            CollectionAssert.IsEmpty(info.Images);
        }

        [Test]
        public void Build_MovingOfficerWithoutTransitImage_ReturnsGenericEnrouteImage()
        {
            Officer officer = new Officer
            {
                DisplayName = "Traveling Officer",
                OwnerInstanceID = _ownerId,
                Movement = new MovementState { TransitTicks = 9, TicksElapsed = 4 },
            };

            StrategyStatusInfo info = _builder.Build(new StrategyStatusTarget(_mapPlanet, officer));

            CollectionAssert.AreEqual(new[] { StatusWindowImage.Enroute }, info.Images);
            CollectionAssert.IsEmpty(info.StatusImageItems);
        }

        [Test]
        public void Build_Fleet_ReturnsMovementDamageCommandAndCapacityStatus()
        {
            GameFleet fleet = new GameFleet
            {
                InstanceID = "fleet",
                DisplayName = "First Fleet",
                OwnerInstanceID = _ownerId,
                Movement = new MovementState { TransitTicks = 12, TicksElapsed = 5 },
            };
            _game.AttachNode(fleet, _planet);
            CapitalShip damagedShip = new CapitalShip
            {
                InstanceID = "damaged-ship",
                DisplayName = "Carrier",
                OwnerInstanceID = _ownerId,
                MaxHullStrength = 100,
                CurrentHullStrength = 60,
                Hyperdrive = 2,
                StarfighterCapacity = 3,
                RegimentCapacity = 2,
            };
            CapitalShip escort = new CapitalShip
            {
                InstanceID = "escort",
                DisplayName = "Escort",
                OwnerInstanceID = _ownerId,
                MaxHullStrength = 80,
                CurrentHullStrength = 80,
                StarfighterCapacity = 1,
                RegimentCapacity = 1,
            };
            _game.AttachNode(damagedShip, fleet);
            _game.AttachNode(escort, fleet);
            Officer admiral = new Officer
            {
                InstanceID = "admiral",
                DisplayName = "Admiral Ackbar",
                OwnerInstanceID = _ownerId,
                CurrentRank = OfficerRank.Admiral,
            };
            SpecialForces specialForces = new SpecialForces
            {
                InstanceID = "fleet-special-forces",
                OwnerInstanceID = _ownerId,
            };
            _game.AttachNode(admiral, damagedShip);
            _game.AttachNode(specialForces, damagedShip);
            _game.AttachNode(
                new Starfighter { InstanceID = "fleet-fighter", OwnerInstanceID = _ownerId },
                damagedShip
            );
            _game.AttachNode(
                new Regiment { InstanceID = "fleet-regiment", OwnerInstanceID = _ownerId },
                damagedShip
            );

            StrategyStatusInfo info = _builder.Build(new StrategyStatusTarget(_mapPlanet, fleet));

            CollectionAssert.AreEqual(
                new[]
                {
                    StatusWindowImage.FleetBannerEnroute,
                    StatusWindowImage.FleetBannerDamaged,
                    StatusWindowImage.FleetBanner,
                },
                info.Images
            );
            Assert.AreEqual("Enroute", info.Rows.Single(row => row.Left == "Status:").Right);
            Assert.AreEqual(
                "Day 107",
                info.Rows.Single(row => row.Left == "ETA Destination:").Right
            );
            Assert.AreEqual(
                "Admiral Ackbar",
                info.Rows.Single(row => row.Left == "Admiral:").Right
            );
            Assert.AreEqual("Not Assigned", info.Rows.Single(row => row.Left == "General:").Right);
            Assert.AreEqual(
                "Not Assigned",
                info.Rows.Single(row => row.Left == "Commander:").Right
            );
            Assert.AreEqual("2", info.Rows.Single(row => row.Left == "Number of Ships:").Right);
            CollectionAssert.AreEqual(
                new[] { "4", "1" },
                info.Rows.Where(row => row.Left == "Fighter Squadrons:").Select(row => row.Right)
            );
            CollectionAssert.AreEqual(
                new[] { "3", "1" },
                info.Rows.Where(row => row.Left == "Trooper Regiments:").Select(row => row.Right)
            );
            Assert.AreEqual("2", info.Rows.Single(row => row.Left == "Personnel:").Right);
            Assert.AreEqual("1", info.Rows.Single(row => row.Left == "Damaged Ships:").Right);
            Assert.AreEqual("Yes", info.Rows.Single(row => row.Left == "Hyperdrive Rating:").Right);
        }

        [Test]
        public void Build_CapitalShip_ReturnsCompleteShipStatus()
        {
            GameFleet fleet = new GameFleet
            {
                InstanceID = "fleet",
                DisplayName = "Second Fleet",
                OwnerInstanceID = _ownerId,
            };
            _game.AttachNode(fleet, _planet);
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "capital-ship",
                DisplayName = "Assault Frigate",
                OwnerInstanceID = _ownerId,
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaintenanceCost = 12,
                StarfighterCapacity = 2,
                RegimentCapacity = 3,
                MaxHullStrength = 360,
                CurrentHullStrength = 300,
                DamageControl = 5,
                Hyperdrive = 100,
                ShieldRechargeRate = 7,
                MaxShieldStrength = 80,
                TractorBeamPower = 9,
                SublightSpeed = 11,
                Maneuverability = 13,
                DetectionRating = 15,
                WeaponRecharge = 17,
                Bombardment = 19,
                DamagedImagePath = "ship/damaged",
            };
            _game.AttachNode(ship, fleet);
            _game.AttachNode(
                new Starfighter { InstanceID = "ship-fighter", OwnerInstanceID = _ownerId },
                ship
            );
            _game.AttachNode(
                new Regiment { InstanceID = "ship-regiment", OwnerInstanceID = _ownerId },
                ship
            );
            _game.AttachNode(
                new Officer { InstanceID = "ship-officer", OwnerInstanceID = _ownerId },
                ship
            );

            StrategyStatusInfo info = _builder.Build(new StrategyStatusTarget(_mapPlanet, ship));

            Assert.AreEqual("Capital Ship Status", info.Header);
            Assert.AreEqual("Assault Frigate", info.Rows.Single(row => row.Left == "Class:").Right);
            Assert.AreEqual("Second Fleet", info.Rows.Single(row => row.Left == "Fleet:").Right);
            Assert.AreEqual("Active", info.Rows.Single(row => row.Left == "Status:").Right);
            Assert.AreEqual("Yes", info.Rows.Single(row => row.Left == "Ship Damaged:").Right);
            Assert.AreEqual("300:360", info.Rows.Single(row => row.Left == "Hull Value:").Right);
            Assert.AreEqual(
                "5",
                info.Rows.Single(row => row.Left == "Damage Control Rating:").Right
            );
            Assert.AreEqual(
                "7",
                info.Rows.Single(row => row.Left == "Shield Recharge Rate:").Right
            );
            Assert.AreEqual(
                "80",
                info.Rows.Single(row => row.Left == "Max Shield Strength:").Right
            );
            Assert.AreEqual("9", info.Rows.Single(row => row.Left == "Tractor Beam Power:").Right);
            Assert.AreEqual(
                "11",
                info.Rows.Single(row => row.Left == "Sub Light Engine Rating:").Right
            );
            Assert.AreEqual("13", info.Rows.Single(row => row.Left == "Maneuverability:").Right);
            Assert.AreEqual("15", info.Rows.Single(row => row.Left == "Detection Rating:").Right);
            Assert.AreEqual(
                "17",
                info.Rows.Single(row => row.Left == "Weapons Recharge Rate:").Right
            );
            Assert.AreEqual(
                "19",
                info.Rows.Single(row => row.Left == "Bombardment Modifier:").Right
            );
            CollectionAssert.AreEqual(new[] { ship }, info.StatusImageItems);
        }

        [Test]
        public void Build_CapitalShipCarriedByMovingFleet_ReturnsTransitStatusAndArrivalDay()
        {
            GameFleet fleet = new GameFleet
            {
                InstanceID = "moving-fleet",
                DisplayName = "Moving Fleet",
                OwnerInstanceID = _ownerId,
                Movement = new MovementState { TransitTicks = 12, TicksElapsed = 5 },
            };
            _game.AttachNode(fleet, _planet);
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "carried-ship",
                DisplayName = "Carried Ship",
                OwnerInstanceID = _ownerId,
                ManufacturingStatus = ManufacturingStatus.Complete,
                InTransitImagePath = "ship/in-transit",
            };
            _game.AttachNode(ship, fleet);

            StrategyStatusInfo info = _builder.Build(new StrategyStatusTarget(_mapPlanet, ship));

            Assert.AreEqual("Enroute", info.Rows.Single(row => row.Left == "Status:").Right);
            Assert.AreEqual(
                "Day 107",
                info.Rows.Single(row => row.Left == "ETA Destination:").Right
            );
            CollectionAssert.AreEqual(new[] { ship }, info.StatusImageItems);
            CollectionAssert.IsEmpty(info.Images);
        }

        [Test]
        public void Build_MissionWithExplicitTarget_ReturnsTargetAndTeamCounts()
        {
            Building targetBuilding = new Building
            {
                InstanceID = "mission-target",
                DisplayName = "Shield Generator",
                OwnerInstanceID = _ownerId,
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            AttachBuilding(targetBuilding);
            SabotageMission mission = new SabotageMission
            {
                InstanceID = "mission",
                DisplayName = "Sabotage Mission",
                OwnerInstanceID = _ownerId,
                LocationInstanceID = _planet.InstanceID,
                SabotageTargetInstanceID = targetBuilding.InstanceID,
                MainParticipants = new List<IMissionParticipant>
                {
                    new Officer
                    {
                        Movement = new MovementState { TransitTicks = 9, TicksElapsed = 4 },
                    },
                    new SpecialForces(),
                },
                DecoyParticipants = new List<IMissionParticipant> { new Officer() },
            };
            _game.AttachNode(mission, _planet);

            StrategyStatusInfo info = _builder.Build(new StrategyStatusTarget(_mapPlanet, mission));

            Assert.AreEqual("Mission Status", info.Header);
            Assert.AreEqual(
                "Shield Generator",
                info.Rows.Single(row => row.Left == "Target:").Right
            );
            Assert.AreEqual("3", info.Rows.Single(row => row.Left == "Team Size:").Right);
            Assert.AreEqual("1", info.Rows.Single(row => row.Left == "Decoys:").Right);
            Assert.AreEqual(
                "Day 105",
                info.Rows.Single(row => row.Left == "ETA Destination:").Right
            );
        }

        [Test]
        public void Build_MissionWithoutExplicitTarget_ReturnsLocation()
        {
            StubMission mission = new StubMission(_ownerId, _planet.InstanceID)
            {
                InstanceID = "mission",
                DisplayName = "Reconnaissance Mission",
            };
            _game.AttachNode(mission, _planet);

            StrategyStatusInfo info = _builder.Build(new StrategyStatusTarget(_mapPlanet, mission));

            Assert.AreEqual("Corellia", info.Rows.Single(row => row.Left == "Target:").Right);
            Assert.AreEqual("0", info.Rows.Single(row => row.Left == "Team Size:").Right);
            Assert.AreEqual("0", info.Rows.Single(row => row.Left == "Decoys:").Right);
        }

        private void AttachBuilding(Building building)
        {
            _game.AttachNode(building, _planet);
        }

        private static StrategyStatusInfoBuilder CreateBuilder(
            GameRoot game,
            GalaxyMapSector sector
        )
        {
            return new StrategyStatusInfoBuilder(
                game,
                new[] { sector },
                instanceId => game.GetSceneNodeByInstanceID<ISceneNode>(instanceId)
            );
        }
    }
}

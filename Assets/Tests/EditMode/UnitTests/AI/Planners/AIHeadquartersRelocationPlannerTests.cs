using System.Linq;
using NUnit.Framework;
using Rebellion.AI;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Planners
{
    [TestFixture]
    public sealed class AIHeadquartersRelocationPlannerTests
    {
        [Test]
        public void Plan_UnobservedIncomingSuperiorFleet_DoesNotRelocateHeadquarters()
        {
            AITurnContext context = CreateThreatenedHeadquarters(
                revealThreat: false,
                threatStrength: 200,
                defenseStrength: 100,
                out _,
                out _
            );

            Assert.IsEmpty(new AIHeadquartersRelocationPlanner().Plan(context));
        }

        [Test]
        public void Plan_ObservedIncomingSuperiorFleet_RelocatesHeadquarters()
        {
            AITurnContext context = CreateThreatenedHeadquarters(
                revealThreat: true,
                threatStrength: 200,
                defenseStrength: 100,
                out _,
                out Planet destination
            );

            AIHeadquartersRelocationProposal proposal = new AIHeadquartersRelocationPlanner()
                .Plan(context)
                .Cast<AIHeadquartersRelocationProposal>()
                .Single();

            Assert.AreSame(destination, proposal.Destination);
            Assert.IsNull(proposal.BuildingToRemove);
            Assert.Greater(proposal.ThreatStrength, proposal.DefenseStrength);
        }

        [Test]
        public void Plan_CapturedHeadquartersAtOrigin_RelocatesIdentifiedHeadquarters()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AddMobileHeadquarters(game, empire, sector);
            origin.EnergyCapacity = 2;
            Planet destination = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "destination",
                empire.InstanceID,
                energyCapacity: 2
            );
            Building headquarters = game.GetSceneNodeByInstanceID<Building>(
                empire.HeadquartersUnitInstanceID
            );
            Building capturedHeadquarters = AddBuilding(
                game,
                origin,
                "captured-headquarters",
                BuildingType.Headquarters
            );
            game.MoveNode(headquarters, destination);
            game.MoveNode(headquarters, origin);
            AddFleet(game, origin, "incoming", rebels.InstanceID, 200, isMoving: true);
            AddFleet(game, origin, "defender", empire.InstanceID, 100, isMoving: false);
            AITestSceneBuilder.RevealPlanet(game, empire, origin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIHeadquartersRelocationProposal proposal = new AIHeadquartersRelocationPlanner()
                .Plan(context)
                .Cast<AIHeadquartersRelocationProposal>()
                .Single();

            Assert.AreSame(headquarters, proposal.Headquarters);
            Assert.AreNotSame(capturedHeadquarters, proposal.Headquarters);
        }

        [Test]
        public void Plan_CombinedSameOwnerIncomingThreatExceedsDefense_RelocatesHeadquarters()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AddMobileHeadquarters(game, empire, sector);
            Planet destination = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "destination",
                empire.InstanceID,
                energyCapacity: 1
            );
            AddFleet(game, origin, "incoming-a", rebels.InstanceID, 60, isMoving: true);
            AddFleet(game, origin, "incoming-b", rebels.InstanceID, 70, isMoving: true);
            AddFleet(game, origin, "defender", empire.InstanceID, 100, isMoving: false);
            AITestSceneBuilder.RevealPlanet(game, empire, origin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIHeadquartersRelocationProposal proposal = new AIHeadquartersRelocationPlanner()
                .Plan(context)
                .Cast<AIHeadquartersRelocationProposal>()
                .Single();

            Assert.AreSame(destination, proposal.Destination);
            Assert.AreEqual(130, proposal.ThreatStrength);
            Assert.AreEqual(100, proposal.DefenseStrength);
        }

        [Test]
        public void Plan_ObservedIncomingDefeatableFleet_DoesNotRelocateHeadquarters()
        {
            AITurnContext context = CreateThreatenedHeadquarters(
                revealThreat: true,
                threatStrength: 100,
                defenseStrength: 200,
                out _,
                out _
            );

            Assert.IsEmpty(new AIHeadquartersRelocationPlanner().Plan(context));
        }

        [Test]
        public void Plan_ThreatenedHeadquartersWithDefenseAndEspionage_AssignsBothToRelocation()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AddMobileHeadquarters(game, empire, sector);
            AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "destination",
                empire.InstanceID,
                energyCapacity: 1
            );
            AddFleet(game, origin, "incoming", rebels.InstanceID, 200, isMoving: true);
            Fleet defender = AddFleet(
                game,
                origin,
                "defender",
                empire.InstanceID,
                100,
                isMoving: false
            );
            defender.RoleType = FleetRoleType.Battle;
            defender.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Defend,
                TargetPlanetId = origin.InstanceID,
            };
            SpecialForces agent = AddEspionageAgent(game, origin, empire.InstanceID);
            AITestSceneBuilder.RevealPlanet(game, empire, origin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIHeadquartersRelocationProposal proposal = new AIHeadquartersRelocationPlanner()
                .Plan(context)
                .Cast<AIHeadquartersRelocationProposal>()
                .Single();

            CollectionAssert.AreEqual(new[] { defender }, proposal.DefensiveFleets);
            Assert.AreSame(agent, proposal.EspionageAgent);
            Assert.IsNull(proposal.EspionageMission);
        }

        [Test]
        public void Plan_SafestPlanetIsFull_RemovesFacilityInsteadOfChoosingHostileSector()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector originSector = AITestSceneBuilder.AddSector(game, "origin-sector");
            PlanetSector safeSector = AITestSceneBuilder.AddSector(game, "safe-sector");
            PlanetSector hostileSector = AITestSceneBuilder.AddSector(game, "hostile-sector");
            Planet origin = AddMobileHeadquarters(game, empire, originSector);
            Planet safePlanet = AITestSceneBuilder.AddPlanet(
                game,
                safeSector,
                "safe",
                empire.InstanceID,
                energyCapacity: 1
            );
            Building removableDefense = AddBuilding(
                game,
                safePlanet,
                "removable-defense",
                BuildingType.Defense
            );
            AITestSceneBuilder.AddPlanet(
                game,
                hostileSector,
                "riskier",
                empire.InstanceID,
                energyCapacity: 2
            );
            Planet knownEnemy = AITestSceneBuilder.AddPlanet(
                game,
                hostileSector,
                "known-enemy",
                rebels.InstanceID
            );
            AddFleet(game, origin, "incoming", rebels.InstanceID, 200, isMoving: true);
            AddFleet(game, origin, "defender", empire.InstanceID, 100, isMoving: false);
            AITestSceneBuilder.RevealPlanet(game, empire, origin);
            AITestSceneBuilder.RevealPlanet(game, empire, knownEnemy);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIHeadquartersRelocationProposal proposal = new AIHeadquartersRelocationPlanner()
                .Plan(context)
                .Cast<AIHeadquartersRelocationProposal>()
                .Single();

            Assert.AreSame(safePlanet, proposal.Destination);
            Assert.AreSame(removableDefense, proposal.BuildingToRemove);
        }

        [Test]
        public void Plan_EquallySafeFullPlanets_PreservesRequiredProduction()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.Selection.MaintenanceHeadroomTarget = 25;
            game.Config.AI.Selection.RefinedMaterialEconomyWarningPercent = 40;
            empire.RefinedMaterialStockpile = 100;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AddMobileHeadquarters(game, empire, sector);
            Planet productionPlanet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "production",
                empire.InstanceID,
                energyCapacity: 1
            );
            Building requiredShipyard = AddBuilding(
                game,
                productionPlanet,
                "required-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Planet resourcePlanet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "resources",
                empire.InstanceID,
                energyCapacity: 4,
                rawResourceNodes: 2
            );
            AddBuilding(game, resourcePlanet, "mine-a", BuildingType.Mine, ManufacturingType.None);
            AddBuilding(game, resourcePlanet, "mine-b", BuildingType.Mine, ManufacturingType.None);
            AddBuilding(
                game,
                resourcePlanet,
                "refinery-a",
                BuildingType.Refinery,
                ManufacturingType.None
            );
            AddBuilding(
                game,
                resourcePlanet,
                "refinery-b",
                BuildingType.Refinery,
                ManufacturingType.None
            );
            AddFleet(game, origin, "incoming", rebels.InstanceID, 200, isMoving: true);
            AddFleet(game, origin, "defender", empire.InstanceID, 100, isMoving: false);
            AITestSceneBuilder.RevealPlanet(game, empire, origin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            AIHeadquartersRelocationProposal proposal = new AIHeadquartersRelocationPlanner()
                .Plan(context)
                .Cast<AIHeadquartersRelocationProposal>()
                .Single();

            Assert.AreSame(resourcePlanet, proposal.Destination);
            Assert.AreNotSame(requiredShipyard, proposal.BuildingToRemove);
            Assert.That(
                proposal.BuildingToRemove.GetBuildingType(),
                Is.EqualTo(BuildingType.Mine).Or.EqualTo(BuildingType.Refinery)
            );
        }

        /// <summary>
        /// Creates a mobile headquarters with one alternate destination and an incoming fleet.
        /// </summary>
        /// <param name="revealThreat">Whether the incoming fleet is recorded in intelligence.</param>
        /// <param name="threatStrength">Incoming fleet combat strength.</param>
        /// <param name="defenseStrength">Ready local fleet combat strength.</param>
        /// <param name="origin">Receives the headquarters planet.</param>
        /// <param name="destination">Receives the alternate owned planet.</param>
        /// <returns>The configured AI turn context.</returns>
        private static AITurnContext CreateThreatenedHeadquarters(
            bool revealThreat,
            int threatStrength,
            int defenseStrength,
            out Planet origin,
            out Planet destination
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            origin = AddMobileHeadquarters(game, empire, sector);
            destination = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "destination",
                empire.InstanceID,
                energyCapacity: 1
            );
            AddFleet(game, origin, "incoming", rebels.InstanceID, threatStrength, isMoving: true);
            AddFleet(game, origin, "defender", empire.InstanceID, defenseStrength, isMoving: false);
            if (revealThreat)
                AITestSceneBuilder.RevealPlanet(game, empire, origin);

            return AITestSceneBuilder.CreateContext(game, empire);
        }

        /// <summary>
        /// Adds a planet containing a completed mobile headquarters.
        /// </summary>
        /// <param name="game">The game graph.</param>
        /// <param name="faction">The headquarters faction.</param>
        /// <param name="sector">The containing sector.</param>
        /// <returns>The headquarters planet.</returns>
        private static Planet AddMobileHeadquarters(
            GameRoot game,
            Faction faction,
            PlanetSector sector
        )
        {
            faction.Settings.Headquarters = new HeadquartersSettings
            {
                FacilityTypeID = "mobile-headquarters",
                IsMobile = true,
            };
            Planet origin = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "headquarters-planet",
                faction.InstanceID,
                energyCapacity: 1
            );
            origin.IsHeadquarters = true;
            faction.HQInstanceID = origin.InstanceID;
            Building headquarters = AddBuilding(
                game,
                origin,
                "mobile-headquarters",
                BuildingType.Headquarters
            );
            faction.HeadquartersUnitInstanceID = headquarters.InstanceID;
            return origin;
        }

        /// <summary>
        /// Adds a completed owned building to a planet.
        /// </summary>
        /// <param name="game">The game graph.</param>
        /// <param name="planet">The destination planet.</param>
        /// <param name="instanceId">The building instance identifier.</param>
        /// <param name="buildingType">The building type.</param>
        /// <param name="productionType">The manufacturing lane supplied by the building.</param>
        /// <returns>The attached building.</returns>
        private static Building AddBuilding(
            GameRoot game,
            Planet planet,
            string instanceId,
            BuildingType buildingType,
            ManufacturingType productionType = ManufacturingType.Building
        )
        {
            Building building = AITestSceneBuilder.CreateBuildingTemplate(
                instanceId,
                buildingType,
                productionType
            );
            building.OwnerInstanceID = planet.OwnerInstanceID;
            game.AttachNode(building, planet);
            return building;
        }

        /// <summary>
        /// Adds a fleet and one capital ship to a planet.
        /// </summary>
        /// <param name="game">The game graph.</param>
        /// <param name="planet">The fleet's current or target planet.</param>
        /// <param name="instanceId">The fleet instance identifier.</param>
        /// <param name="ownerInstanceId">The owning faction identifier.</param>
        /// <param name="combatStrength">The capital ship combat strength.</param>
        /// <param name="isMoving">Whether the fleet is inbound.</param>
        /// <returns>The attached fleet.</returns>
        private static Fleet AddFleet(
            GameRoot game,
            Planet planet,
            string instanceId,
            string ownerInstanceId,
            int combatStrength,
            bool isMoving
        )
        {
            Fleet fleet = EntityFactory.CreateFleet(instanceId, ownerInstanceId);
            if (isMoving)
                fleet.Movement = new MovementState { TransitTicks = 10 };
            game.AttachNode(fleet, planet);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    $"{instanceId}-ship",
                    ownerInstanceId,
                    combatStrength
                ),
                fleet
            );
            return fleet;
        }

        /// <summary>
        /// Adds an available espionage special-forces unit to a planet.
        /// </summary>
        /// <param name="game">The game graph.</param>
        /// <param name="planet">The unit's planet.</param>
        /// <param name="ownerInstanceId">The owning faction identifier.</param>
        /// <returns>The attached espionage unit.</returns>
        private static SpecialForces AddEspionageAgent(
            GameRoot game,
            Planet planet,
            string ownerInstanceId
        )
        {
            SpecialForces agent = new SpecialForces
            {
                InstanceID = "headquarters-agent",
                OwnerInstanceID = ownerInstanceId,
                ManufacturingStatus = ManufacturingStatus.Complete,
                AllowedMissionTypeIDs = new() { MissionTypeIDs.Espionage },
            };
            game.AttachNode(agent, planet);
            return agent;
        }
    }
}

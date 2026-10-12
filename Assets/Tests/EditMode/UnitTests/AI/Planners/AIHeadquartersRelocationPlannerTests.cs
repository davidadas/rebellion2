using System.Linq;
using NUnit.Framework;
using Rebellion.AI;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
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
            AddBuilding(game, origin, "mobile-headquarters", BuildingType.Headquarters);
            return origin;
        }

        /// <summary>
        /// Adds a completed owned building to a planet.
        /// </summary>
        /// <param name="game">The game graph.</param>
        /// <param name="planet">The destination planet.</param>
        /// <param name="instanceId">The building instance identifier.</param>
        /// <param name="buildingType">The building type.</param>
        /// <returns>The attached building.</returns>
        private static Building AddBuilding(
            GameRoot game,
            Planet planet,
            string instanceId,
            BuildingType buildingType
        )
        {
            Building building = AITestSceneBuilder.CreateBuildingTemplate(instanceId, buildingType);
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
    }
}

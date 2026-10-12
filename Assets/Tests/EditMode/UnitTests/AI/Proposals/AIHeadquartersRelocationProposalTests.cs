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

namespace Rebellion.Tests.AI.Proposals
{
    [TestFixture]
    public sealed class AIHeadquartersRelocationProposalTests
    {
        [Test]
        public void Execute_FullDestination_ScrapsFacilityAndRelocatesHeadquarters()
        {
            AITurnContext context = CreateContext(
                unfinishedDestinationBuilding: false,
                out Planet origin,
                out Planet destination,
                out Building headquarters,
                out Building removableBuilding
            );
            AIHeadquartersRelocationProposal proposal = new AIHeadquartersRelocationPlanner()
                .Plan(context)
                .Cast<AIHeadquartersRelocationProposal>()
                .Single();

            proposal.Execute(context);

            Assert.IsNull(
                context.Game.GetSceneNodeByInstanceID<Building>(removableBuilding.InstanceID)
            );
            Assert.AreSame(destination, headquarters.GetParent());
            Assert.IsNotNull(headquarters.Movement);
            Assert.IsFalse(origin.IsHeadquarters);
            Assert.IsNull(context.Faction.HQInstanceID);
        }

        [Test]
        public void Execute_FullDestinationWithQueuedBuilding_CancelsBuildingAndRelocatesHeadquarters()
        {
            AITurnContext context = CreateContext(
                unfinishedDestinationBuilding: true,
                out _,
                out Planet destination,
                out Building headquarters,
                out Building removableBuilding
            );
            AIHeadquartersRelocationProposal proposal = new AIHeadquartersRelocationPlanner()
                .Plan(context)
                .Cast<AIHeadquartersRelocationProposal>()
                .Single();

            proposal.Execute(context);

            Assert.IsNull(
                context.Game.GetSceneNodeByInstanceID<Building>(removableBuilding.InstanceID)
            );
            Assert.AreSame(destination, headquarters.GetParent());
            Assert.IsNotNull(headquarters.Movement);
        }

        /// <summary>
        /// Creates a threatened mobile headquarters and a full safe destination.
        /// </summary>
        /// <param name="unfinishedDestinationBuilding">Whether queued construction occupies the destination slot.</param>
        /// <param name="origin">Receives the headquarters planet.</param>
        /// <param name="destination">Receives the destination planet.</param>
        /// <param name="headquarters">Receives the headquarters building.</param>
        /// <param name="removableBuilding">Receives the destination facility.</param>
        /// <returns>The configured AI turn context.</returns>
        private static AITurnContext CreateContext(
            bool unfinishedDestinationBuilding,
            out Planet origin,
            out Planet destination,
            out Building headquarters,
            out Building removableBuilding
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            empire.Settings.Headquarters = new HeadquartersSettings
            {
                FacilityTypeID = "mobile-headquarters",
                IsMobile = true,
            };
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            origin = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "origin",
                empire.InstanceID,
                energyCapacity: 1
            );
            origin.IsHeadquarters = true;
            empire.HQInstanceID = origin.InstanceID;
            headquarters = AddBuilding(
                game,
                origin,
                "mobile-headquarters",
                BuildingType.Headquarters
            );
            destination = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "destination",
                empire.InstanceID,
                energyCapacity: 1
            );
            AddFleet(game, origin, "incoming", rebels.InstanceID, 200, isMoving: true);
            AddFleet(game, origin, "defender", empire.InstanceID, 100, isMoving: false);
            AITestSceneBuilder.RevealPlanet(game, empire, origin);

            if (!unfinishedDestinationBuilding)
            {
                removableBuilding = AddBuilding(game, destination, "defense", BuildingType.Defense);
                return AITestSceneBuilder.CreateContext(game, empire);
            }

            removableBuilding = AITestSceneBuilder.CreateBuildingTemplate(
                "queued-defense",
                BuildingType.Defense
            );
            removableBuilding.OwnerInstanceID = empire.InstanceID;
            AITurnContext enqueueContext = AITestSceneBuilder.CreateContext(game, empire);
            Assert.IsTrue(
                enqueueContext.Manufacturing.Enqueue(origin, removableBuilding, destination)
            );
            return AITestSceneBuilder.CreateContext(game, empire);
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
        private static void AddFleet(
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
        }
    }
}

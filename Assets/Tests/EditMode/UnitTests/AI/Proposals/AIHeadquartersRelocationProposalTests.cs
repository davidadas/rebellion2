using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.AI;
using Rebellion.AI.Phases;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
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

        [Test]
        public void Execute_FullDestinationWithRebuildingRubble_RemovesRubbleAndRelocatesHeadquarters()
        {
            AITurnContext context = CreateContext(
                unfinishedDestinationBuilding: false,
                out Planet origin,
                out Planet destination,
                out Building headquarters,
                out Building rubble
            );
            rubble.MarkDestroyed();
            rubble.BeginRebuild();
            rubble.ProducerOwnerID = context.Faction.InstanceID;
            rubble.ProducerPlanetID = origin.InstanceID;
            origin.AddToManufacturingQueue(rubble);
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);
            AIHeadquartersRelocationProposal proposal = new AIHeadquartersRelocationPlanner()
                .Plan(context)
                .Cast<AIHeadquartersRelocationProposal>()
                .Single();

            proposal.Execute(context);

            Assert.IsNull(context.Game.GetSceneNodeByInstanceID<Building>(rubble.InstanceID));
            Assert.AreSame(destination, headquarters.GetParent());
            Assert.IsNotNull(headquarters.Movement);
        }

        [Test]
        public void Execute_FullDestinationWithUpgradingBuilding_RemovesSourceAndRelocatesHeadquarters()
        {
            AITurnContext context = CreateContext(
                unfinishedDestinationBuilding: false,
                out Planet origin,
                out Planet destination,
                out Building headquarters,
                out Building upgrading
            );
            Building target = AITestSceneBuilder.CreateBuildingTemplate(
                "advanced-defense",
                BuildingType.Defense
            );
            upgrading.Upgrades.Add(target.TypeID);
            upgrading.BeginUpgrade(target);
            upgrading.ProducerOwnerID = context.Faction.InstanceID;
            upgrading.ProducerPlanetID = origin.InstanceID;
            origin.AddToManufacturingQueue(upgrading);
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);
            AIHeadquartersRelocationProposal proposal = new AIHeadquartersRelocationPlanner()
                .Plan(context)
                .Cast<AIHeadquartersRelocationProposal>()
                .Single();

            proposal.Execute(context);

            Assert.IsNull(context.Game.GetSceneNodeByInstanceID<Building>(upgrading.InstanceID));
            Assert.AreSame(destination, headquarters.GetParent());
            Assert.IsNotNull(headquarters.Movement);
        }

        [Test]
        public void Execute_ResourceFacilityRemoval_CommitsMaintenanceHeadroomCost()
        {
            AITurnContext context = CreateContext(
                unfinishedDestinationBuilding: false,
                out _,
                out _,
                out _,
                out _,
                completedDestinationBuildingType: BuildingType.Mine
            );
            AIHeadquartersRelocationProposal proposal = new AIHeadquartersRelocationPlanner()
                .Plan(context)
                .Cast<AIHeadquartersRelocationProposal>()
                .Single();
            int initialHeadroom = context.AvailableProjectedMaintenanceHeadroom;

            proposal.Execute(context);

            Assert.Greater(proposal.MaintenanceHeadroomCost, 0);
            Assert.AreEqual(
                initialHeadroom - proposal.MaintenanceHeadroomCost,
                context.AvailableProjectedMaintenanceHeadroom
            );
        }

        [Test]
        public void Execute_DefenseFleetAndActiveEspionageMission_EvacuatesCompleteAssignment()
        {
            AITurnContext setupContext = CreateContext(
                unfinishedDestinationBuilding: false,
                out Planet origin,
                out Planet destination,
                out Building headquarters,
                out _
            );
            Fleet defender = setupContext
                .Assessment.GetFriendlyFleets(origin)
                .Single(fleet => fleet.GetOwnerInstanceID() == setupContext.Faction.InstanceID);
            SpecialForces agent = new SpecialForces
            {
                InstanceID = "headquarters-agent",
                OwnerInstanceID = setupContext.Faction.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                AllowedMissionTypeIDs = new List<string> { MissionTypeIDs.Espionage },
            };
            setupContext.Game.AttachNode(agent, origin);
            Assert.IsTrue(
                setupContext.Missions.InitiateMission(
                    new MissionContext
                    {
                        MissionTypeID = MissionTypeIDs.Espionage,
                        Location = origin,
                        MainParticipants = new List<IMissionParticipant> { agent },
                        DecoyParticipants = new List<IMissionParticipant>(),
                    }
                )
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(
                setupContext.Game,
                setupContext.Faction
            );
            AIHeadquartersRelocationProposal proposal = new AIHeadquartersRelocationPlanner()
                .Plan(context)
                .Cast<AIHeadquartersRelocationProposal>()
                .Single();
            AIFleetDefenseProposal competingDefense = new AIFleetDefenseProposal(defender, origin);
            AIAbortMissionProposal competingAbort = new AIAbortMissionProposal(
                proposal.EspionageMission
            );
            proposal.SetScore(1);
            competingDefense.SetScore(0.5);
            competingAbort.SetScore(0.5);
            context.AddProposal(proposal);
            context.AddProposal(competingDefense);
            context.AddProposal(competingAbort);

            List<AIProposal> selected = new AISelectionPhase().Select(context);
            CollectionAssert.AreEqual(new[] { proposal }, selected);
            selected.Single().Execute(context);

            Assert.IsNull(
                context.Game.GetSceneNodeByInstanceID<Mission>(proposal.EspionageMission.InstanceID)
            );
            Assert.AreSame(destination, headquarters.GetParent());
            Assert.AreSame(destination, defender.GetParent());
            Assert.AreSame(destination, agent.GetParent());
            Assert.AreEqual(
                headquarters.Movement.MovementGroupID,
                defender.Movement.MovementGroupID
            );
            Assert.AreEqual(headquarters.Movement.MovementGroupID, agent.Movement.MovementGroupID);
            Assert.AreEqual(destination.InstanceID, defender.Order.TargetPlanetId);
            Assert.AreEqual(FleetOrderStatus.Readying, defender.Order.Status);
        }

        /// <summary>
        /// Creates a threatened mobile headquarters and a full safe destination.
        /// </summary>
        /// <param name="unfinishedDestinationBuilding">Whether queued construction occupies the destination slot.</param>
        /// <param name="origin">Receives the headquarters planet.</param>
        /// <param name="destination">Receives the destination planet.</param>
        /// <param name="headquarters">Receives the headquarters building.</param>
        /// <param name="removableBuilding">Receives the destination facility.</param>
        /// <param name="completedDestinationBuildingType">The completed facility occupying the destination.</param>
        /// <returns>The configured AI turn context.</returns>
        private static AITurnContext CreateContext(
            bool unfinishedDestinationBuilding,
            out Planet origin,
            out Planet destination,
            out Building headquarters,
            out Building removableBuilding,
            BuildingType completedDestinationBuildingType = BuildingType.Defense
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
                energyCapacity: completedDestinationBuildingType == BuildingType.Mine ? 2 : 1
            );
            origin.IsHeadquarters = true;
            empire.HQInstanceID = origin.InstanceID;
            headquarters = AddBuilding(
                game,
                origin,
                "mobile-headquarters",
                BuildingType.Headquarters
            );
            if (completedDestinationBuildingType == BuildingType.Mine)
            {
                AddBuilding(game, origin, "refinery", BuildingType.Refinery);
            }
            destination = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "destination",
                empire.InstanceID,
                energyCapacity: 1,
                rawResourceNodes: completedDestinationBuildingType == BuildingType.Mine ? 1 : 0
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
            AITestSceneBuilder.RevealPlanet(game, empire, origin);

            if (!unfinishedDestinationBuilding)
            {
                removableBuilding = AddBuilding(
                    game,
                    destination,
                    "destination-building",
                    completedDestinationBuildingType
                );
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

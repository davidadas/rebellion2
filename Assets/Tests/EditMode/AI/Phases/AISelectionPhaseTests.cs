using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Phases;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Phases
{
    [TestFixture]
    public class AISelectionPhaseTests
    {
        [Test]
        public void Select_WithScoredProposals_ReturnsHighestScoreFirst()
        {
            AITurnContext context = CreateEmptyContext();
            TestAIProposal lowerScore = new TestAIProposal("lower", new[] { "claim:lower" });
            TestAIProposal higherScore = new TestAIProposal("higher", new[] { "claim:higher" });
            lowerScore.SetScore(10);
            higherScore.SetScore(20);
            context.AddProposal(lowerScore);
            context.AddProposal(higherScore);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.AreEqual(2, selected.Count);
            Assert.AreSame(higherScore, selected[0]);
            Assert.AreSame(lowerScore, selected[1]);
        }

        [Test]
        public void Select_WithConflictingClaims_SelectsOnlyHighestScoredProposal()
        {
            AITurnContext context = CreateEmptyContext();
            TestAIProposal lowerScore = new TestAIProposal("lower", new[] { "claim:shared" });
            TestAIProposal higherScore = new TestAIProposal("higher", new[] { "claim:shared" });
            lowerScore.SetScore(10);
            higherScore.SetScore(20);
            context.AddProposal(lowerScore);
            context.AddProposal(higherScore);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.AreEqual(1, selected.Count);
            Assert.AreSame(higherScore, selected[0]);
        }

        [Test]
        public void Select_WithUnscoredProposal_DoesNotSelectProposal()
        {
            AITurnContext context = CreateEmptyContext();
            TestAIProposal proposal = new TestAIProposal("proposal", new[] { "claim" });
            context.AddProposal(proposal);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.AreEqual(0, selected.Count);
        }

        [Test]
        public void Select_WithNonPositiveScore_DoesNotSelectProposal()
        {
            AITurnContext context = CreateEmptyContext();
            TestAIProposal proposal = new TestAIProposal("proposal", new[] { "claim" });
            proposal.SetScore(0);
            context.AddProposal(proposal);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.AreEqual(0, selected.Count);
        }

        [Test]
        public void Execute_StoresSelectedProposalsOnContext()
        {
            AITurnContext context = CreateEmptyContext();
            TestAIProposal proposal = new TestAIProposal("proposal", new[] { "claim" });
            proposal.SetScore(10);
            context.AddProposal(proposal);

            new AISelectionPhase().Execute(context);

            Assert.AreEqual(1, context.SelectedProposals.Count);
            Assert.AreSame(proposal, context.SelectedProposals[0]);
        }

        [Test]
        public void Select_WithManufactureProposalBeyondMaintenanceHeadroom_DoesNotSelectProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(game, system, "p1", empire.InstanceID);
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            Building shipyard = AITestSceneBuilder.CreateBuildingTemplate(
                "shipyard-template",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            shipyard.MaintenanceCost = 10;
            AIProductionDemand demand = new AIProductionDemand(
                "shipyard-demand",
                AIProductionDemandKind.Shipyard,
                ManufacturingType.Building,
                BuildingType.Shipyard,
                planet,
                1,
                100
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                planet,
                new Technology(shipyard)
            );
            proposal.SetScore(100);
            context.AddProposal(proposal);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.AreEqual(0, selected.Count);
        }

        [Test]
        public void Select_WithTwoFreeProducerSlots_SelectsTwoManufactureProposals()
        {
            AITurnContext context = CreateManufacturingContext(
                out Planet producer,
                out PlanetSystem system
            );
            AIManufactureProposal highest = CreateDefenseProposal(
                context,
                producer,
                system,
                "highest",
                1,
                100
            );
            AIManufactureProposal middle = CreateDefenseProposal(
                context,
                producer,
                system,
                "middle",
                1,
                90
            );
            AIManufactureProposal lowest = CreateDefenseProposal(
                context,
                producer,
                system,
                "lowest",
                1,
                80
            );
            context.AddProposal(lowest);
            context.AddProposal(highest);
            context.AddProposal(middle);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new[] { highest, middle }, selected);
        }

        [Test]
        public void Select_WithCountedBatchFillingProducer_SelectsOnlyBatch()
        {
            AITurnContext context = CreateManufacturingContext(
                out Planet producer,
                out PlanetSystem system
            );
            AIManufactureProposal batch = CreateDefenseProposal(
                context,
                producer,
                system,
                "batch",
                2,
                100
            );
            AIManufactureProposal remaining = CreateDefenseProposal(
                context,
                producer,
                system,
                "remaining",
                1,
                90
            );
            context.AddProposal(remaining);
            context.AddProposal(batch);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new[] { batch }, selected);
        }

        [Test]
        public void Select_WithFacilityExpansionAndSharedWork_SelectsOnlyHigherScoredExpansion()
        {
            AITurnContext context = CreateManufacturingContext(
                out Planet producer,
                out PlanetSystem system
            );
            AIManufactureProposal expansion = CreateFacilityExpansionProposal(
                context,
                producer,
                system,
                100
            );
            AIManufactureProposal defense = CreateDefenseProposal(
                context,
                producer,
                system,
                "defense",
                1,
                90
            );
            context.AddProposal(defense);
            context.AddProposal(expansion);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new[] { expansion }, selected);
        }

        [Test]
        public void Select_WithTwoNewAttackOrders_SelectsOnlyHigherScoredOrder()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet firstEnemy = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "enemy-1",
                rebels.InstanceID
            );
            Planet secondEnemy = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "enemy-2",
                rebels.InstanceID
            );
            Fleet firstFleet = CreateBattleFleet(game, owned, empire.InstanceID, "fleet-1");
            Fleet secondFleet = CreateBattleFleet(game, owned, empire.InstanceID, "fleet-2");
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetAttackProposal lowerScore = new AIFleetAttackProposal(
                firstFleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Staging,
                firstEnemy
            );
            AIFleetAttackProposal higherScore = new AIFleetAttackProposal(
                secondFleet,
                FleetOrderType.Attack,
                FleetOrderStatus.Staging,
                secondEnemy
            );
            lowerScore.SetScore(10);
            higherScore.SetScore(20);
            context.AddProposal(lowerScore);
            context.AddProposal(higherScore);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.AreEqual(1, selected.Count);
            Assert.AreSame(higherScore, selected[0]);
        }

        [Test]
        public void Select_WithHostileMissionProposals_SelectsAllNonConflictingProposals()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "system");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposal first = CreateHostileMissionProposal(
                game,
                context,
                origin,
                system,
                rebels.InstanceID,
                "first",
                100
            );
            AIMissionProposal second = CreateHostileMissionProposal(
                game,
                context,
                origin,
                system,
                rebels.InstanceID,
                "second",
                90
            );
            AIMissionProposal third = CreateHostileMissionProposal(
                game,
                context,
                origin,
                system,
                rebels.InstanceID,
                "third",
                80
            );
            context.AddProposal(third);
            context.AddProposal(first);
            context.AddProposal(second);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new[] { first, second, third }, selected);
        }

        [Test]
        public void Select_WithActiveHostileMission_SelectsAllNonConflictingProposals()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "system");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "origin", empire.InstanceID);
            Planet activeTarget = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "active-target",
                rebels.InstanceID
            );
            StubMission activeMission = EntityFactory.CreateMission(
                "active-mission",
                empire.InstanceID,
                activeTarget.InstanceID
            );
            activeMission.ConfigKey = MissionTypeIDs.InciteUprising;
            game.AttachNode(activeMission, activeTarget);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIMissionProposal first = CreateHostileMissionProposal(
                game,
                context,
                origin,
                system,
                rebels.InstanceID,
                "first",
                100
            );
            AIMissionProposal second = CreateHostileMissionProposal(
                game,
                context,
                origin,
                system,
                rebels.InstanceID,
                "second",
                90
            );
            context.AddProposal(second);
            context.AddProposal(first);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new[] { first, second }, selected);
        }

        private static Fleet CreateBattleFleet(
            GameRoot game,
            Planet planet,
            string ownerInstanceId,
            string fleetId
        )
        {
            Fleet fleet = EntityFactory.CreateFleet(fleetId, ownerInstanceId);
            fleet.RoleType = FleetRoleType.Battle;
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip(
                $"{fleetId}-ship",
                ownerInstanceId
            );
            fleet.AddChild(ship);
            ship.SetParent(fleet);
            game.AttachNode(fleet, planet);
            return fleet;
        }

        private static AIMissionProposal CreateHostileMissionProposal(
            GameRoot game,
            AITurnContext context,
            Planet origin,
            PlanetSystem system,
            string targetOwnerInstanceId,
            string id,
            double score
        )
        {
            Planet target = AITestSceneBuilder.AddPlanet(
                game,
                system,
                $"target-{id}",
                targetOwnerInstanceId
            );
            SpecialForces participant = new SpecialForces
            {
                InstanceID = $"participant-{id}",
                OwnerInstanceID = context.Faction.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                AllowedMissionTypeIDs = new List<string> { MissionTypeIDs.InciteUprising },
            };
            game.AttachNode(participant, origin);
            AIMissionProposal proposal = new AIMissionProposal(
                new[] { participant },
                MissionTypeIDs.InciteUprising,
                target
            );
            proposal.SetScore(score);
            return proposal;
        }

        private static AITurnContext CreateManufacturingContext(
            out Planet producer,
            out PlanetSystem system
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            system = AITestSceneBuilder.AddSystem(game, "system");
            producer = AITestSceneBuilder.AddPlanet(game, system, "producer", empire.InstanceID);
            AITestSceneBuilder.AddProductionFacility(
                game,
                producer,
                "construction-yard-1",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                producer,
                "construction-yard-2",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            return AITestSceneBuilder.CreateContext(game, empire);
        }

        private static AIManufactureProposal CreateDefenseProposal(
            AITurnContext context,
            Planet producer,
            PlanetSystem system,
            string id,
            int quantity,
            double score
        )
        {
            Planet destination = AITestSceneBuilder.AddPlanet(
                context.Game,
                system,
                $"destination-{id}",
                context.Faction.InstanceID
            );
            Building shield = AITestSceneBuilder.CreateBuildingTemplate(
                $"shield-{id}",
                BuildingType.Defense
            );
            shield.MaintenanceCost = 0;
            AIProductionDemand demand = new AIProductionDemand(
                $"demand-{id}",
                AIProductionDemandKind.PlanetaryDefense,
                ManufacturingType.Building,
                BuildingType.Defense,
                destination,
                quantity,
                score
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                producer,
                new Technology(shield)
            );
            proposal.SetScore(score);
            return proposal;
        }

        private static AIManufactureProposal CreateFacilityExpansionProposal(
            AITurnContext context,
            Planet producer,
            PlanetSystem system,
            double score
        )
        {
            Planet destination = AITestSceneBuilder.AddPlanet(
                context.Game,
                system,
                "facility-destination",
                context.Faction.InstanceID
            );
            Building shipyard = AITestSceneBuilder.CreateBuildingTemplate(
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            shipyard.MaintenanceCost = 0;
            AIProductionDemand demand = new AIProductionDemand(
                "facility-expansion",
                AIProductionDemandKind.Shipyard,
                ManufacturingType.Building,
                BuildingType.Shipyard,
                destination,
                1,
                score
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                producer,
                new Technology(shipyard)
            );
            proposal.SetScore(score);
            return proposal;
        }

        private static AITurnContext CreateEmptyContext()
        {
            return new AITurnContext(null, null, null, null, null, null, null, null);
        }
    }
}

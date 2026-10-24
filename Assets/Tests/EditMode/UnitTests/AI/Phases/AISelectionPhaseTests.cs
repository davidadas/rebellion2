using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.AI;
using Rebellion.AI.Demands;
using Rebellion.AI.Phases;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scorers;
using Rebellion.AI.Selectors;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.Simulation;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Phases
{
    [TestFixture]
    public class AISelectionPhaseTests
    {
        [Test]
        public void Select_CompetingReinforcementRedirections_ReservesFleetOnce()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(game, sector, "origin", empire.InstanceID);
            Planet firstDestination = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "first",
                empire.InstanceID
            );
            Planet secondDestination = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "second",
                empire.InstanceID
            );
            Fleet fleet = EntityFactory.CreateFleet("receiving", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            game.AttachNode(fleet, origin);
            CapitalShip unfinished = AITestSceneBuilder.CreateCapitalShip(
                "unfinished",
                empire.InstanceID
            );
            unfinished.ManufacturingStatus = ManufacturingStatus.Building;
            game.AttachNode(unfinished, fleet);
            Fleet blockade = EntityFactory.CreateFleet("blockade", rebels.InstanceID);
            game.AttachNode(blockade, origin);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip("enemy", rebels.InstanceID),
                blockade
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetReinforcementRedirectionProposal first = new(fleet, origin, firstDestination);
            AIFleetReinforcementRedirectionProposal second = new(fleet, origin, secondDestination);
            Assert.IsTrue(first.CanSelect(context));
            Assert.IsTrue(second.CanSelect(context));
            first.SetScore(0.9);
            second.SetScore(0.8);
            context.AddProposals(new AIProposal[] { first, second });

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new AIProposal[] { first }, selected);
        }

        [Test]
        public void Select_ContinuationAndNewBuilding_ChargesOnlyNewBuildingForFreeSpace()
        {
            AITurnContext context = AITestSceneBuilder.CreateProductionComparison(
                out Building order
            );
            Planet destination = order.GetParentOfType<Planet>();
            destination.EnergyCapacity = destination.GetAllBuildings().Count + 1;
            PlanetSector sector = destination.GetParentOfType<PlanetSector>();
            Planet secondProducer = AITestSceneBuilder.AddPlanet(
                context.Game,
                sector,
                "second-producer",
                context.Faction.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                context.Game,
                secondProducer,
                "second-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);
            AIManufactureProposal continuation = AITestSceneBuilder.CreateProductionChoices(
                context,
                order
            )[0];
            AIManufactureProposal expansion = new AIManufactureProposal(
                new AIProductionDemand(
                    "expansion",
                    AIProductionDemandKind.ConstructionFacility,
                    ManufacturingType.Building,
                    BuildingType.ConstructionFacility,
                    destination,
                    1,
                    targetCount: 5,
                    baseDemandPercent: 100
                ),
                secondProducer,
                new Technology(
                    AITestSceneBuilder.CreateBuildingTemplate(
                        "additional-yard",
                        BuildingType.ConstructionFacility
                    )
                ),
                false,
                replacedOrder: context.Assessment.GetReplaceableManufacturingOrder(
                    secondProducer,
                    ManufacturingType.Building
                )
            );
            continuation.SetScore(0.9);
            expansion.SetScore(0.8);
            context.AddProposals(new AIProposal[] { continuation, expansion });

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEquivalent(new AIProposal[] { continuation, expansion }, selected);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Select_ContinuationCompetesWithReplacement_ReservesLaneForHigherScoredChoice(
            bool continueCurrent
        )
        {
            AITurnContext context = AITestSceneBuilder.CreateProductionComparison(
                out Building order
            );
            AIManufactureProposal[] choices = AITestSceneBuilder.CreateProductionChoices(
                context,
                order,
                competingDefenses: true,
                continuationDemandPercent: continueCurrent ? 100 : 0,
                replacementDemandPercent: continueCurrent ? 0 : 100
            );
            context.AddProposals(choices);
            new AIScoringPhase().Execute(context);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.AreSame(choices[continueCurrent ? 0 : 1], selected.Single());
        }

        [TestCase(1, true, false, 1)]
        [TestCase(2, true, false, 2)]
        [TestCase(2, false, false, 2)]
        [TestCase(1, true, true, 1)]
        [TestCase(2, true, true, 2)]
        public void Select_AdditionalAgentDemandWithExistingOrder_UsesOnlySpareCapacity(
            int facilityCount,
            bool continuationFirst,
            bool separateDestination,
            int expectedOrders
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out _);
            faction.RefinedMaterialStockpile = 10000;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "training",
                faction.InstanceID
            );
            for (int index = 0; index < facilityCount; index++)
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    planet,
                    $"facility-{index}",
                    BuildingType.TrainingFacility,
                    ManufacturingType.Troop
                );
            SpecialForces template = AITestSceneBuilder.CreateSpecialForces(
                "saboteur",
                faction.InstanceID,
                MissionTypeIDs.Sabotage
            );
            template.ConstructionCost = 10;
            SpecialForces existing = (SpecialForces)template.CreateCopy();
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            Assert.IsTrue(context.Manufacturing.Enqueue(planet, existing, planet));
            context = AITestSceneBuilder.CreateContext(game, faction);
            AIManufactureProposal continuation = new AIManufactureProposal(
                new AIProductionDemand(
                    "continue",
                    AIProductionDemandKind.SpecialForces,
                    ManufacturingType.Troop,
                    BuildingType.None,
                    planet,
                    1,
                    template.TypeID,
                    existingOrder: existing
                ),
                planet,
                new Technology(template),
                false
            );
            Planet additionalDestination = separateDestination
                ? AITestSceneBuilder.AddPlanet(game, sector, "destination", faction.InstanceID)
                : planet;
            AIManufactureProposal additional = new AIManufactureProposal(
                new AIProductionDemand(
                    "additional",
                    AIProductionDemandKind.SpecialForces,
                    ManufacturingType.Troop,
                    BuildingType.None,
                    additionalDestination,
                    1,
                    template.TypeID
                ),
                planet,
                new Technology(template),
                false,
                replacedOrder: context.Assessment.GetReplaceableManufacturingOrder(
                    planet,
                    ManufacturingType.Troop
                )
            );
            continuation.SetScore(continuationFirst ? 0.9 : 0.8);
            additional.SetScore(continuationFirst ? 0.8 : 0.9);
            context.AddProposals(new AIProposal[] { continuation, additional });

            foreach (AIProposal proposal in new AISelectionPhase().Select(context))
                proposal.Execute(context);

            Assert.AreEqual(
                expectedOrders,
                planet.GetManufacturingQueue()[ManufacturingType.Troop].Count
            );
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Select_EqualProductionScores_ContinuesExistingOrder(bool continuationFirst)
        {
            AITurnContext context = AITestSceneBuilder.CreateProductionComparison(
                out Building order
            );
            AIManufactureProposal[] choices = AITestSceneBuilder.CreateProductionChoices(
                context,
                order,
                competingDefenses: true,
                continuationDemandPercent: 100
            );
            IEnumerable<AIProposal> ordered = choices;
            if (!continuationFirst)
                ordered = ordered.Reverse();
            context.AddProposals(ordered);
            new AIScoringPhase().Execute(context);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.AreSame(choices[0], selected.Single());
        }

        [Test]
        public void Select_EqualProductionScores_PreservesTieBreakRandomConsumption()
        {
            AITurnContext original = AITestSceneBuilder.CreateProductionComparison(
                out Building order
            );
            SequenceRNG random = new SequenceRNG(intValues: new[] { 0, 1 });
            AITurnContext context = AITestSceneBuilder.CreateContext(
                original.Game,
                original.Faction,
                random: random
            );
            AIManufactureProposal[] choices = AITestSceneBuilder.CreateProductionChoices(
                context,
                order,
                competingDefenses: true,
                continuationDemandPercent: 100
            );
            context.AddProposals(choices);
            new AIScoringPhase().Execute(context);

            new AISelectionPhase().Select(context);

            Assert.AreEqual(1, random.NextInt(0, 2));
        }

        [Test]
        public void Select_RoutineDefenseContinuationAndExpansion_PreservesInfrastructurePriority()
        {
            AITurnContext context = AITestSceneBuilder.CreateProductionComparison(
                out Building order
            );
            AIManufactureProposal[] choices = AITestSceneBuilder.CreateProductionChoices(
                context,
                order
            );
            choices[0].SetScore(0.9);
            choices[1].SetScore(0.8);
            context.AddProposals(choices);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.AreSame(choices[1], selected.Single());
        }

        [Test]
        public void Select_RejectedReplacementFallback_DoesNotOverbookSpareTrainingCapacity()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out _);
            faction.RefinedMaterialStockpile = 10000;
            game.Config.AI.Selection.MaintenanceHeadroomReserve = 0;
            game.Config.AI.Selection.DemandUtility.Weight = 1;
            game.Config.AI.Selection.DemandUtility.SaturationValue = 100;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet producer = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "training",
                faction.InstanceID
            );
            Planet unavailable = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "unavailable",
                faction.InstanceID
            );
            for (int index = 0; index < 2; index++)
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    producer,
                    $"facility-{index}",
                    BuildingType.TrainingFacility,
                    ManufacturingType.Troop
                );
            SpecialForces template = AITestSceneBuilder.CreateSpecialForces(
                "saboteur",
                faction.InstanceID,
                MissionTypeIDs.Sabotage
            );
            template.ConstructionCost = 10;
            SpecialForces existing = (SpecialForces)template.CreateCopy();
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            Assert.IsTrue(context.Manufacturing.Enqueue(producer, existing, producer));
            context = AITestSceneBuilder.CreateContext(game, faction);
            AIProductionDemand currentNeed = new AIProductionDemand(
                "current",
                AIProductionDemandKind.SpecialForces,
                ManufacturingType.Troop,
                BuildingType.None,
                producer,
                1,
                template.TypeID,
                targetCount: 3,
                baseDemandPercent: 100,
                existingOrder: existing
            );
            AIProductionDemand troopNeed = new AIProductionDemand(
                "troop",
                AIProductionDemandKind.GarrisonRegimentReserve,
                ManufacturingType.Troop,
                BuildingType.None,
                producer,
                1,
                targetCount: 1,
                baseDemandPercent: 100
            );
            Regiment regimentTemplate = AITestSceneBuilder.CreateRegiment(
                "regiment",
                faction.InstanceID
            );
            regimentTemplate.ConstructionCost = template.ConstructionCost;
            regimentTemplate.BaseBuildSpeed = template.BaseBuildSpeed;
            Technology regiment = new Technology(regimentTemplate);
            AIManufactureProposal replacement = new AIManufactureProposal(
                new[]
                {
                    new AIManufactureProposal(
                        troopNeed,
                        unavailable,
                        regiment,
                        false,
                        replacedOrder: context.Assessment.GetReplaceableManufacturingOrder(
                            unavailable,
                            troopNeed.ManufacturingType
                        )
                    ),
                    new AIManufactureProposal(
                        troopNeed,
                        producer,
                        regiment,
                        false,
                        replacedOrder: context.Assessment.GetReplaceableManufacturingOrder(
                            producer,
                            troopNeed.ManufacturingType
                        )
                    ),
                }
            );
            AIManufactureProposal first = new AIManufactureProposal(
                new AIProductionDemand(
                    "first",
                    AIProductionDemandKind.SpecialForces,
                    ManufacturingType.Troop,
                    BuildingType.None,
                    producer,
                    1,
                    template.TypeID,
                    targetCount: 3,
                    baseDemandPercent: 20
                ),
                producer,
                new Technology(template),
                false,
                replacedOrder: context.Assessment.GetReplaceableManufacturingOrder(
                    producer,
                    ManufacturingType.Troop
                )
            );
            AIManufactureProposal second = new AIManufactureProposal(
                new AIProductionDemand(
                    "second",
                    AIProductionDemandKind.SpecialForces,
                    ManufacturingType.Troop,
                    BuildingType.None,
                    producer,
                    1,
                    template.TypeID,
                    targetCount: 3,
                    baseDemandPercent: 10
                ),
                producer,
                new Technology(template),
                false,
                replacedOrder: context.Assessment.GetReplaceableManufacturingOrder(
                    producer,
                    ManufacturingType.Troop
                )
            );
            context.SetProductionDemands(
                new[] { currentNeed, troopNeed, first.Demand, second.Demand }
            );
            context.AddProposals(new AIProposal[] { replacement, first, second });
            new AIScoringPhase().Execute(context);
            Assert.Greater(replacement.Score, first.Score);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            foreach (AIProposal proposal in selected)
                proposal.Execute(context);

            Assert.AreEqual(2, producer.GetManufacturingQueue()[ManufacturingType.Troop].Count);
        }

        [TestCase(0, false)]
        [TestCase(90, true)]
        public void Select_SaveReloadWithExistingConstruction_PreservesContinueOrReplaceDecision(
            int progress,
            bool continueCurrent
        )
        {
            AITurnContext uninterrupted = AITestSceneBuilder.CreateProductionComparison(
                out Building order
            );
            uninterrupted.Game.Config.AI.Selection.ProductionUtility.CompletionTime.Weight = 0.4;
            uninterrupted
                .Game
                .Config
                .AI
                .Selection
                .ProductionUtility
                .CompletionTime
                .SaturationValue = 100;
            uninterrupted.Game.Config.AI.Selection.ProductionUtility.InterruptionCost.Weight = 1;
            uninterrupted
                .Game
                .Config
                .AI
                .Selection
                .ProductionUtility
                .InterruptionCost
                .SaturationValue = 100;
            order.ManufacturingProgress = progress;
            string saved = SerializationHelper.Serialize(uninterrupted.Game);
            StringAssert.DoesNotContain("IsAIProductionFiller", saved);
            GameRoot loaded = SerializationHelper.Deserialize<GameRoot>(saved);
            loaded.SetConfig(uninterrupted.Game.Config);
            using GameSession session = new GameSession(loaded, TestGameData.Create(loaded.Config));
            AITurnContext resumed = AITestSceneBuilder.CreateContext(
                loaded,
                loaded.GetFactionByOwnerInstanceID(uninterrupted.Faction.InstanceID)
            );
            Building loadedOrder = loaded.GetSceneNodeByInstanceID<Building>(order.InstanceID);
            uninterrupted.AddProposals(
                AITestSceneBuilder.CreateProductionChoices(
                    uninterrupted,
                    order,
                    competingDefenses: true
                )
            );
            resumed.AddProposals(
                AITestSceneBuilder.CreateProductionChoices(
                    resumed,
                    loadedOrder,
                    competingDefenses: true
                )
            );
            new AIScoringPhase().Execute(uninterrupted);
            new AIScoringPhase().Execute(resumed);

            AIManufactureProposal first = new AISelectionPhase()
                .Select(uninterrupted)
                .OfType<AIManufactureProposal>()
                .Single();
            AIManufactureProposal second = new AISelectionPhase()
                .Select(resumed)
                .OfType<AIManufactureProposal>()
                .Single();

            Assert.AreEqual(continueCurrent, first.IsContinuation);
            Assert.AreEqual(
                first.Product.GetReference().GetTypeID(),
                second.Product.GetReference().GetTypeID()
            );
        }

        [TestCase(false, false, 1)]
        [TestCase(true, false, 1)]
        [TestCase(false, true, 0)]
        [TestCase(true, true, 1)]
        public void Select_PlannedLoadedReinforcements_ReservesOnlyRemainingGroundRequirement(
            bool shielded,
            bool occupied,
            int expectedTransfers
        )
        {
            AITurnContext context = AITestSceneBuilder.CreateBombardmentReinforcementContext(
                shielded,
                occupied,
                out Fleet receiver,
                out Fleet other,
                out Planet source
            );
            GameRoot game = context.Game;
            game.DeleteNode(other);
            var transfers = new List<AITransferUnitProposal>();
            Planet target = context.Assessment.GetKnownPlanet(receiver.Order.TargetPlanetId);
            for (int index = 0; index < 2; index++)
            {
                Fleet donor = EntityFactory.CreateFleet(
                    "donor-" + index,
                    context.Faction.InstanceID
                );
                donor.RoleType = FleetRoleType.Battle;
                game.AttachNode(donor, source);
                game.AttachNode(
                    AITestSceneBuilder.CreateCapitalShip(
                        "anchor-" + index,
                        context.Faction.InstanceID,
                        100,
                        0,
                        0
                    ),
                    donor
                );
                CapitalShip loaded = AITestSceneBuilder.CreateCapitalShip(
                    "loaded-" + index,
                    context.Faction.InstanceID,
                    100,
                    1,
                    0
                );
                game.AttachNode(loaded, donor);
                game.AttachNode(
                    AITestSceneBuilder.CreateRegiment(
                        "carried-" + index,
                        context.Faction.InstanceID
                    ),
                    loaded
                );
                transfers.Add(
                    new AITransferUnitProposal(donor, receiver, loaded, receiver, target)
                );
            }
            context = AITestSceneBuilder.CreateContext(game, context.Faction);
            new AIProductionDemandGenerator().Generate(context);
            Assert.AreEqual(3, context.GetAttackDemand(target).RegimentCount);
            Assert.AreEqual(1, context.GetAttackDemand(target).OccupationRegimentCount);
            foreach (AITransferUnitProposal transfer in transfers)
                Assert.IsTrue(transfer.CanSelect(context));
            List<AITransferUnitProposal> planned = new AIFleetPlanner()
                .Plan(context)
                .OfType<AITransferUnitProposal>()
                .Where(proposal =>
                    proposal.TargetFleet == receiver
                    && transfers.Any(transfer => transfer.Unit == proposal.Unit)
                )
                .ToList();
            for (int index = 0; index < planned.Count; index++)
            {
                planned[index].SetScore(2 - index);
                context.AddProposal(planned[index]);
            }

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.AreEqual(expectedTransfers, selected.OfType<AITransferUnitProposal>().Count());
        }

        [Test]
        public void Select_LoadedShipWithoutHullShortage_ReinforcesMissingRegiments()
        {
            AITransferUnitProposal transfer = CreateDefenseTransfer(out AITurnContext context);
            context.Game.AttachNode(
                AITestSceneBuilder.CreateRegiment("reinforcement", context.Faction.InstanceID, 100),
                (CapitalShip)transfer.Unit
            );
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);
            context.SetAttackDemands(
                new[]
                {
                    new AIAttackDemand(transfer.TargetPlanet, 0, 0, 0, 0, 0, 100, 1, 1, 0, false),
                }
            );
            context.SetProductionDemands(Array.Empty<AIProductionDemand>());
            transfer.SetScore(1);
            context.AddProposal(transfer);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new AIProposal[] { transfer }, selected);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Select_DefenseContinuationAndDonation_SelectsBoth(bool defenseFirst)
        {
            AITransferUnitProposal transfer = CreateDefenseTransfer(out AITurnContext context);
            Fleet donor = (Fleet)transfer.SourceContainer;
            AIFleetDefenseProposal defense = new(donor, donor.GetParentOfType<Planet>());
            transfer.SetScore(defenseFirst ? 1 : 2);
            defense.SetScore(defenseFirst ? 2 : 1);
            context.AddProposal(transfer);
            context.AddProposal(defense);

            var selected = new Rebellion.AI.Phases.AISelectionPhase().Select(context);

            CollectionAssert.AreEquivalent(new AIProposal[] { transfer, defense }, selected);
        }

        [Test]
        public void Select_OrderCleanupAndDonation_SelectsOnlyCleanup()
        {
            AITransferUnitProposal transfer = CreateDefenseTransfer(out AITurnContext context);
            Fleet donor = (Fleet)transfer.SourceContainer;
            AIClearFleetOrderProposal clear = new(donor, donor.Order);
            transfer.SetScore(2);
            clear.SetScore(1);
            context.AddProposal(transfer);
            context.AddProposal(clear);

            var selected = new Rebellion.AI.Phases.AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new AIProposal[] { clear }, selected);
        }

        [TestCase(true, 500)]
        [TestCase(false, 500)]
        [TestCase(true, 1000)]
        [TestCase(false, 1000)]
        public void Select_TransferAndProduction_CountsBothAgainstSharedShortage(
            bool transferFirst,
            int deficit
        )
        {
            AITransferUnitProposal transfer = CreateDefenseTransfer(out AITurnContext context);
            Planet producer = transfer.SourceContainer.GetParentOfType<Planet>();
            AITestSceneBuilder.AddProductionFacility(
                context.Game,
                producer,
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            context = AITestSceneBuilder.CreateContext(context.Game, context.Faction);
            context.Game.Config.AI.Selection.MaintenanceHeadroomReserve = 0;
            AIProductionDemand demand = new(
                "reinforcement",
                AIProductionDemandKind.FleetCapitalShip,
                ManufacturingType.Ship,
                BuildingType.None,
                transfer.TargetFleet,
                1,
                combatStrengthDeficit: deficit
            );
            context.SetProductionDemands(new[] { demand });
            CapitalShip hull = AITestSceneBuilder.CreateCapitalShip(
                "hull",
                context.Faction.InstanceID,
                500
            );
            hull.ConstructionCost = 0;
            AIManufactureProposal production = new(demand, producer, new Technology(hull), true);
            transfer.SetScore(transferFirst ? 2 : 1);
            production.SetScore(transferFirst ? 1 : 2);
            context.AddProposal(transfer);
            context.AddProposal(production);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            if (deficit == 1000)
                CollectionAssert.AreEquivalent(new AIProposal[] { transfer, production }, selected);
            else
                CollectionAssert.AreEqual(
                    new AIProposal[] { transferFirst ? transfer : production },
                    selected
                );
        }

        [TestCase(AIProductionDemandKind.FleetCapitalShip)]
        [TestCase(AIProductionDemandKind.FleetStarfighter)]
        [TestCase(AIProductionDemandKind.FleetRegiment)]
        public void Select_PriorityFleetsWithIndependentCapacity_SelectsBothInFleetOrder(
            AIProductionDemandKind kind
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            AIManufactureProposal primary = CreateFleetProductionProposal(
                context,
                "primary",
                kind,
                1
            );
            AIManufactureProposal secondary = CreateFleetProductionProposal(
                context,
                "secondary",
                kind,
                2
            );
            context.SetFleetProductionPriorities(
                new[] { primary.Demand.DestinationFleet, secondary.Demand.DestinationFleet }
            );
            context.AddProposal(primary);
            context.AddProposal(secondary);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new[] { primary, secondary }, selected);
        }

        [Test]
        public void Select_PriorityFleetWithSpareCapacity_AllowsNewBattleFleetSeed()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            AIManufactureProposal primary = CreateFleetProductionProposal(
                context,
                "primary",
                AIProductionDemandKind.FleetCapitalShip,
                1
            );
            AIManufactureProposal seed = CreateFleetProductionProposal(
                context,
                "seed",
                AIProductionDemandKind.FleetSeedCapitalShip,
                2
            );
            context.SetFleetProductionPriorities(new[] { primary.Demand.DestinationFleet });
            context.AddProposal(primary);
            context.AddProposal(seed);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new[] { primary, seed }, selected);
        }

        [Test]
        public void Select_PriorityFleet_PreservesUnguardedHeadquartersSeed()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            AIManufactureProposal primary = CreateFleetProductionProposal(
                context,
                "primary",
                AIProductionDemandKind.FleetCapitalShip,
                1
            );
            AIManufactureProposal seed = CreateFleetProductionProposal(
                context,
                "headquarters",
                AIProductionDemandKind.FleetSeedCapitalShip,
                2
            );
            faction.HQInstanceID = seed.Demand.DestinationPlanet.InstanceID;
            context.SetFleetProductionPriorities(new[] { primary.Demand.DestinationFleet });
            context.AddProposal(primary);
            context.AddProposal(seed);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEquivalent(new[] { primary, seed }, selected);
        }

        [Test]
        public void Select_NoFleetPriorities_AllowsOtherFleet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            AIManufactureProposal primary = CreateFleetProductionProposal(
                context,
                "primary",
                AIProductionDemandKind.FleetCapitalShip,
                1
            );
            AIManufactureProposal other = CreateFleetProductionProposal(
                context,
                "other",
                AIProductionDemandKind.FleetCapitalShip,
                2
            );
            context.SetFleetProductionPriorities(null);
            context.AddProposal(other);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new[] { other }, selected);
        }

        [TestCase(FleetRoleType.Battle, FleetOrderType.Defend)]
        [TestCase(FleetRoleType.Colonization, FleetOrderType.Colonize)]
        public void Select_PriorityFleet_PreservesDefenseAndColonization(
            FleetRoleType role,
            FleetOrderType order
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            AIManufactureProposal primary = CreateFleetProductionProposal(
                context,
                "primary",
                AIProductionDemandKind.FleetCapitalShip,
                1
            );
            AIManufactureProposal other = CreateFleetProductionProposal(
                context,
                "other",
                AIProductionDemandKind.FleetCapitalShip,
                2
            );
            other.Demand.DestinationFleet.RoleType = role;
            other.Demand.DestinationFleet.Order.OrderType = order;
            context.SetFleetProductionPriorities(new[] { primary.Demand.DestinationFleet });
            context.AddProposal(primary);
            context.AddProposal(other);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEquivalent(new[] { primary, other }, selected);
        }

        [Test]
        public void Select_PriorityFleetWithSpareCapacity_AllowsIdleBattleFleetAssembly()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            AIManufactureProposal primary = CreateFleetProductionProposal(
                context,
                "primary",
                AIProductionDemandKind.FleetCapitalShip,
                1
            );
            AIManufactureProposal assembly = CreateFleetProductionProposal(
                context,
                "assembly",
                AIProductionDemandKind.FleetCapitalShip,
                2
            );
            assembly.Demand.DestinationFleet.Order = null;
            context.SetFleetProductionPriorities(new[] { primary.Demand.DestinationFleet });
            context.AddProposal(primary);
            context.AddProposal(assembly);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new[] { primary, assembly }, selected);
        }

        [Test]
        public void Select_PriorityFleetNeedsTroops_AllowsOtherFleetShipProduction()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            AIManufactureProposal troops = CreateFleetProductionProposal(
                context,
                "primary",
                AIProductionDemandKind.FleetRegiment,
                1
            );
            AIManufactureProposal ship = CreateFleetProductionProposal(
                context,
                "secondary",
                AIProductionDemandKind.FleetCapitalShip,
                2
            );
            context.SetFleetProductionPriorities(
                new[] { troops.Demand.DestinationFleet, ship.Demand.DestinationFleet }
            );
            context.AddProposal(ship);
            context.AddProposal(troops);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new[] { troops, ship }, selected);
        }

        [Test]
        public void Select_PriorityFleetsCompeteForOneYard_SelectsHigherPriorityFleet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            AIManufactureProposal primary = CreateFleetProductionProposal(
                context,
                "primary",
                AIProductionDemandKind.FleetCapitalShip,
                1
            );
            AIManufactureProposal secondary = CreateFleetProductionProposal(
                context,
                "secondary",
                AIProductionDemandKind.FleetCapitalShip,
                2
            );
            secondary = new AIManufactureProposal(
                secondary.Demand,
                primary.ProducerPlanet,
                primary.Product,
                true
            );
            secondary.SetScore(2);
            context.SetFleetProductionPriorities(
                new[] { primary.Demand.DestinationFleet, secondary.Demand.DestinationFleet }
            );
            context.AddProposal(secondary);
            context.AddProposal(primary);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new[] { primary }, selected);
        }

        [TestCase(
            BuildingType.ConstructionFacility,
            ManufacturingType.Building,
            AIProductionDemandKind.ConstructionFacility,
            5
        )]
        [TestCase(
            BuildingType.Shipyard,
            ManufacturingType.Ship,
            AIProductionDemandKind.Shipyard,
            6
        )]
        [TestCase(
            BuildingType.TrainingFacility,
            ManufacturingType.Troop,
            AIProductionDemandKind.TrainingFacility,
            4
        )]
        public void Select_IndustryUsesAllocatedSpace_PreservesShieldAndRejectsExtraEconomy(
            BuildingType facilityType,
            ManufacturingType productionType,
            AIProductionDemandKind demandKind,
            int facilityTarget
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            game.Config.AI.Selection.MaintenanceHeadroomReserve = 0;
            game.Config.AI.Infrastructure.PlanetaryDefenseMaintenanceReservePercent = 0;
            game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit = 2;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet hub = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "hub",
                faction.InstanceID,
                energyCapacity: facilityTarget + 1,
                rawResourceNodes: 1
            );
            Planet industryProducer = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "industry",
                faction.InstanceID
            );
            Planet defenseProducer = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "defense",
                faction.InstanceID
            );
            for (int index = 0; index < facilityTarget; index++)
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    industryProducer,
                    $"constructor-{index}",
                    BuildingType.ConstructionFacility,
                    ManufacturingType.Building
                );
            AITestSceneBuilder.AddProductionFacility(
                game,
                defenseProducer,
                "constructor",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            Assert.IsTrue(
                context.StrategicPlan.TryAllocateInfrastructureHub(
                    hub,
                    facilityType,
                    facilityTarget
                )
            );
            Building yard = AITestSceneBuilder.CreateBuildingTemplate(
                "yard",
                facilityType,
                productionType
            );
            Building shield = AITestSceneBuilder.CreateBuildingTemplate(
                "shield",
                BuildingType.Defense
            );
            shield.ShieldStrength = 80;
            Building mine = AITestSceneBuilder.CreateBuildingTemplate("mine", BuildingType.Mine);
            AIManufactureProposal industry = new(
                new AIProductionDemand(
                    "industry",
                    demandKind,
                    ManufacturingType.Building,
                    facilityType,
                    hub,
                    facilityTarget
                ),
                industryProducer,
                new Technology(yard),
                false
            );
            AIManufactureProposal defense = new(
                new AIProductionDemand(
                    "defense",
                    AIProductionDemandKind.PlanetaryDefense,
                    ManufacturingType.Building,
                    BuildingType.Defense,
                    hub,
                    1
                ),
                defenseProducer,
                new Technology(shield),
                false
            );
            AIManufactureProposal economy = new(
                new AIProductionDemand(
                    "economy",
                    AIProductionDemandKind.Mine,
                    ManufacturingType.Building,
                    BuildingType.Mine,
                    hub,
                    1
                ),
                defenseProducer,
                new Technology(mine),
                false
            );
            industry.SetScore(100);
            defense.SetScore(1);
            economy.SetScore(1000);
            context.AddProposal(economy);
            context.AddProposal(industry);
            context.AddProposal(defense);

            var selected = new AISelectionPhase().Select(context);

            Assert.AreEqual(2, selected.Count);
            Assert.IsFalse(
                selected
                    .OfType<AIManufactureProposal>()
                    .Any(proposal => proposal.Demand.Kind == AIProductionDemandKind.Mine)
            );
            foreach (var proposal in selected)
                proposal.Execute(context);
            Assert.AreEqual(facilityTarget, hub.GetTotalBuildingTypeCount(facilityType));
            Assert.AreEqual(1, hub.GetTotalBuildingTypeCount(BuildingType.Defense));
            Assert.AreEqual(0, hub.GetTotalBuildingTypeCount(BuildingType.Mine));
        }

        [Test]
        public void Select_MultipleRoutineDefenseRequests_StartsOnlyOneFillerPerLane()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "producer",
                faction.InstanceID
            );
            for (int index = 0; index < 2; index++)
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    planet,
                    $"yard-{index}",
                    BuildingType.ConstructionFacility,
                    ManufacturingType.Building
                );
            Building shield = AITestSceneBuilder.CreateBuildingTemplate(
                "shield",
                BuildingType.Defense
            );
            shield.ShieldStrength = 80;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            for (int index = 0; index < 2; index++)
            {
                AIManufactureProposal proposal = new AIManufactureProposal(
                    new AIProductionDemand(
                        $"filler-{index}",
                        AIProductionDemandKind.PlanetaryDefense,
                        ManufacturingType.Building,
                        BuildingType.Defense,
                        planet,
                        1
                    ),
                    planet,
                    new Technology(shield),
                    false
                );
                proposal.SetScore(1);
                context.AddProposal(proposal);
            }

            var selected = new AISelectionPhase().Select(context);
            Assert.AreEqual(1, selected.Count);
            selected.Single().Execute(context);

            Assert.AreSame(
                planet.GetManufacturingQueue()[ManufacturingType.Building].Single(),
                context.Assessment.GetReplaceableManufacturingOrder(
                    planet,
                    ManufacturingType.Building
                )
            );
        }

        [TestCase(false, BuildingType.Mine)]
        [TestCase(true, BuildingType.Defense)]
        public void Select_StaticDefenseAndEconomicExpansion_UsesImmediateThreatPriority(
            bool emergency,
            BuildingType expectedType
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out _);
            game.Config.AI.Selection.MaintenanceHeadroomReserve = 0;
            game.Config.AI.Infrastructure.PlanetaryDefenseMaintenanceReservePercent = 0;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet destination = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "destination",
                faction.InstanceID,
                rawResourceNodes: 1
            );
            Planet producer = AddBuildingProducer(game, sector, faction, "producer");
            AIManufactureProposal economy = CreateBuildingProposal(
                destination,
                producer,
                AIProductionDemandKind.Mine,
                BuildingType.Mine,
                100
            );
            Building shield = AITestSceneBuilder.CreateBuildingTemplate(
                "shield",
                BuildingType.Defense,
                ManufacturingType.None
            );
            shield.MaintenanceCost = 0;
            AIManufactureProposal defense = new AIManufactureProposal(
                new AIProductionDemand(
                    "defense",
                    AIProductionDemandKind.PlanetaryDefense,
                    ManufacturingType.Building,
                    BuildingType.Defense,
                    destination,
                    1,
                    isEmergencyDefense: emergency
                ),
                producer,
                new Technology(shield),
                false
            );
            defense.SetScore(1);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            context.AddProposal(defense);
            context.AddProposal(economy);

            List<AIManufactureProposal> selected = new AISelectionPhase()
                .Select(context)
                .OfType<AIManufactureProposal>()
                .ToList();

            Assert.AreEqual(1, selected.Count);
            Assert.AreEqual(expectedType, selected.Single().Demand.BuildingType);
        }

        [Test]
        public void Select_WithIdleTrainingLanesWithoutKnownWork_DoesNotStartAgentProduction()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            for (int index = 0; index < 2; index++)
            {
                Planet planet = AITestSceneBuilder.AddPlanet(
                    game,
                    sector,
                    $"training-{index}",
                    faction.InstanceID
                );
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    planet,
                    $"facility-{index}",
                    BuildingType.TrainingFacility,
                    ManufacturingType.Troop
                );
            }
            SpecialForces template = AITestSceneBuilder.CreateSpecialForces(
                "saboteur",
                faction.InstanceID,
                MissionTypeIDs.Sabotage
            );
            template.ConstructionCost = 10;
            faction.ResearchQueue[ManufacturingType.Troop] = new List<Technology> { new(template) };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            context.SetProductionDemands(
                new Rebellion.AI.Demands.AIProductionDemandGenerator()
                    .BuildDemands(context)
                    .Where(demand => demand.Kind == AIProductionDemandKind.SpecialForces)
            );
            List<AIProposal> proposals = new AIProductionPlanner().Plan(context);
            foreach (AIProposal proposal in proposals)
                proposal.SetScore(50);
            context.AddProposals(proposals);

            List<AIManufactureProposal> selected = new AISelectionPhase()
                .Select(context)
                .OfType<AIManufactureProposal>()
                .ToList();
            Assert.IsEmpty(selected);
        }

        [Test]
        public void Select_WithFillerScoredAboveNormalDemand_SelectsNormalDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "training",
                faction.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "facility",
                BuildingType.TrainingFacility,
                ManufacturingType.Troop
            );
            SpecialForces template = AITestSceneBuilder.CreateSpecialForces(
                "saboteur",
                faction.InstanceID,
                MissionTypeIDs.Sabotage
            );
            template.ConstructionCost = 10;
            AITurnContext context = AITestSceneBuilder.CreateContext(game, faction);
            AIManufactureProposal normal = new AIManufactureProposal(
                new AIProductionDemand(
                    "normal",
                    AIProductionDemandKind.SpecialForces,
                    ManufacturingType.Troop,
                    BuildingType.None,
                    planet,
                    1,
                    template.TypeID
                ),
                planet,
                new Technology(template),
                true
            );
            AIManufactureProposal filler = new AIManufactureProposal(
                new AIProductionDemand(
                    "filler",
                    AIProductionDemandKind.IdleSpecialForces,
                    ManufacturingType.Troop,
                    BuildingType.None,
                    planet,
                    1,
                    template.TypeID
                ),
                planet,
                new Technology(template),
                false
            );
            normal.SetScore(1);
            filler.SetScore(100);
            context.AddProposals(new List<AIProposal> { filler, normal });

            List<AIProposal> selected = new Rebellion.AI.Phases.AISelectionPhase().Select(context);

            Assert.AreEqual(1, selected.Count);
            Assert.AreEqual(
                AIProductionDemandKind.SpecialForces,
                ((AIManufactureProposal)selected[0]).Demand.Kind
            );
        }

        [Test]
        public void Select_WithScoredProposals_ReturnsHighestScoreFirst()
        {
            AITurnContext context = CreateEmptyContext();
            TestAIProposal lowerScore = new TestAIProposal("lower");
            TestAIProposal higherScore = new TestAIProposal("higher");
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
        public void Select_WithConflictingFleetReservations_SelectsOnlyHighestScoredProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(game, sector, "planet", empire.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            game.AttachNode(fleet, planet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIFleetRoleProposal lowerScore = new AIFleetRoleProposal(
                fleet,
                FleetRoleType.Colonization
            );
            AIFleetRoleProposal higherScore = new AIFleetRoleProposal(fleet, FleetRoleType.Battle);
            lowerScore.SetScore(10);
            higherScore.SetScore(20);
            context.AddProposal(lowerScore);
            context.AddProposal(higherScore);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.AreEqual(1, selected.Count);
            Assert.AreSame(higherScore, selected[0]);
        }

        [Test]
        public void Select_WithBuildingProposalsBeyondDestinationEnergy_SelectsHighestValueFit()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet destination = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "destination",
                empire.InstanceID,
                energyCapacity: 1,
                rawResourceNodes: 1
            );
            Planet firstProducer = AddBuildingProducer(game, sector, empire, "first-producer");
            Planet secondProducer = AddBuildingProducer(game, sector, empire, "second-producer");
            AIManufactureProposal higher = CreateBuildingProposal(
                destination,
                firstProducer,
                AIProductionDemandKind.Mine,
                BuildingType.Mine,
                100
            );
            AIManufactureProposal lower = CreateBuildingProposal(
                destination,
                secondProducer,
                AIProductionDemandKind.Refinery,
                BuildingType.Refinery,
                90
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            context.AddProposal(lower);
            context.AddProposal(higher);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new[] { higher }, selected);
        }

        [Test]
        public void Select_WithEqualScoredConflictingProposals_UsesPersistedRandomTieBreaker()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            AITurnContext context = AITestSceneBuilder.CreateContext(
                game,
                empire,
                random: new SequenceRNG(intValues: new[] { 0 })
            );
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(game, sector, "planet", empire.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            game.AttachNode(fleet, planet);
            AIFleetRoleProposal first = new AIFleetRoleProposal(fleet, FleetRoleType.Colonization);
            AIFleetRoleProposal second = new AIFleetRoleProposal(fleet, FleetRoleType.Battle);
            first.SetScore(10);
            second.SetScore(10);
            context.AddProposal(first);
            context.AddProposal(second);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new[] { second }, selected);
        }

        [Test]
        public void Select_WithZeroScoreFleetOrderCleanup_SelectsProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector1");
            Planet planet = AITestSceneBuilder.AddPlanet(game, sector, "planet", empire.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            FleetOrder order = new FleetOrder { OrderType = FleetOrderType.Colonize };
            fleet.Order = order;
            game.AttachNode(fleet, planet);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIClearFleetOrderProposal proposal = new AIClearFleetOrderProposal(fleet, order);
            proposal.SetScore(0);
            context.AddProposal(proposal);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new[] { proposal }, selected);
        }

        [Test]
        public void Select_WithUnscoredProposal_DoesNotSelectProposal()
        {
            AITurnContext context = CreateEmptyContext();
            TestAIProposal proposal = new TestAIProposal("proposal");
            context.AddProposal(proposal);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.AreEqual(0, selected.Count);
        }

        [Test]
        public void Select_WithNonPositiveScore_DoesNotSelectProposal()
        {
            AITurnContext context = CreateEmptyContext();
            TestAIProposal proposal = new TestAIProposal("proposal");
            proposal.SetScore(0);
            context.AddProposal(proposal);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.AreEqual(0, selected.Count);
        }

        [Test]
        public void Select_WithManufactureProposalBeyondMaintenanceHeadroom_DoesNotSelectProposal()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector planetSector = AITestSceneBuilder.AddSector(game, "sector1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "p1",
                empire.InstanceID
            );
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
                baseDemandPercent: 100
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                planet,
                new Technology(shipyard),
                false
            );
            proposal.SetScore(100);
            context.AddProposal(proposal);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.AreEqual(0, selected.Count);
        }

        [Test]
        public void Select_HigherScoringMandatoryManufacturingCompetesWithRelocation_SelectsOnlyRelocation()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.Selection.MaintenanceHeadroomReserve = 0;
            empire.Settings.Headquarters = new HeadquartersSettings
            {
                FacilityTypeID = "mobile-headquarters",
                IsMobile = true,
            };
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "origin",
                empire.InstanceID,
                energyCapacity: 4
            );
            origin.IsHeadquarters = true;
            empire.HQInstanceID = origin.InstanceID;
            Building headquarters = AITestSceneBuilder.CreateBuildingTemplate(
                "mobile-headquarters",
                BuildingType.Headquarters
            );
            headquarters.OwnerInstanceID = empire.InstanceID;
            game.AttachNode(headquarters, origin);
            AITestSceneBuilder.AddProductionFacility(
                game,
                origin,
                "construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                origin,
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                origin,
                "refinery",
                BuildingType.Refinery,
                ManufacturingType.None
            );
            Planet destination = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "destination",
                empire.InstanceID,
                energyCapacity: 1,
                rawResourceNodes: 1
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                destination,
                "mine",
                BuildingType.Mine,
                ManufacturingType.None
            );
            Fleet incoming = EntityFactory.CreateFleet("incoming", rebels.InstanceID);
            incoming.Movement = new MovementState { TransitTicks = 10 };
            game.AttachNode(incoming, origin);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip("incoming-ship", rebels.InstanceID, 200),
                incoming
            );
            Fleet defender = EntityFactory.CreateFleet("defender", empire.InstanceID);
            defender.RoleType = FleetRoleType.Battle;
            game.AttachNode(defender, origin);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip("defender-ship", empire.InstanceID, 100),
                defender
            );
            AITestSceneBuilder.RevealPlanet(game, empire, origin);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIHeadquartersRelocationProposal relocation = new AIHeadquartersRelocationPlanner()
                .Plan(context)
                .Cast<AIHeadquartersRelocationProposal>()
                .Single();
            AIManufactureProposal manufacture = CreateManufactureProposal(
                context,
                origin,
                AIProductionDemandKind.ColonizationFleetSeedCapitalShip,
                BuildingType.None,
                score: 0.99,
                maintenanceCost: 1
            );
            relocation.SetScore(
                new AIHeadquartersRelocationProposalScorer().Score(context, relocation)
            );
            context.AddProposal(relocation);
            context.AddProposal(manufacture);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.Greater(manufacture.Score, relocation.Score);
            Assert.Greater(relocation.MaintenanceHeadroomCost, 0);
            CollectionAssert.AreEqual(new[] { relocation }, selected);
        }

        [TestCase(AIProductionDemandKind.Mine, BuildingType.Mine)]
        [TestCase(AIProductionDemandKind.Refinery, BuildingType.Refinery)]
        public void Select_WithEconomyRecoveryBelowMaintenanceReserve_SelectsProposal(
            AIProductionDemandKind kind,
            BuildingType buildingType
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "p1",
                empire.InstanceID,
                rawResourceNodes: buildingType == BuildingType.Mine ? 1 : 0
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            game.Config.AI.Selection.MaintenanceHeadroomReserve =
                context.Assessment.ProjectedMaintenanceHeadroom + 100;
            Building economyBuilding = AITestSceneBuilder.CreateBuildingTemplate(
                $"{kind}-template",
                buildingType,
                ManufacturingType.None
            );
            economyBuilding.MaintenanceCost = 10;
            AIManufactureProposal proposal = new AIManufactureProposal(
                new AIProductionDemand(
                    $"{kind}-demand",
                    kind,
                    ManufacturingType.Building,
                    buildingType,
                    planet,
                    1,
                    baseDemandPercent: 100
                ),
                planet,
                new Technology(economyBuilding),
                false
            );
            proposal.SetScore(100);
            context.AddProposal(proposal);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new[] { proposal }, selected);
        }

        [Test]
        public void Select_WithFacilityExpansionAtDistinctProducers_SelectsBothProposals()
        {
            AITurnContext context = CreateFacilityExpansionContext(
                allocationPercent: 30,
                out AIManufactureProposal first,
                out AIManufactureProposal second
            );
            context.AddProposal(first);
            context.AddProposal(second);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEquivalent(new[] { first, second }, selected);
        }

        [Test]
        public void Select_WithFacilityExpansionBeyondLegacyBudget_SelectsBothProposals()
        {
            AITurnContext context = CreateFacilityExpansionContext(
                allocationPercent: 10,
                out AIManufactureProposal first,
                out AIManufactureProposal second
            );
            context.AddProposal(first);
            context.AddProposal(second);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEquivalent(new[] { first, second }, selected);
        }

        [Test]
        public void Select_WithPartiallyAffordableFacilityBatch_PreservesFullBatchOrdering()
        {
            AITurnContext context = CreateFacilityExpansionContext(
                allocationPercent: 100,
                out AIManufactureProposal proposal,
                out AIManufactureProposal _,
                quantity: 4
            );
            context.Game.Config.AI.Selection.MaintenanceHeadroomReserve =
                context.Assessment.ProjectedMaintenanceHeadroom - 25;
            TestAIProposal independent = new TestAIProposal("independent");
            independent.SetScore(90);
            context.AddProposal(proposal);
            context.AddProposal(independent);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.AreEqual(2, selected.Count);
            Assert.AreEqual(2, ((AIManufactureProposal)selected[0]).GetManufacturingCount());
            Assert.AreEqual(4, proposal.GetManufacturingCount());
            Assert.Less(selected[0].Score, independent.Score);
            Assert.AreSame(independent, selected[1]);
        }

        [Test]
        public void Select_WithDiscretionaryProductionBelowRefinedReserve_SelectsProposal()
        {
            AITurnContext context = CreateRefinedMaterialReserveContext(out Planet producer);
            AIManufactureProposal proposal = CreateManufactureProposal(
                context,
                producer,
                AIProductionDemandKind.PlanetaryDefense,
                BuildingType.Defense
            );
            context.AddProposal(proposal);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new[] { proposal }, selected);
        }

        [TestCase(AIProductionDemandKind.Mine, BuildingType.Mine)]
        [TestCase(AIProductionDemandKind.Refinery, BuildingType.Refinery)]
        [TestCase(AIProductionDemandKind.ColonizationFleetSeedCapitalShip, BuildingType.None)]
        public void Select_WithStrategicReserveExemptionBelowRefinedReserve_SelectsProposal(
            AIProductionDemandKind demandKind,
            BuildingType buildingType
        )
        {
            AITurnContext context = CreateRefinedMaterialReserveContext(out Planet producer);
            AIManufactureProposal proposal = CreateManufactureProposal(
                context,
                producer,
                demandKind,
                buildingType
            );
            context.AddProposal(proposal);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new[] { proposal }, selected);
        }

        [TestCase(AIProductionDemandKind.FleetCapitalShip)]
        [TestCase(AIProductionDemandKind.FleetSeedCapitalShip)]
        [TestCase(AIProductionDemandKind.FleetStarfighter)]
        [TestCase(AIProductionDemandKind.FleetRegiment)]
        [TestCase(AIProductionDemandKind.GarrisonRegimentReserve)]
        public void Select_WithMilitaryProductionBelowRefinedReserve_SelectsProposal(
            AIProductionDemandKind demandKind
        )
        {
            AITurnContext context = CreateRefinedMaterialReserveContext(out Planet producer);
            AIManufactureProposal proposal = CreateManufactureProposal(
                context,
                producer,
                demandKind,
                BuildingType.None
            );
            context.AddProposal(proposal);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new[] { proposal }, selected);
        }

        [Test]
        public void Select_WithMultipleIdleShipyardFighters_SelectsConfiguredConcurrentCount()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.IdleShipyardFighterReserveCount = 1;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet higherPlanet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "higher-planet",
                empire.InstanceID
            );
            Planet lowerPlanet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "lower-planet",
                empire.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                higherPlanet,
                "higher-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                lowerPlanet,
                "lower-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Starfighter fighter = AITestSceneBuilder.CreateStarfighter(
                "fighter",
                empire.InstanceID
            );
            AIManufactureProposal higher = CreateIdleShipyardFighterProposal(
                higherPlanet,
                fighter,
                20
            );
            AIManufactureProposal lower = CreateIdleShipyardFighterProposal(
                lowerPlanet,
                fighter,
                10
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            context.AddProposal(lower);
            context.AddProposal(higher);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new[] { higher }, selected);
        }

        [Test]
        public void Select_WithRegularAndIdlePlanetaryFighters_CountsBothAgainstIdleLimit()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.IdleShipyardFighterReserveCount = 1;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet regularPlanet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "regular-planet",
                empire.InstanceID
            );
            Planet idlePlanet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "idle-planet",
                empire.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                regularPlanet,
                "regular-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                idlePlanet,
                "idle-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Starfighter fighter = AITestSceneBuilder.CreateStarfighter(
                "fighter",
                empire.InstanceID
            );
            AIManufactureProposal regular = CreatePlanetaryStarfighterProposal(
                regularPlanet,
                fighter,
                20,
                usesIdleShipyardCapacity: false
            );
            AIManufactureProposal idle = CreatePlanetaryStarfighterProposal(
                idlePlanet,
                fighter,
                10,
                usesIdleShipyardCapacity: true
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            context.AddProposal(idle);
            context.AddProposal(regular);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new[] { regular }, selected);
        }

        [TestCase(AIProductionDemandKind.ConstructionFacility, BuildingType.ConstructionFacility)]
        [TestCase(AIProductionDemandKind.Shipyard, BuildingType.Shipyard)]
        [TestCase(AIProductionDemandKind.TrainingFacility, BuildingType.TrainingFacility)]
        public void Select_WithStrategicFacilityBelowRefinedReserve_SelectsProposal(
            AIProductionDemandKind demandKind,
            BuildingType buildingType
        )
        {
            AITurnContext context = CreateRefinedMaterialReserveContext(out Planet producer);
            AIManufactureProposal proposal = CreateManufactureProposal(
                context,
                producer,
                demandKind,
                buildingType
            );
            context.AddProposal(proposal);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new[] { proposal }, selected);
        }

        [Test]
        public void Select_WithShipyardAndHigherScoredDefense_DrainsDefenseProduction()
        {
            AITurnContext context = CreateRefinedMaterialCommitmentContext(out Planet producer);
            AIManufactureProposal defense = CreateManufactureProposal(
                context,
                producer,
                AIProductionDemandKind.PlanetaryDefense,
                BuildingType.Defense,
                score: 100
            );
            AIManufactureProposal shipyard = CreateManufactureProposal(
                context,
                producer,
                AIProductionDemandKind.Shipyard,
                BuildingType.Shipyard,
                score: 1
            );
            context.AddProposal(defense);
            context.AddProposal(shipyard);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.IsEmpty(selected);
        }

        [Test]
        public void Select_WithQueuedDefenseAndStrategicManufacturing_DrainsDefenseProduction()
        {
            AITurnContext context = CreateRefinedMaterialCommitmentContext(out Planet producer);
            AIManufactureProposal higherScore = CreateManufactureProposal(
                context,
                producer,
                AIProductionDemandKind.PlanetaryDefense,
                BuildingType.Defense,
                constructionCost: 30,
                score: 100
            );
            AIManufactureProposal lowerScore = CreateManufactureProposal(
                context,
                producer,
                AIProductionDemandKind.Shipyard,
                BuildingType.Shipyard,
                constructionCost: 30,
                score: 90
            );
            context.AddProposal(lowerScore);
            context.AddProposal(higherScore);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.IsEmpty(selected);
        }

        [Test]
        public void Select_WithHigherScoredDefense_DrainsForStrategicManufacturing()
        {
            AITurnContext context = CreateRefinedMaterialCommitmentContext(out Planet producer);
            AIManufactureProposal higherTotalCost = CreateManufactureProposal(
                context,
                producer,
                AIProductionDemandKind.PlanetaryDefense,
                BuildingType.Defense,
                constructionCost: 60,
                score: 100
            );
            AIManufactureProposal affordable = CreateManufactureProposal(
                context,
                producer,
                AIProductionDemandKind.Shipyard,
                BuildingType.Shipyard,
                constructionCost: 40,
                score: 90
            );
            context.AddProposal(higherTotalCost);
            context.AddProposal(affordable);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.IsEmpty(selected);
        }

        [Test]
        public void Select_WithLongProductionOrder_UsesAverageHorizonConsumption()
        {
            AITurnContext context = CreateRefinedMaterialCommitmentContext(out Planet producer);
            AIManufactureProposal proposal = CreateManufactureProposal(
                context,
                producer,
                AIProductionDemandKind.PlanetaryDefense,
                BuildingType.Defense,
                constructionCost: 400,
                score: 100,
                isEmergencyDefense: true
            );
            context.AddProposal(proposal);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            CollectionAssert.AreEqual(new[] { proposal }, selected);
        }

        [Test]
        public void Select_WithHigherScoredReplacement_DrainsCurrentProduction()
        {
            AITurnContext context = CreateRefinedMaterialCommitmentContext(out Planet producer);
            AIManufactureProposal replacement = CreateManufactureProposal(
                context,
                producer,
                AIProductionDemandKind.Shipyard,
                BuildingType.Shipyard,
                score: 200
            );
            AIManufactureProposal continuation = CreateManufactureProposal(
                context,
                producer,
                AIProductionDemandKind.PlanetaryDefense,
                BuildingType.Defense,
                score: 100
            );
            context.AddProposal(replacement);
            context.AddProposal(continuation);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.AreEqual(0, selected.Count);
        }

        [Test]
        public void Select_WithLowerScoredStrategicReplacement_DrainsDefenseProduction()
        {
            AITurnContext context = CreateRefinedMaterialCommitmentContext(out Planet producer);
            AIManufactureProposal continuation = CreateManufactureProposal(
                context,
                producer,
                AIProductionDemandKind.PlanetaryDefense,
                BuildingType.Defense,
                score: 200
            );
            AIManufactureProposal replacement = CreateManufactureProposal(
                context,
                producer,
                AIProductionDemandKind.Shipyard,
                BuildingType.Shipyard,
                score: 100
            );
            context.AddProposal(continuation);
            context.AddProposal(replacement);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.IsEmpty(selected);
        }

        [Test]
        public void Select_WithUnavailablePreferredManufacturingProducer_PreservesPrimaryOrdering()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "producer-system");
            Planet preferredProducer = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "preferred-producer",
                empire.InstanceID,
                energyCapacity: 20,
                rawResourceNodes: 2
            );
            Planet fallbackProducer = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "fallback-producer",
                empire.InstanceID,
                energyCapacity: 20,
                rawResourceNodes: 2
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                preferredProducer,
                "preferred-construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                fallbackProducer,
                "fallback-construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            Building mine = AITestSceneBuilder.CreateBuildingTemplate(
                "mine-template",
                BuildingType.Mine,
                ManufacturingType.Building
            );
            AIProductionDemand preferredDemand = new AIProductionDemand(
                "preferred-demand",
                AIProductionDemandKind.Mine,
                ManufacturingType.Building,
                BuildingType.Mine,
                preferredProducer,
                1,
                baseDemandPercent: 100
            );
            AIProductionDemand flexibleDemand = new AIProductionDemand(
                "flexible-demand",
                AIProductionDemandKind.Mine,
                ManufacturingType.Building,
                BuildingType.Mine,
                fallbackProducer,
                1,
                baseDemandPercent: 90
            );
            AIManufactureProposal preferredProposal = new AIManufactureProposal(
                preferredDemand,
                preferredProducer,
                new Technology(mine),
                false
            );
            AIManufactureProposal flexibleProposal = new AIManufactureProposal(
                (new[] { preferredProducer, fallbackProducer })
                    .Select(candidateProducer => new AIManufactureProposal(
                        flexibleDemand,
                        candidateProducer,
                        new Technology(mine),
                        false
                    ))
                    .ToArray(),
                carriesReducedCountAcrossAlternatives: true
            );
            preferredProposal.SetScore(100);
            flexibleProposal.SetScore(90);
            TestAIProposal independent = new TestAIProposal("independent");
            independent.SetScore(80);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            context.AddProposal(preferredProposal);
            context.AddProposal(flexibleProposal);
            context.AddProposal(independent);

            List<AIProposal> selected = new AISelectionPhase().Select(context);

            Assert.AreEqual(3, selected.Count);
            Assert.AreSame(preferredProposal, selected[0]);
            AIManufactureProposal selectedFallback = (AIManufactureProposal)selected[1];
            Assert.AreSame(fallbackProducer, selectedFallback.ProducerPlanet);
            Assert.AreEqual(
                new AIProductionProposalScorer().Score(context, selectedFallback),
                selectedFallback.Score
            );
            Assert.Less(selectedFallback.Score, independent.Score);
            Assert.AreSame(independent, selected[2]);
            Assert.AreSame(preferredProducer, flexibleProposal.ProducerPlanet);
        }

        [Test]
        public void Execute_Default_StoresSelectedProposalsOnContext()
        {
            AITurnContext context = CreateEmptyContext();
            TestAIProposal proposal = new TestAIProposal("proposal");
            proposal.SetScore(10);
            context.AddProposal(proposal);

            new AISelectionPhase().Execute(context);

            Assert.AreEqual(1, context.SelectedProposals.Count);
            Assert.AreSame(proposal, context.SelectedProposals[0]);
        }

        [Test]
        public void Select_PreferredProducerBusy_StartsAtAvailableAlternative()
        {
            AITurnContext context = CreateUnitSchedulingContext(
                out Planet weak,
                out Planet strong,
                out AIManufactureProposal request
            );
            CapitalShip existing = QueueSchedulingShip(context, strong, 4);
            context.AddProposal(request);

            AIManufactureProposal selected = new AISelectionPhase()
                .Select(context)
                .OfType<AIManufactureProposal>()
                .Single();
            selected.Execute(context);

            Assert.AreSame(weak, selected.ProducerPlanet);
            Assert.AreSame(
                existing,
                strong.GetManufacturingQueue()[ManufacturingType.Ship].Single()
            );
            Assert.AreEqual(1, weak.GetManufacturingQueue()[ManufacturingType.Ship].Count);
        }

        [Test]
        public void Select_PreferredFacilitiesStillInTransit_StartsWithCurrentCapacity()
        {
            AITurnContext context = CreateUnitSchedulingContext(
                out Planet weak,
                out Planet strong,
                out AIManufactureProposal request
            );
            foreach (Building facility in strong.GetProductionFacilities(ManufacturingType.Ship))
            {
                facility.ManufacturingStatus = ManufacturingStatus.Delivering;
                facility.Movement = new MovementState { TransitTicks = 5 };
            }
            context.AddProposal(request);

            AIManufactureProposal selected = new AISelectionPhase()
                .Select(context)
                .OfType<AIManufactureProposal>()
                .Single();
            selected.Execute(context);

            Assert.AreSame(weak, selected.ProducerPlanet);
            Assert.IsNull(
                ManufacturingQueries.GetManufacturingOrder(strong, ManufacturingType.Ship)
            );
        }

        [Test]
        public void Select_AllProducersBusy_PreservesExistingOrders()
        {
            AITurnContext context = CreateUnitSchedulingContext(
                out Planet weak,
                out Planet strong,
                out AIManufactureProposal request
            );
            CapitalShip weakOrder = QueueSchedulingShip(context, weak, 4);
            CapitalShip strongOrder = QueueSchedulingShip(context, strong, 4);
            context.AddProposal(request);

            Assert.IsEmpty(new AISelectionPhase().Select(context));
            Assert.AreSame(
                weakOrder,
                weak.GetManufacturingQueue()[ManufacturingType.Ship].Single()
            );
            Assert.AreSame(
                strongOrder,
                strong.GetManufacturingQueue()[ManufacturingType.Ship].Single()
            );
        }

        /// <summary>
        /// Creates two producers and a single ranked capital-ship request.
        /// </summary>
        /// <param name="weak">The single-shipyard producer.</param>
        /// <param name="strong">The five-shipyard producer.</param>
        /// <param name="request">The scored fleet production request.</param>
        /// <returns>The faction's production turn context.</returns>
        private static AITurnContext CreateUnitSchedulingContext(
            out Planet weak,
            out Planet strong,
            out AIManufactureProposal request
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "scheduling-sector");
            weak = AITestSceneBuilder.AddPlanet(game, sector, "weak", empire.InstanceID);
            strong = AITestSceneBuilder.AddPlanet(game, sector, "strong", empire.InstanceID);
            AITestSceneBuilder.AddProductionFacility(
                game,
                weak,
                "weak-yard",
                BuildingType.Shipyard,
                ManufacturingType.Ship,
                processRate: 1
            );
            for (int index = 0; index < 5; index++)
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    strong,
                    $"strong-yard-{index}",
                    BuildingType.Shipyard,
                    ManufacturingType.Ship,
                    processRate: 1
                );
            Fleet fleet = EntityFactory.CreateFleet("scheduling-fleet", empire.InstanceID);
            game.AttachNode(fleet, weak);
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip(
                "requested-ship",
                empire.InstanceID
            );
            ship.ConstructionCost = 100;
            AIProductionDemand demand = new AIProductionDemand(
                "scheduling-request",
                AIProductionDemandKind.FleetCapitalShip,
                ManufacturingType.Ship,
                BuildingType.None,
                fleet,
                1
            );
            request = new AIManufactureProposal(
                (new[] { strong, weak })
                    .Select(candidateProducer => new AIManufactureProposal(
                        demand,
                        candidateProducer,
                        new Technology(ship),
                        true
                    ))
                    .ToArray(),
                carriesReducedCountAcrossAlternatives: true
            );
            request.SetScore(100);
            return AITestSceneBuilder.CreateContext(game, empire);
        }

        /// <summary>
        /// Commits a capital ship to a producer's current manufacturing queue.
        /// </summary>
        /// <param name="context">The faction's turn context.</param>
        /// <param name="producer">The planet manufacturing the ship.</param>
        /// <param name="cost">The ship's construction cost.</param>
        /// <returns>The queued ship.</returns>
        private static CapitalShip QueueSchedulingShip(
            AITurnContext context,
            Planet producer,
            int cost
        )
        {
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip(
                $"current-ship-{producer.InstanceID}",
                context.Faction.InstanceID
            );
            ship.InstanceID = $"current-ship-{producer.InstanceID}";
            ship.ConstructionCost = cost;
            ship.ManufacturingStatus = ManufacturingStatus.Building;
            ship.ProducerPlanetID = producer.InstanceID;
            Fleet fleet = EntityFactory.CreateFleet(
                $"current-fleet-{producer.InstanceID}",
                context.Faction.InstanceID
            );
            context.Game.AttachNode(fleet, producer);
            context.Game.AttachNode(ship, fleet);
            producer.AddToManufacturingQueue(ship);
            return ship;
        }

        /// <summary>
        /// Creates a manufacturing context below its configured refined-material reserve.
        /// </summary>
        /// <param name="producer">The planet containing the construction facility.</param>
        /// <returns>The configured AI turn context.</returns>
        private static AITurnContext CreateRefinedMaterialReserveContext(out Planet producer)
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Selection.RefinedMaterialReservePercent = 50;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "reserve-system");
            producer = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "reserve-producer",
                empire.InstanceID,
                energyCapacity: 20,
                rawResourceNodes: 3
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                producer,
                "construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                producer,
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                producer,
                "training-facility",
                BuildingType.TrainingFacility,
                ManufacturingType.Troop
            );
            for (int index = 0; index < 2; index++)
            {
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    producer,
                    $"mine-{index}",
                    BuildingType.Mine,
                    ManufacturingType.None
                );
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    producer,
                    $"refinery-{index}",
                    BuildingType.Refinery,
                    ManufacturingType.None
                );
            }

            empire.RefinedMaterialStockpile = 0;
            return AITestSceneBuilder.CreateContext(game, empire);
        }

        /// <summary>
        /// Creates one scored opportunistic planetary-fighter proposal.
        /// </summary>
        /// <param name="planet">Planet producing and receiving the fighter.</param>
        /// <param name="fighter">Fighter technology to manufacture.</param>
        /// <param name="score">Proposal score.</param>
        /// <returns>The configured proposal.</returns>
        private static AIManufactureProposal CreateIdleShipyardFighterProposal(
            Planet planet,
            Starfighter fighter,
            double score
        ) => CreatePlanetaryStarfighterProposal(planet, fighter, score, true);

        /// <summary>
        /// Creates one scored planetary-fighter proposal.
        /// </summary>
        /// <param name="planet">Planet producing and receiving the fighter.</param>
        /// <param name="fighter">Fighter technology to manufacture.</param>
        /// <param name="score">Proposal score.</param>
        /// <param name="usesIdleShipyardCapacity">Whether the proposal uses idle capacity.</param>
        /// <returns>The configured proposal.</returns>
        private static AIManufactureProposal CreatePlanetaryStarfighterProposal(
            Planet planet,
            Starfighter fighter,
            double score,
            bool usesIdleShipyardCapacity
        )
        {
            AIProductionDemand demand = new AIProductionDemand(
                $"idle-{planet.InstanceID}",
                AIProductionDemandKind.PlanetaryStarfighterReserve,
                ManufacturingType.Ship,
                BuildingType.None,
                planet,
                1,
                targetCount: 1,
                baseDemandPercent: 1,
                usesIdleShipyardCapacity: usesIdleShipyardCapacity
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                planet,
                new Technology(fighter),
                true
            );
            proposal.SetScore(score);
            return proposal;
        }

        /// <summary>
        /// Creates a manufacturing context with a bounded refined-material commitment budget.
        /// </summary>
        /// <param name="producer">The planet containing the construction facilities.</param>
        /// <returns>The configured AI turn context.</returns>
        private static AITurnContext CreateRefinedMaterialCommitmentContext(out Planet producer)
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Selection.RefinedMaterialReservePercent = 50;
            empire.Settings.RefinementMultiplier = 100;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "commitment-system");
            producer = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "commitment-producer",
                empire.InstanceID,
                energyCapacity: 20,
                rawResourceNodes: 2
            );
            for (int index = 0; index < 2; index++)
            {
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    producer,
                    $"construction-yard-{index}",
                    BuildingType.ConstructionFacility,
                    ManufacturingType.Building
                );
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    producer,
                    $"mine-{index}",
                    BuildingType.Mine,
                    ManufacturingType.None
                );
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    producer,
                    $"refinery-{index}",
                    BuildingType.Refinery,
                    ManufacturingType.None
                );
            }

            Building queued = AITestSceneBuilder.CreateBuildingTemplate(
                "reserve-Defense",
                BuildingType.Defense,
                ManufacturingType.None
            );
            queued.OwnerInstanceID = empire.InstanceID;
            queued.ConstructionCost = 100;
            queued.ManufacturingStatus = ManufacturingStatus.Building;
            game.AttachNode(queued, producer);
            producer.AddToManufacturingQueue(queued);
            empire.RefinedMaterialStockpile = 209;
            return AITestSceneBuilder.CreateContext(game, empire);
        }

        /// <summary>
        /// Adds a planet with one building-production facility.
        /// </summary>
        /// <param name="game">The game receiving the planet.</param>
        /// <param name="sector">The containing sector.</param>
        /// <param name="faction">The owning faction.</param>
        /// <param name="planetId">The planet identifier.</param>
        /// <returns>The building producer planet.</returns>
        private static Planet AddBuildingProducer(
            GameRoot game,
            PlanetSector sector,
            Faction faction,
            string planetId
        )
        {
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                planetId,
                faction.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                $"{planetId}-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            return planet;
        }

        /// <summary>
        /// Creates a scored building proposal with distinct producer and destination planets.
        /// </summary>
        /// <param name="destination">The planet receiving the building.</param>
        /// <param name="producer">The planet manufacturing the building.</param>
        /// <param name="kind">The demand category.</param>
        /// <param name="buildingType">The manufactured building type.</param>
        /// <param name="score">The proposal score.</param>
        /// <param name="maintenanceCost">The manufactured building's maintenance cost.</param>
        /// <returns>The scored manufacturing proposal.</returns>
        private static AIManufactureProposal CreateBuildingProposal(
            Planet destination,
            Planet producer,
            AIProductionDemandKind kind,
            BuildingType buildingType,
            double score,
            int maintenanceCost = 0
        )
        {
            Building building = AITestSceneBuilder.CreateBuildingTemplate(
                $"{buildingType}-template",
                buildingType,
                ManufacturingType.None
            );
            building.MaintenanceCost = maintenanceCost;
            AIManufactureProposal proposal = new AIManufactureProposal(
                new AIProductionDemand(
                    $"{kind}-demand",
                    kind,
                    ManufacturingType.Building,
                    buildingType,
                    destination,
                    1,
                    baseDemandPercent: 100
                ),
                producer,
                new Technology(building),
                false
            );
            proposal.SetScore(score);
            return proposal;
        }

        /// <summary>
        /// Creates a scored building-production proposal.
        /// </summary>
        /// <param name="producer">The planet producing the building.</param>
        /// <param name="kind">The demand kind represented by the proposal.</param>
        /// <param name="buildingType">The building type to manufacture.</param>
        /// <returns>The building-production proposal.</returns>
        /// <param name="context">The context.</param>
        /// <param name="constructionCost">The construction cost.</param>
        /// <param name="score">The score.</param>
        /// <param name="isEmergencyDefense">Whether the defense is essential rather than spare-capacity filler.</param>
        /// <param name="maintenanceCost">The manufactured product's maintenance cost.</param>
        private static AIManufactureProposal CreateManufactureProposal(
            AITurnContext context,
            Planet producer,
            AIProductionDemandKind kind,
            BuildingType buildingType,
            int constructionCost = 10,
            double score = 100,
            bool isEmergencyDefense = false,
            int maintenanceCost = 0
        )
        {
            IManufacturable product;
            ManufacturingType manufacturingType;
            Rebellion.SceneGraph.ContainerNode destination = producer;
            switch (kind)
            {
                case AIProductionDemandKind.FleetCapitalShip:
                case AIProductionDemandKind.FleetSeedCapitalShip:
                case AIProductionDemandKind.ColonizationFleetSeedCapitalShip:
                    product = AITestSceneBuilder.CreateCapitalShip(
                        $"reserve-{kind}",
                        context.Faction.InstanceID
                    );
                    manufacturingType = ManufacturingType.Ship;
                    break;
                case AIProductionDemandKind.FleetStarfighter:
                    product = AITestSceneBuilder.CreateStarfighter(
                        $"reserve-{kind}",
                        context.Faction.InstanceID
                    );
                    manufacturingType = ManufacturingType.Ship;
                    break;
                case AIProductionDemandKind.FleetRegiment:
                case AIProductionDemandKind.GarrisonRegimentReserve:
                    product = AITestSceneBuilder.CreateRegiment(
                        $"reserve-{kind}",
                        context.Faction.InstanceID
                    );
                    manufacturingType = ManufacturingType.Troop;
                    break;
                default:
                    product = AITestSceneBuilder.CreateBuildingTemplate(
                        $"reserve-{buildingType}",
                        buildingType,
                        ManufacturingType.None
                    );
                    manufacturingType = ManufacturingType.Building;
                    break;
            }

            product.ConstructionCost = constructionCost;
            product.MaintenanceCost = maintenanceCost;

            if (
                kind
                is AIProductionDemandKind.FleetCapitalShip
                    or AIProductionDemandKind.FleetStarfighter
                    or AIProductionDemandKind.FleetRegiment
            )
            {
                Fleet fleet = EntityFactory.CreateFleet(
                    $"reserve-fleet-{kind}",
                    context.Faction.InstanceID
                );
                CapitalShip carrier = AITestSceneBuilder.CreateCapitalShip(
                    $"reserve-carrier-{kind}",
                    context.Faction.InstanceID,
                    regimentCapacity: 4,
                    starfighterCapacity: 4
                );
                context.Game.AttachNode(fleet, producer);
                context.Game.AttachNode(carrier, fleet);
                destination = fleet;
            }
            AIProductionDemand demand = new AIProductionDemand(
                $"reserve-{kind}",
                kind,
                manufacturingType,
                buildingType,
                destination,
                1,
                baseDemandPercent: 100,
                isEmergencyDefense: isEmergencyDefense
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                producer,
                new Technology(product),
                false
            );
            proposal.SetScore(score);
            return proposal;
        }

        /// <summary>
        /// Creates two independent production-facility proposals backed by separate producers.
        /// </summary>
        /// <param name="allocationPercent">Shared maintenance allocation for production facilities.</param>
        /// <param name="first">The first facility proposal.</param>
        /// <param name="second">The second facility proposal.</param>
        /// <param name="quantity">The quantity represented by the first proposal.</param>
        /// <returns>The configured AI turn context.</returns>
        private static AITurnContext CreateFacilityExpansionContext(
            int allocationPercent,
            out AIManufactureProposal first,
            out AIManufactureProposal second,
            int quantity = 1
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Selection.MaintenanceHeadroomReserve = 0;
            game.Config.AI.Infrastructure.ProductionFacilityMaintenanceAllocationPercent =
                allocationPercent;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "facility-system");
            Planet firstPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "first-producer",
                empire.InstanceID,
                rawResourceNodes: 1
            );
            Planet secondPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "second-producer",
                empire.InstanceID,
                rawResourceNodes: 1
            );
            foreach (Planet planet in new[] { firstPlanet, secondPlanet })
            {
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    planet,
                    $"{planet.InstanceID}-construction-yard",
                    BuildingType.ConstructionFacility,
                    ManufacturingType.Building
                );
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    planet,
                    $"{planet.InstanceID}-mine",
                    BuildingType.Mine,
                    ManufacturingType.None
                );
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    planet,
                    $"{planet.InstanceID}-refinery",
                    BuildingType.Refinery,
                    ManufacturingType.None
                );
            }

            Building shipyard = AITestSceneBuilder.CreateBuildingTemplate(
                "shipyard-template",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            shipyard.MaintenanceCost = 10;
            first = CreateFacilityExpansionProposal(firstPlanet, shipyard, 100, quantity);
            second = CreateFacilityExpansionProposal(secondPlanet, shipyard, 90);
            return AITestSceneBuilder.CreateContext(game, empire);
        }

        /// <summary>
        /// Creates a scored shipyard-expansion proposal at one planet.
        /// </summary>
        /// <param name="planet">The producer and destination planet.</param>
        /// <param name="shipyard">The shipyard template.</param>
        /// <param name="score">The proposal score.</param>
        /// <param name="quantity">The proposed shipyard count.</param>
        /// <returns>The facility-expansion proposal.</returns>
        private static AIManufactureProposal CreateFacilityExpansionProposal(
            Planet planet,
            Building shipyard,
            float score,
            int quantity = 1
        )
        {
            AIProductionDemand demand = new AIProductionDemand(
                $"shipyard-{planet.InstanceID}",
                AIProductionDemandKind.Shipyard,
                ManufacturingType.Building,
                BuildingType.Shipyard,
                planet,
                quantity,
                baseDemandPercent: 100
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                planet,
                new Technology(shipyard),
                false
            );
            proposal.SetScore(score);
            return proposal;
        }

        /// <summary>
        /// Creates empty context.
        /// </summary>
        /// <returns>The created empty context.</returns>
        private static AITurnContext CreateEmptyContext()
        {
            return new AITurnContext(
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null
            );
        }

        /// <summary>
        /// Creates a valid transfer from a stationary headquarters defender to an attack fleet.
        /// </summary>
        /// <param name="context">The faction turn context.</param>
        /// <returns>The transfer of the defender's spare hull.</returns>
        private static AITransferUnitProposal CreateDefenseTransfer(out AITurnContext context)
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction faction, out Faction enemy);
            game.Config.AI.FleetDeployment.MinimumDefenseStrength = 1000;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet planet = AITestSceneBuilder.AddPlanet(game, sector, "hq", faction.InstanceID);
            planet.IsHeadquarters = true;
            faction.HQInstanceID = planet.InstanceID;
            Planet target = AITestSceneBuilder.AddPlanet(game, sector, "enemy", enemy.InstanceID);
            Fleet donor = EntityFactory.CreateFleet("donor", faction.InstanceID);
            donor.RoleType = FleetRoleType.Battle;
            donor.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Defend,
                Status = FleetOrderStatus.Ready,
                TargetPlanetId = planet.InstanceID,
            };
            Fleet receiver = EntityFactory.CreateFleet("receiver", faction.InstanceID);
            receiver.RoleType = FleetRoleType.Battle;
            receiver.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Building,
                TargetPlanetId = target.InstanceID,
            };
            game.AttachNode(donor, planet);
            game.AttachNode(receiver, planet);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip("anchor", faction.InstanceID, 1000),
                donor
            );
            CapitalShip spare = AITestSceneBuilder.CreateCapitalShip(
                "spare",
                faction.InstanceID,
                500
            );
            game.AttachNode(spare, donor);
            context = AITestSceneBuilder.CreateContext(game, faction);
            return new AITransferUnitProposal(donor, receiver, spare, receiver, target);
        }

        /// <summary>
        /// Creates an independently producible fleet reinforcement or fleet seed.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="id">The unique producer and fleet prefix.</param>
        /// <param name="kind">The military production demand.</param>
        /// <param name="score">The proposal score.</param>
        /// <returns>The scored manufacturing proposal.</returns>
        private static AIManufactureProposal CreateFleetProductionProposal(
            AITurnContext context,
            string id,
            AIProductionDemandKind kind,
            double score
        )
        {
            GameRoot game = context.Game;
            string owner = context.Faction.InstanceID;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, id + "-sector");
            Planet producer = AITestSceneBuilder.AddPlanet(game, sector, id + "-producer", owner);
            bool isTroop = kind == AIProductionDemandKind.FleetRegiment;
            ManufacturingType manufacturingType = isTroop
                ? ManufacturingType.Troop
                : ManufacturingType.Ship;
            AITestSceneBuilder.AddProductionFacility(
                game,
                producer,
                id + "-facility",
                isTroop ? BuildingType.TrainingFacility : BuildingType.Shipyard,
                manufacturingType
            );
            IManufacturable product = kind switch
            {
                AIProductionDemandKind.FleetRegiment => AITestSceneBuilder.CreateRegiment(
                    id + "-troop",
                    owner
                ),
                AIProductionDemandKind.FleetStarfighter => AITestSceneBuilder.CreateStarfighter(
                    id + "-fighter",
                    owner
                ),
                _ => AITestSceneBuilder.CreateCapitalShip(id + "-hull", owner),
            };
            product.ConstructionCost = 0;
            Rebellion.SceneGraph.ContainerNode destination = producer;
            if (kind != AIProductionDemandKind.FleetSeedCapitalShip)
            {
                Fleet fleet = EntityFactory.CreateFleet(id + "-fleet", owner);
                fleet.RoleType = FleetRoleType.Battle;
                fleet.Order = new FleetOrder { OrderType = FleetOrderType.Attack };
                game.AttachNode(fleet, producer);
                game.AttachNode(
                    AITestSceneBuilder.CreateCapitalShip(
                        id + "-carrier",
                        owner,
                        regimentCapacity: 4,
                        starfighterCapacity: 4
                    ),
                    fleet
                );
                destination = fleet;
            }
            AIProductionDemand demand = new(
                id,
                kind,
                manufacturingType,
                BuildingType.None,
                destination,
                1,
                baseDemandPercent: 100
            );
            AIManufactureProposal proposal = new(demand, producer, new Technology(product), false);
            proposal.SetScore(score);
            return proposal;
        }
    }
}

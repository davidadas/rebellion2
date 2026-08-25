using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Planners;
using Rebellion.AI.Planners.Demand;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Movement;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Proposals
{
    [TestFixture]
    public class AIManufactureProposalTests
    {
        [Test]
        public void TrainingFacilityDemand_UsesDefensiveMaintenanceReserve()
        {
            AIProductionDemand demand = new AIProductionDemand(
                "training-facility-demand",
                AIProductionDemandKind.TrainingFacility,
                ManufacturingType.Building,
                BuildingType.TrainingFacility,
                null,
                1,
                100
            );

            Assert.IsTrue(demand.UsesDefensiveReserve);
        }

        [Test]
        public void GetClaimKeys_WithBuildingDemand_ClaimsDemandAndDestination()
        {
            Planet producer = new Planet { InstanceID = "producer" };
            Planet destination = new Planet { InstanceID = "destination" };
            AIProductionDemand demand = CreateBuildingDemand(destination);
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                producer,
                new Technology(AITestSceneBuilder.CreateBuildingTemplate("mine", BuildingType.Mine))
            );

            IReadOnlyList<string> claimKeys = proposal.GetClaimKeys();

            CollectionAssert.Contains(claimKeys, "production:demand:mine-demand");
            CollectionAssert.DoesNotContain(claimKeys, "production:building:producer");
            CollectionAssert.Contains(claimKeys, "production:building-destination:destination");
        }

        [Test]
        public void GetClaimKeys_WithFleetCapitalShipDemand_ClaimsCapitalReinforcement()
        {
            Planet producer = new Planet { InstanceID = "producer" };
            Fleet destination = EntityFactory.CreateFleet("fleet", "empire");
            AIProductionDemand demand = new AIProductionDemand(
                "capital-demand",
                AIProductionDemandKind.FleetCapitalShip,
                ManufacturingType.Ship,
                BuildingType.None,
                destination,
                1,
                100
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                producer,
                new Technology(AITestSceneBuilder.CreateCapitalShip("capital", "empire"))
            );

            IReadOnlyList<string> claimKeys = proposal.GetClaimKeys();

            CollectionAssert.Contains(claimKeys, "fleet:capital-reinforcement:fleet");
        }

        [Test]
        public void Execute_WithValidBuildingProposal_QueuesManufacturing()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector planetSector = AITestSceneBuilder.AddSector(game, "sector1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "resource-world",
                empire.InstanceID,
                rawResourceNodes: 4
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            Building mine = AITestSceneBuilder.CreateBuildingTemplate(
                "mine-template",
                BuildingType.Mine
            );
            mine.MaintenanceCost = 0;
            AIManufactureProposal proposal = new AIManufactureProposal(
                CreateBuildingDemand(planet),
                planet,
                new Technology(mine)
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            proposal.Execute(context);

            Assert.AreEqual(1, planet.GetManufacturingQueue()[ManufacturingType.Building].Count);
        }

        [Test]
        public void Execute_WithFacilityBatch_QueuesExactlyCalculatedQuantity()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "construction-world",
                empire.InstanceID,
                energyCapacity: 12,
                rawResourceNodes: 2
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            for (int index = 0; index < 2; index++)
            {
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    planet,
                    $"mine-{index}",
                    BuildingType.Mine,
                    ManufacturingType.None
                );
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    planet,
                    $"refinery-{index}",
                    BuildingType.Refinery,
                    ManufacturingType.None
                );
            }
            Building shipyard = AITestSceneBuilder.CreateBuildingTemplate(
                "shipyard-template",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            shipyard.MaintenanceCost = 1;
            AIProductionDemand demand = new AIProductionDemand(
                "shipyard-demand",
                AIProductionDemandKind.Shipyard,
                ManufacturingType.Building,
                BuildingType.Shipyard,
                planet,
                3,
                100
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                planet,
                new Technology(shipyard)
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            proposal.Execute(context);

            List<IManufacturable> queue = planet.GetManufacturingQueue()[
                ManufacturingType.Building
            ];
            Assert.AreEqual(3, proposal.GetMaintenanceCost());
            Assert.AreEqual(3, queue.Count);
            Assert.AreEqual(3, queue.OfType<Building>().Count());
            Assert.IsTrue(
                queue.All(item => item.GetManufacturingStatus() == ManufacturingStatus.Building)
            );
            Assert.AreEqual(3, planet.GetTotalBuildingTypeCount(BuildingType.Shipyard));
        }

        [Test]
        public void Execute_WithFacilityUpgrade_ReplacesExactlyOneFacility()
        {
            (
                GameRoot _,
                Faction _,
                Planet planet,
                Building replacement,
                Building remaining,
                AIManufactureProposal proposal,
                AITurnContext context
            ) = CreateFacilityUpgradeProposalScene(2, 0, 0);

            proposal.Execute(context);

            List<Building> queued = planet
                .GetManufacturingQueue()[ManufacturingType.Building]
                .OfType<Building>()
                .ToList();
            Assert.IsNull(replacement.GetParent());
            Assert.AreSame(planet, remaining.GetParent());
            Assert.AreEqual(1, queued.Count);
            Assert.AreEqual("advanced-shipyard", queued[0].GetDisplayName());
            Assert.AreEqual(ManufacturingStatus.Building, queued[0].ManufacturingStatus);
            Assert.AreEqual(2, planet.GetTotalBuildingTypeCount(BuildingType.Shipyard));
        }

        [Test]
        public void Execute_WithOnlyOneFacility_DoesNotRemoveIt()
        {
            (
                GameRoot _,
                Faction _,
                Planet planet,
                Building replacement,
                Building _,
                AIManufactureProposal proposal,
                AITurnContext context
            ) = CreateFacilityUpgradeProposalScene(1, 0, 0);

            proposal.Execute(context);

            Assert.AreSame(planet, replacement.GetParent());
            Assert.AreEqual(1, planet.GetTotalBuildingTypeCount(BuildingType.Shipyard));
            Assert.IsFalse(planet.GetManufacturingQueue().ContainsKey(ManufacturingType.Building));
        }

        [Test]
        public void CanExecute_WithFacilityUpgradeOnOverCapacityPlanet_ReturnsFalse()
        {
            (
                GameRoot _,
                Faction _,
                Planet planet,
                Building _,
                Building _,
                AIManufactureProposal proposal,
                AITurnContext context
            ) = CreateFacilityUpgradeProposalScene(2, 0, 0);
            planet.EnergyCapacity = planet.GetEnergyUsed() - 1;

            bool canExecute = proposal.CanExecute(context);

            Assert.IsFalse(canExecute);
        }

        [Test]
        public void GetMaintenanceCost_WithFacilityUpgrade_ReturnsNetIncrease()
        {
            (
                GameRoot _,
                Faction _,
                Planet _,
                Building _,
                Building _,
                AIManufactureProposal proposal,
                AITurnContext _
            ) = CreateFacilityUpgradeProposalScene(2, 10, 17);

            Assert.AreEqual(7, proposal.GetMaintenanceCost());
        }

        [Test]
        public void Execute_WithPlanetaryShieldBatch_QueuesCompleteShieldNetwork()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "defense-world",
                empire.InstanceID,
                energyCapacity: 3
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            Building shield = AITestSceneBuilder.CreateBuildingTemplate(
                "shield-template",
                BuildingType.Defense
            );
            shield.DefenseFacilityClass = DefenseFacilityClass.Shield;
            shield.MaintenanceCost = 0;
            AIProductionDemand demand = new AIProductionDemand(
                "planetary-shield-demand",
                AIProductionDemandKind.PlanetaryDefense,
                ManufacturingType.Building,
                BuildingType.Defense,
                planet,
                2,
                100
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                planet,
                new Technology(shield)
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            proposal.Execute(context);

            List<IManufacturable> queue = planet.GetManufacturingQueue()[
                ManufacturingType.Building
            ];
            Assert.AreEqual(2, queue.Count);
            Assert.AreEqual(2, planet.GetTotalBuildingTypeCount(BuildingType.Defense));
        }

        [Test]
        public void Execute_WithGarrisonBatch_QueuesCalculatedRegimentCount()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "garrison-world",
                empire.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "training-facility",
                BuildingType.TrainingFacility,
                ManufacturingType.Troop
            );
            Regiment regiment = AITestSceneBuilder.CreateRegiment(
                "regiment-template",
                empire.InstanceID
            );
            regiment.MaintenanceCost = 0;
            AIProductionDemand demand = new AIProductionDemand(
                "garrison-demand",
                AIProductionDemandKind.GarrisonRegimentReserve,
                ManufacturingType.Troop,
                BuildingType.None,
                planet,
                6,
                100
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                planet,
                new Technology(regiment)
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            proposal.Execute(context);

            List<IManufacturable> queue = planet.GetManufacturingQueue()[ManufacturingType.Troop];
            Assert.AreEqual(6, queue.Count);
            Assert.AreEqual(6, planet.GetAllRegiments().Count);
        }

        [Test]
        public void ManufacturedFacilityTotal_IncrementsOnlyAfterCompletion()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "construction-world",
                empire.InstanceID,
                energyCapacity: 4
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            Building shipyard = AITestSceneBuilder.CreateBuildingTemplate(
                "shipyard-template",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            System.Type trackerType = System
                .AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly =>
                    assembly.GetType("HeadlessSimulationRunner+ManufacturedUnitTracker")
                )
                .Single(type => type != null);
            object tracker = System.Activator.CreateInstance(trackerType, nonPublic: true);
            System.Reflection.MethodInfo recordInitialState = trackerType.GetMethod(
                "RecordInitialState"
            );
            System.Reflection.MethodInfo recordTick = trackerType.GetMethod("RecordTick");
            System.Reflection.MethodInfo getManufacturedBuildings = trackerType.GetMethod(
                "GetManufacturedBuildings",
                new[] { typeof(string), typeof(BuildingType) }
            );
            recordInitialState.Invoke(tracker, new object[] { game });

            Assert.IsTrue(
                context.Manufacturing.StartManufacturing(
                    planet,
                    shipyard,
                    planet,
                    1,
                    empire.InstanceID
                )
            );
            recordTick.Invoke(tracker, new object[] { game });

            Assert.AreEqual(
                0,
                getManufacturedBuildings.Invoke(
                    tracker,
                    new object[] { empire.InstanceID, BuildingType.Shipyard }
                )
            );

            Building queuedShipyard = planet
                .GetManufacturingQueue()[ManufacturingType.Building]
                .OfType<Building>()
                .Single();
            queuedShipyard.ManufacturingStatus = ManufacturingStatus.Complete;
            recordTick.Invoke(tracker, new object[] { game });
            recordTick.Invoke(tracker, new object[] { game });

            Assert.AreEqual(
                1,
                getManufacturedBuildings.Invoke(
                    tracker,
                    new object[] { empire.InstanceID, BuildingType.Shipyard }
                )
            );
        }

        [Test]
        public void Execute_WithFullDestination_DoesNotReplaceExistingFacility()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "headquarters",
                empire.InstanceID,
                energyCapacity: 1
            );
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            Building replaceableShipyard = AITestSceneBuilder.AddProductionFacility(
                game,
                headquarters,
                "existing-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Planet producer = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "producer",
                empire.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                producer,
                "construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            Building shield = AITestSceneBuilder.CreateBuildingTemplate(
                "shield",
                BuildingType.Defense
            );
            shield.MaintenanceCost = 0;
            AIProductionDemand demand = new AIProductionDemand(
                "headquarters-defense",
                AIProductionDemandKind.PlanetaryDefense,
                ManufacturingType.Building,
                BuildingType.Defense,
                headquarters,
                1,
                100
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                producer,
                new Technology(shield)
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            proposal.Execute(context);

            Assert.AreSame(headquarters, replaceableShipyard.GetParent());
            Assert.AreEqual(0, headquarters.GetTotalBuildingTypeCount(BuildingType.Defense));
            Assert.IsFalse(
                producer.GetManufacturingQueue().ContainsKey(ManufacturingType.Building)
            );
        }

        [Test]
        public void Execute_WithSpecialForcesProposal_QueuesRequestedUnitAtPlanet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "training-world",
                empire.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "training-facility",
                BuildingType.TrainingFacility,
                ManufacturingType.Troop
            );
            SpecialForces template = AITestSceneBuilder.CreateSpecialForces(
                "commandos",
                empire.InstanceID
            );
            template.SetBaseRating(OfficerRating.Combat, 70);
            AIProductionDemand demand = new AIProductionDemand(
                "special-forces-demand",
                AIProductionDemandKind.SpecialForces,
                ManufacturingType.Troop,
                BuildingType.None,
                planet,
                1,
                100,
                template.GetTypeID()
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                planet,
                new Technology(template)
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            proposal.Execute(context);

            SpecialForces queued = planet
                .GetManufacturingQueue()[ManufacturingType.Troop]
                .OfType<SpecialForces>()
                .Single();
            Assert.AreEqual("commandos", queued.GetTypeID());
            Assert.AreSame(planet, queued.GetParent());
            Assert.AreEqual(70, queued.GetBaseRating(OfficerRating.Combat));
        }

        [Test]
        public void Execute_WithDistributedStarfighterBatch_QueuesExactQuantity()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "shipyard-world",
                empire.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            CapitalShip carrier = AITestSceneBuilder.CreateCapitalShip(
                "carrier",
                empire.InstanceID,
                starfighterCapacity: 3
            );
            game.AttachNode(fleet, planet);
            game.AttachNode(carrier, fleet);
            Starfighter template = new Starfighter
            {
                InstanceID = "starfighter-template",
                TypeID = "starfighter",
                OwnerInstanceID = empire.InstanceID,
                ManufacturingFactionInstanceIDs = new List<string> { empire.InstanceID },
                ConstructionCost = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            AIProductionDemand demand = new AIProductionDemand(
                "starfighter-demand",
                AIProductionDemandKind.FleetStarfighter,
                ManufacturingType.Ship,
                BuildingType.None,
                fleet,
                3,
                100
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                planet,
                new Technology(template),
                distributesDemand: true
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            proposal.Execute(context);

            List<IManufacturable> queue = planet.GetManufacturingQueue()[ManufacturingType.Ship];
            Assert.AreEqual(3, queue.Count);
            Assert.AreEqual(3, queue.OfType<Starfighter>().Count());
            Assert.AreEqual(3, fleet.GetCurrentStarfighterCount());
        }

        [Test]
        public void Execute_WithPlanetaryStarfighterBatch_QueuesExactQuantityAtPlanet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "shipyard-world",
                empire.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Starfighter template = AITestSceneBuilder.CreateStarfighter(
                "planetary-fighter",
                empire.InstanceID
            );
            AIProductionDemand demand = new AIProductionDemand(
                "planetary-starfighter-demand",
                AIProductionDemandKind.PlanetaryStarfighterReserve,
                ManufacturingType.Ship,
                BuildingType.None,
                planet,
                3,
                100
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                planet,
                new Technology(template),
                distributesDemand: true
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            proposal.Execute(context);

            List<IManufacturable> queue = planet.GetManufacturingQueue()[ManufacturingType.Ship];
            Assert.AreEqual(3, queue.Count);
            Assert.AreEqual(3, queue.OfType<Starfighter>().Count());
            Assert.AreEqual(3, planet.GetStarfighterCount());
            Assert.IsTrue(planet.GetAllStarfighters().All(item => item.GetParent() == planet));
        }

        [Test]
        public void CanExecute_WithDistributedBatchForMovingFleet_ReturnsFalse()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "shipyard-world",
                empire.InstanceID
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.Movement = new MovementState { TransitTicks = 10 };
            game.AttachNode(fleet, planet);
            CapitalShip template = AITestSceneBuilder.CreateCapitalShip(
                "capital-template",
                empire.InstanceID
            );
            AIProductionDemand demand = new AIProductionDemand(
                "capital-demand",
                AIProductionDemandKind.FleetCapitalShip,
                ManufacturingType.Ship,
                BuildingType.None,
                fleet,
                1,
                100
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                planet,
                new Technology(template),
                distributesDemand: true
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            bool canExecute = proposal.CanExecute(context);

            Assert.IsFalse(canExecute);
        }

        [Test]
        public void Execute_WithFleetSeedDemandBeyondMinimum_CreatesBattleFleetAndQueuesCapitalShip()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "shipyard-world",
                empire.InstanceID
            );
            empire.HQInstanceID = planet.InstanceID;
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            CapitalShip template = AITestSceneBuilder.CreateCapitalShip(
                "corvette-template",
                empire.InstanceID
            );
            template.TypeID = "corvette";
            template.ManufacturingFactionInstanceIDs.Add(empire.InstanceID);
            for (
                int index = 0;
                index < game.Config.AI.FleetDeployment.MinimumBattleFleetCount;
                index++
            )
            {
                Fleet existingFleet = empire.CreateFleet(roleType: FleetRoleType.Battle);
                game.AttachNode(existingFleet, planet);
                game.AttachNode(
                    AITestSceneBuilder.CreateCapitalShip(
                        $"existing-capital-{index}",
                        empire.InstanceID
                    ),
                    existingFleet
                );
            }

            AIProductionDemand demand = new AIProductionDemand(
                "fleet-seed-demand",
                AIProductionDemandKind.FleetSeedCapitalShip,
                ManufacturingType.Ship,
                BuildingType.None,
                planet,
                1,
                100
            );
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                planet,
                new Technology(template)
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            proposal.Execute(context);

            IReadOnlyList<Fleet> fleets = game.GetSceneNodesByOwnerInstanceID<Fleet>(
                empire.InstanceID
            );
            Assert.AreEqual(
                game.Config.AI.FleetDeployment.MinimumBattleFleetCount + 1,
                fleets.Count
            );
            Fleet fleet = fleets.Single(candidate =>
                candidate
                    .GetChildren<CapitalShip>()
                    .Any(ship => ship.ManufacturingStatus == ManufacturingStatus.Building)
            );
            Assert.AreEqual(FleetRoleType.Battle, fleet.RoleType);
            Assert.AreSame(planet, fleet.GetParent());
            Assert.AreEqual(1, fleet.GetChildren<CapitalShip>().Count);
            Assert.AreEqual(
                ManufacturingStatus.Building,
                fleet.GetChildren<CapitalShip>()[0].ManufacturingStatus
            );
            Assert.AreEqual(1, planet.GetManufacturingQueue()[ManufacturingType.Ship].Count);
        }

        private static AIProductionDemand CreateBuildingDemand(Planet destination)
        {
            return new AIProductionDemand(
                "mine-demand",
                AIProductionDemandKind.Mine,
                ManufacturingType.Building,
                BuildingType.Mine,
                destination,
                1,
                100
            );
        }

        private static (
            GameRoot game,
            Faction faction,
            Planet planet,
            Building replacement,
            Building remaining,
            AIManufactureProposal proposal,
            AITurnContext context
        ) CreateFacilityUpgradeProposalScene(
            int shipyardCount,
            int existingMaintenance,
            int upgradeMaintenance
        )
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "shipyard-world",
                empire.InstanceID,
                energyCapacity: shipyardCount + 1
            );
            AITestSceneBuilder.AddProductionFacility(
                game,
                planet,
                "construction-yard",
                BuildingType.ConstructionFacility,
                ManufacturingType.Building
            );
            Building replacement = null;
            Building remaining = null;
            for (int index = 0; index < shipyardCount; index++)
            {
                Building facility = AITestSceneBuilder.AddProductionFacility(
                    game,
                    planet,
                    $"legacy-shipyard-{index}",
                    BuildingType.Shipyard,
                    ManufacturingType.Ship,
                    processRate: 4
                );
                facility.ResearchOrder = 0;
                facility.MaintenanceCost = existingMaintenance;
                facility.Upgrades.Add("advanced-shipyard");
                replacement ??= facility;
                if (index > 0)
                    remaining = facility;
            }

            Building advancedShipyard = AITestSceneBuilder.CreateBuildingTemplate(
                "advanced-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            advancedShipyard.TypeID = "advanced-shipyard";
            advancedShipyard.ProcessRate = 2;
            advancedShipyard.ResearchOrder = 5;
            advancedShipyard.MaintenanceCost = upgradeMaintenance;
            AIProductionDemand demand = new AIProductionDemand(
                "facility-upgrade",
                AIProductionDemandKind.BuildingUpgrade,
                ManufacturingType.Building,
                BuildingType.Shipyard,
                planet,
                1,
                100
            );
            demand.BuildingToReplace = replacement;
            AIManufactureProposal proposal = new AIManufactureProposal(
                demand,
                planet,
                new Technology(advancedShipyard)
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            return (game, empire, planet, replacement, remaining, proposal, context);
        }
    }
}

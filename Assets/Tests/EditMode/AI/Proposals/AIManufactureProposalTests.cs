using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Proposals
{
    [TestFixture]
    public class AIManufactureProposalTests
    {
        [Test]
        public void GetClaimKeys_WithBuildingDemand_ClaimsDemandProducerAndDestination()
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
            CollectionAssert.Contains(claimKeys, "production:building:producer");
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
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
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
        public void Execute_WithHeadquartersDefenseAndFullCapacity_ReplacesExcessFacility()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.AI.Infrastructure.PlanetsPerShipyard = 2;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
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
                "replaceable-shipyard",
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
                "required-shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
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
                AIProductionDemandKind.HeadquartersDefense,
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

            Assert.IsNull(replaceableShipyard.GetParent());
            Assert.AreEqual(1, headquarters.GetTotalBuildingTypeCount(BuildingType.Defense));
            Assert.AreEqual(1, producer.GetManufacturingQueue()[ManufacturingType.Building].Count);
        }

        [Test]
        public void Execute_WithSpecialForcesProposal_QueuesRequestedUnitAtPlanet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
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
        public void Execute_WithFleetSeedDemandBeyondMinimum_CreatesBattleFleetAndQueuesCapitalShip()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
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
            template.AllowedOwnerInstanceIDs.Add(empire.InstanceID);
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
                candidate.CapitalShips.Any(ship =>
                    ship.ManufacturingStatus == ManufacturingStatus.Building
                )
            );
            Assert.AreEqual(FleetRoleType.Battle, fleet.RoleType);
            Assert.AreSame(planet, fleet.GetParent());
            Assert.AreEqual(1, fleet.CapitalShips.Count);
            Assert.AreEqual(
                ManufacturingStatus.Building,
                fleet.CapitalShips[0].ManufacturingStatus
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
    }
}

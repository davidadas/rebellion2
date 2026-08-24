using System.Linq;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Movement;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Director
{
    [TestFixture]
    public class AIAssessmentTests
    {
        [Test]
        public void Constructor_WithMixedPlanetOwnership_BuildsOwnershipLists()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            Planet neutral = AITestSceneBuilder.AddPlanet(game, system, "neutral", null);
            AITestSceneBuilder.RevealPlanet(game, empire, enemy);
            AITestSceneBuilder.RevealPlanet(game, empire, neutral);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            CollectionAssert.Contains(assessment.OwnedPlanets, owned);
            CollectionAssert.Contains(
                assessment.EnemyPlanets.Select(planet => planet.InstanceID),
                enemy.InstanceID
            );
            CollectionAssert.Contains(
                assessment.NeutralPlanets.Select(planet => planet.InstanceID),
                neutral.InstanceID
            );
        }

        [Test]
        public void Constructor_WithEconomicState_CachesTurnValues()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Starfighter starfighter = AITestSceneBuilder.CreateStarfighter(
                "fighter",
                empire.InstanceID,
                maintenanceCost: 7
            );
            game.AttachNode(starfighter, planet);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;
            int cachedHeadroom = assessment.ProjectedMaintenanceHeadroom;
            int cachedSupply = assessment.RefinedMaterialSupply;
            int cachedStockpile = assessment.RefinedMaterialStockpile;
            starfighter.MaintenanceCost = 11;
            empire.RefinedMaterialStockpile++;

            Assert.AreEqual(empire.MaintenanceCapacity, assessment.MaintenanceCapacity);
            Assert.AreEqual(cachedHeadroom, assessment.ProjectedMaintenanceHeadroom);
            Assert.AreNotEqual(
                empire.ProjectedMaintenanceHeadroom,
                assessment.ProjectedMaintenanceHeadroom
            );
            Assert.AreEqual(cachedSupply, assessment.RefinedMaterialSupply);
            Assert.AreEqual(cachedStockpile, assessment.RefinedMaterialStockpile);
        }

        [Test]
        public void GetAvailableProductionLaneCount_WithPartiallyUsedStack_ReturnsFreeLanes()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "production-world",
                empire.InstanceID
            );
            for (int index = 0; index < 3; index++)
            {
                AITestSceneBuilder.AddProductionFacility(
                    game,
                    planet,
                    $"construction-facility-{index}",
                    BuildingType.ConstructionFacility,
                    ManufacturingType.Building
                );
            }

            Building queued = AITestSceneBuilder.CreateBuildingTemplate(
                "queued-building",
                BuildingType.Defense
            );
            queued.OwnerInstanceID = empire.InstanceID;
            queued.ManufacturingStatus = ManufacturingStatus.Building;
            game.AttachNode(queued, planet);
            planet.AddToManufacturingQueue(queued);
            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(
                2,
                assessment.GetAvailableProductionLaneCount(ManufacturingType.Building)
            );
        }

        [Test]
        public void GetDiplomacyTargetStrategicValue_WithHealthyMaintenance_IgnoresResources()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet target = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "target",
                null,
                rawResourceNodes: 5
            );
            game.Config.AI.Selection.MinimumMaintenanceHeadroomAfterProduction = 0;
            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            int value = assessment.GetDiplomacyTargetStrategicValue(target);

            Assert.AreEqual(0, value);
        }

        [Test]
        public void GetDiplomacyTargetStrategicValue_WithMaintenancePressure_ValuesResources()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet target = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "target",
                null,
                rawResourceNodes: 5
            );
            game.Config.AI.Selection.MinimumMaintenanceHeadroomAfterProduction = 1;
            game.Config.AI.MissionPlanning.DiplomacyResourceNodeWeight = 4;
            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            int value = assessment.GetDiplomacyTargetStrategicValue(target);

            Assert.AreEqual(20, value);
        }

        [Test]
        public void Constructor_WithUnobservedForeignPlanet_DoesNotExposePlanet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet hidden = AITestSceneBuilder.AddPlanet(game, system, "hidden", rebels.InstanceID);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            CollectionAssert.DoesNotContain(
                assessment.KnownColonizedPlanets.Select(planet => planet.InstanceID),
                hidden.InstanceID
            );
            Assert.AreEqual(hidden.InstanceID, assessment.UnexploredPlanets.Single().InstanceID);
            Assert.IsNull(assessment.GetKnownPlanet(hidden.InstanceID));
        }

        [Test]
        public void Constructor_WithObservedUncolonizedPlanet_ExposesColonizationTarget()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", null);
            target.IsColonized = false;
            AITestSceneBuilder.RevealPlanet(game, empire, target);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(
                target.InstanceID,
                assessment.KnownUncolonizedPlanets.Single().InstanceID
            );
            Assert.AreEqual(
                target.InstanceID,
                assessment.GetKnownPlanet(target.InstanceID).InstanceID
            );
        }

        [Test]
        public void Constructor_WithStaleUncolonizedSnapshot_DoesNotRevealCurrentColonization()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", null);
            target.IsColonized = false;
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            target.IsColonized = true;
            game.ChangeOwnership(target, rebels.InstanceID);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;
            Planet knownTarget = assessment.GetKnownPlanet(target.InstanceID);

            Assert.IsFalse(knownTarget.IsColonized);
            Assert.IsNull(knownTarget.GetOwnerInstanceID());
            Assert.AreEqual(
                target.InstanceID,
                assessment.KnownUncolonizedPlanets.Single().InstanceID
            );
        }

        [Test]
        public void Constructor_WithEnemyOfficer_BuildsTargetableEnemyOfficerTargets()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            Officer target = EntityFactory.CreateOfficer("target", rebels.InstanceID);
            game.AttachNode(target, enemy);
            AITestSceneBuilder.RevealPlanet(game, empire, enemy);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(1, assessment.TargetableEnemyOfficerMissionTargets.Count);
            Assert.AreEqual(
                enemy.InstanceID,
                assessment.TargetableEnemyOfficerMissionTargets[0].Planet.InstanceID
            );
            Assert.AreEqual(
                target.InstanceID,
                assessment.TargetableEnemyOfficerMissionTargets[0].TargetOfficer.InstanceID
            );
            Assert.AreNotSame(
                target,
                assessment.TargetableEnemyOfficerMissionTargets[0].TargetOfficer
            );
        }

        [Test]
        public void Constructor_WithEnemyOfficerAboardFleet_BuildsTargetableEnemyOfficerTargets()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", rebels.InstanceID);
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip("ship", rebels.InstanceID);
            Officer target = EntityFactory.CreateOfficer("target", rebels.InstanceID);
            game.AttachNode(fleet, enemy);
            game.AttachNode(ship, fleet);
            game.AttachNode(target, ship);
            AITestSceneBuilder.RevealPlanet(game, empire, enemy);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            (Planet Planet, Officer TargetOfficer) candidate =
                assessment.TargetableEnemyOfficerMissionTargets.Single();
            Assert.AreEqual(enemy.InstanceID, candidate.Planet.InstanceID);
            Assert.AreEqual(target.InstanceID, candidate.TargetOfficer.InstanceID);
            Assert.AreNotSame(target, candidate.TargetOfficer);
        }

        [Test]
        public void Constructor_WithStaleSnapshot_UsesLastObservedEnemyState()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            Building observed = AITestSceneBuilder.CreateBuildingTemplate(
                "observed",
                BuildingType.Mine
            );
            observed.OwnerInstanceID = rebels.InstanceID;
            observed.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(observed, enemy);
            AITestSceneBuilder.RevealPlanet(game, empire, enemy);

            Building hidden = AITestSceneBuilder.CreateBuildingTemplate(
                "hidden",
                BuildingType.Mine
            );
            hidden.OwnerInstanceID = rebels.InstanceID;
            hidden.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(hidden, enemy);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;
            Planet knownEnemy = assessment.GetKnownPlanet(enemy.InstanceID);

            Assert.AreEqual(1, assessment.GetPlanetBuildingCount(knownEnemy));
        }

        [Test]
        public void Constructor_WithStaleSnapshot_UsesDetachedRecordedEntityCopies()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            Building observed = AITestSceneBuilder.CreateBuildingTemplate(
                "observed",
                BuildingType.Mine
            );
            observed.OwnerInstanceID = rebels.InstanceID;
            observed.ManufacturingStatus = ManufacturingStatus.Complete;
            game.AttachNode(observed, enemy);
            AITestSceneBuilder.RevealPlanet(game, empire, enemy);
            Building recorded = empire
                .Fog.Snapshots[system.InstanceID]
                .Planets[enemy.InstanceID]
                .Buildings.Single();

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;
            Planet knownEnemy = assessment.GetKnownPlanet(enemy.InstanceID);

            Building knownBuilding = knownEnemy.GetChildren<Building>().Single();
            Assert.AreNotSame(recorded, knownBuilding);
            Assert.AreEqual(recorded.InstanceID, knownBuilding.InstanceID);
            Assert.AreSame(knownEnemy, knownBuilding.GetParentOfType<Planet>());
        }

        [Test]
        public void CanFleetDepartHeadquarters_WithOnlyLocalFleet_ReturnsFalse()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "headquarters",
                empire.InstanceID
            );
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            game.AttachNode(fleet, headquarters);
            game.AttachNode(AITestSceneBuilder.CreateCapitalShip("ship", empire.InstanceID), fleet);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.IsFalse(assessment.CanFleetDepartHeadquarters(fleet));
        }

        [Test]
        public void CanFleetDepartHeadquarters_WithThreatBeyondRemainingDefense_ReturnsFalse()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumDefenseStrength = 1000;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 125;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "headquarters",
                empire.InstanceID
            );
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            Fleet departingFleet = EntityFactory.CreateFleet("departing", empire.InstanceID);
            game.AttachNode(departingFleet, headquarters);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "departing-ship",
                    empire.InstanceID,
                    combatStrength: 3000
                ),
                departingFleet
            );
            Fleet remainingFleet = EntityFactory.CreateFleet("remaining", empire.InstanceID);
            game.AttachNode(remainingFleet, headquarters);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "remaining-ship",
                    empire.InstanceID,
                    combatStrength: 1000
                ),
                remainingFleet
            );
            Fleet hostileFleet = EntityFactory.CreateFleet("hostile", rebels.InstanceID);
            game.AttachNode(hostileFleet, headquarters);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "hostile-ship",
                    rebels.InstanceID,
                    combatStrength: 2000
                ),
                hostileFleet
            );

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.IsFalse(assessment.CanFleetDepartHeadquarters(departingFleet));
        }

        [Test]
        public void GetRequiredHeadquartersDefenseStrength_WithUncommittedRemoteHostileFleet_UsesMinimum()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumDefenseStrength = 1000;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 125;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "headquarters",
                empire.InstanceID
            );
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            Planet enemyPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "enemy",
                rebels.InstanceID
            );
            Fleet hostileFleet = EntityFactory.CreateFleet("hostile", rebels.InstanceID);
            game.AttachNode(hostileFleet, enemyPlanet);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "hostile-ship",
                    rebels.InstanceID,
                    combatStrength: 2000
                ),
                hostileFleet
            );
            AITestSceneBuilder.RevealPlanet(game, empire, enemyPlanet);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(1000, assessment.GetRequiredHeadquartersDefenseStrength(headquarters));
        }

        [Test]
        public void GetRequiredHeadquartersDefenseStrength_WithUnknownRemoteAttackOrder_UsesMinimumStrength()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumDefenseStrength = 1000;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 125;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "headquarters",
                empire.InstanceID
            );
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            Planet enemyPlanet = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "enemy",
                rebels.InstanceID
            );
            Fleet hostileFleet = EntityFactory.CreateFleet("hostile", rebels.InstanceID);
            hostileFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                TargetPlanetId = headquarters.InstanceID,
            };
            game.AttachNode(hostileFleet, enemyPlanet);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "hostile-ship",
                    rebels.InstanceID,
                    combatStrength: 2000
                ),
                hostileFleet
            );
            AITestSceneBuilder.RevealPlanet(game, empire, enemyPlanet);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(1000, assessment.GetRequiredHeadquartersDefenseStrength(headquarters));
        }

        [Test]
        public void CanFleetDepartHeadquarters_AtUndefendedCapturedEnemyHeadquarters_ReturnsFalse()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "captured-headquarters",
                empire.InstanceID
            );
            headquarters.IsHeadquarters = true;
            rebels.HQInstanceID = headquarters.InstanceID;
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            game.AttachNode(fleet, headquarters);
            game.AttachNode(AITestSceneBuilder.CreateCapitalShip("ship", empire.InstanceID), fleet);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.IsFalse(assessment.CanFleetDepartHeadquarters(fleet));
        }

        [Test]
        public void GetProjectedFleetCombatValue_WithCommittedUnits_IncludesReadyAndPendingCombat()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            CapitalShip readyShip = AITestSceneBuilder.CreateCapitalShip(
                "ready-ship",
                empire.InstanceID,
                combatStrength: 100,
                starfighterCapacity: 1
            );
            Starfighter readyFighter = new Starfighter
            {
                InstanceID = "ready-fighter",
                OwnerInstanceID = empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                LaserCannon = 100,
                MaxSquadronSize = 10,
                CurrentSquadronSize = 10,
            };
            CapitalShip inboundShip = AITestSceneBuilder.CreateCapitalShip(
                "inbound-ship",
                empire.InstanceID,
                combatStrength: 300,
                starfighterCapacity: 1
            );
            inboundShip.Movement = new MovementState { TransitTicks = 10 };
            Starfighter inboundFighter = new Starfighter
            {
                InstanceID = "inbound-fighter",
                OwnerInstanceID = empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                LaserCannon = 50,
                MaxSquadronSize = 10,
                CurrentSquadronSize = 10,
            };
            CapitalShip buildingShip = AITestSceneBuilder.CreateCapitalShip(
                "building-ship",
                empire.InstanceID,
                combatStrength: 200
            );
            buildingShip.ManufacturingStatus = ManufacturingStatus.Building;

            game.AttachNode(fleet, planet);
            game.AttachNode(readyShip, fleet);
            game.AttachNode(readyFighter, readyShip);
            game.AttachNode(inboundShip, fleet);
            game.AttachNode(inboundFighter, inboundShip);
            game.AttachNode(buildingShip, fleet);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(200, assessment.GetReadyFleetCombatValue(fleet));
            Assert.AreEqual(750, assessment.GetProjectedFleetCombatValue(fleet));
        }

        [Test]
        public void GetProjectedFleetRegimentAttackStrength_WithCommittedRegiments_IncludesPendingStrength()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            CapitalShip readyShip = AITestSceneBuilder.CreateCapitalShip(
                "ready-ship",
                empire.InstanceID,
                regimentCapacity: 2
            );
            CapitalShip inboundShip = AITestSceneBuilder.CreateCapitalShip(
                "inbound-ship",
                empire.InstanceID
            );
            inboundShip.Movement = new MovementState { TransitTicks = 10 };
            Regiment readyRegiment = AITestSceneBuilder.CreateRegiment(
                "ready-regiment",
                empire.InstanceID,
                attackRating: 10
            );
            Regiment inboundRegiment = AITestSceneBuilder.CreateRegiment(
                "inbound-regiment",
                empire.InstanceID,
                attackRating: 20
            );
            Regiment buildingRegiment = AITestSceneBuilder.CreateRegiment(
                "building-regiment",
                empire.InstanceID,
                attackRating: 30
            );
            buildingRegiment.ManufacturingStatus = ManufacturingStatus.Building;

            game.AttachNode(fleet, planet);
            game.AttachNode(readyShip, fleet);
            game.AttachNode(readyRegiment, readyShip);
            game.AttachNode(inboundShip, fleet);
            game.AttachNode(inboundRegiment, inboundShip);
            game.AttachNode(buildingRegiment, readyShip);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(10, assessment.GetReadyFleetRegimentAttackStrength(fleet));
            Assert.AreEqual(60, assessment.GetProjectedFleetRegimentAttackStrength(fleet));
        }

        [Test]
        public void GetFleetBombardmentStrength_WithDamagedUnitsAndAdmiral_MatchesCombatRules()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            game.Config.Combat.Bombardment.AttackerLeadershipDivisor = 10;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet planet = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip("ship", empire.InstanceID);
            ship.Bombardment = 20;
            ship.CurrentHullStrength = 50;
            Starfighter fighter = new Starfighter
            {
                InstanceID = "fighter",
                OwnerInstanceID = empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                Bombardment = 10,
                MaxSquadronSize = 10,
                CurrentSquadronSize = 5,
            };
            Officer admiral = EntityFactory.CreateOfficer("admiral", empire.InstanceID);
            admiral.CurrentRank = OfficerRank.Admiral;
            admiral.Ratings[OfficerRating.Leadership] = 20;
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            game.AttachNode(fighter, ship);
            game.AttachNode(admiral, ship);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(45, assessment.GetFleetBombardmentStrength(fleet));
        }

        [Test]
        public void IsFleetReadyToAttack_ShieldedTarget_RequiresShieldPenetration()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 0;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 0;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 1;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfDefense = 100;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            target.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            AddShield(game, target, "shield-1", rebels.InstanceID, 5);
            AddShield(game, target, "shield-2", rebels.InstanceID, 5);

            Fleet blockedFleet = CreateAssaultFleet(game, origin, "blocked", empire.InstanceID, 10);
            Fleet readyFleet = CreateAssaultFleet(game, origin, "ready", empire.InstanceID, 10);
            Starfighter bomber = new Starfighter
            {
                InstanceID = "bomber",
                OwnerInstanceID = empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                Bombardment = 1,
                MaxSquadronSize = 1,
                CurrentSquadronSize = 1,
            };
            game.AttachNode(bomber, readyFleet.GetChildren<CapitalShip>().Single());

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.IsFalse(assessment.IsFleetReadyToAttack(blockedFleet, target));
            Assert.IsTrue(assessment.IsFleetReadyToAttack(readyFleet, target));
        }

        [Test]
        public void IsFleetReadyToAttack_BombardmentCanRemoveDefenders_RequiresOccupationForce()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 0;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 0;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 1;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfDefense = 100;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            target.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            for (int index = 0; index < 3; index++)
            {
                game.AttachNode(
                    AITestSceneBuilder.CreateRegiment(
                        $"defender-{index}",
                        rebels.InstanceID,
                        defenseRating: 100
                    ),
                    target
                );
            }
            Fleet groundFleet = CreateAssaultFleet(game, origin, "ground", empire.InstanceID, 0);
            Fleet bombardmentFleet = CreateAssaultFleet(
                game,
                origin,
                "bombardment",
                empire.InstanceID,
                1
            );

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.IsFalse(assessment.IsFleetReadyToAttack(groundFleet, target));
            Assert.IsTrue(assessment.IsFleetReadyToAttack(bombardmentFleet, target));
        }

        [Test]
        public void IsFleetReadyToAttack_TransportOnlyFleet_ReturnsFalse()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 1;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            target.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            Fleet fleet = EntityFactory.CreateFleet("transport", empire.InstanceID);
            CapitalShip transport = AITestSceneBuilder.CreateCapitalShip(
                "transport-ship",
                empire.InstanceID,
                combatStrength: 0
            );
            game.AttachNode(fleet, origin);
            game.AttachNode(transport, fleet);
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("transport-regiment", empire.InstanceID),
                transport
            );

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.IsFalse(assessment.IsFleetReadyToAttack(fleet, target));
        }

        [Test]
        public void IsFleetReadyToAttack_SingleTroopFacingCertainDefenseFire_ReturnsFalse()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 0;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 0;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 1;
            game.Config.Combat.PlanetaryAssault.DefenseFireDivisor = 5;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            target.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            Building battery = AITestSceneBuilder.CreateBuildingTemplate(
                "battery",
                BuildingType.Weapon
            );
            battery.OwnerInstanceID = rebels.InstanceID;
            battery.DefenseFacilityClass = DefenseFacilityClass.KDY;
            battery.WeaponPower = 500;
            game.AttachNode(battery, target);
            Fleet fleet = CreateAssaultFleet(game, origin, "attacker", empire.InstanceID, 0);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.IsFalse(assessment.IsFleetReadyToAttack(fleet, target));
        }

        [Test]
        public void IsFleetReadyToAttack_ReserveTroopSurvivesCertainDefenseFire_ReturnsTrue()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 0;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 0;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 1;
            game.Config.Combat.PlanetaryAssault.DefenseFireDivisor = 5;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            target.SetPopularSupport(empire.InstanceID, game.Config.AI.Garrison.SupportThreshold);
            Building battery = AITestSceneBuilder.CreateBuildingTemplate(
                "battery",
                BuildingType.Weapon
            );
            battery.OwnerInstanceID = rebels.InstanceID;
            battery.DefenseFacilityClass = DefenseFacilityClass.KDY;
            battery.WeaponPower = 500;
            game.AttachNode(battery, target);
            Fleet fleet = CreateAssaultFleet(game, origin, "attacker", empire.InstanceID, 0);
            fleet.GetChildren<CapitalShip>().Single().RegimentCapacity = 2;
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("attacker-reserve", empire.InstanceID),
                fleet.GetChildren<CapitalShip>().Single()
            );

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.IsTrue(assessment.IsFleetReadyToAttack(fleet, target));
        }

        [Test]
        public void GetRequiredAttackRegimentCount_BombardmentCanRemoveDefenders_RequiresStableOccupation()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 1;
            game.Config.Combat.PlanetaryAssault.CaptureGarrisonCount = 6;
            game.Config.AI.Garrison.SupportThreshold = 60;
            game.Config.AI.Garrison.GarrisonDivisor = 10;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            target.SetPopularSupport(empire.InstanceID, 20);
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("defender", rebels.InstanceID),
                target
            );
            Fleet fleet = CreateAssaultFleet(game, origin, "attacker", empire.InstanceID, 4);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(4, assessment.GetRequiredAttackRegimentCount(fleet, target));
        }

        [Test]
        public void GetRequiredAttackRegimentCount_StabilityExceedsLandingCapacity_CapsOccupation()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 1;
            game.Config.Combat.PlanetaryAssault.CaptureGarrisonCount = 3;
            game.Config.AI.Garrison.SupportThreshold = 100;
            game.Config.AI.Garrison.GarrisonDivisor = 1;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet origin = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            target.SetPopularSupport(empire.InstanceID, 0);
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("defender", rebels.InstanceID),
                target
            );
            Fleet fleet = CreateAssaultFleet(game, origin, "attacker", empire.InstanceID, 3);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(3, assessment.GetRequiredAttackRegimentCount(fleet, target));
        }

        [Test]
        public void GetDefendingRegimentDefenseStrength_IgnoresBombardmentDefense()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet lowBombardmentDefense = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "low",
                rebels.InstanceID
            );
            Planet highBombardmentDefense = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "high",
                rebels.InstanceID
            );
            Regiment low = AITestSceneBuilder.CreateRegiment("low-regiment", rebels.InstanceID);
            Regiment high = AITestSceneBuilder.CreateRegiment("high-regiment", rebels.InstanceID);
            low.BombardmentDefense = 0;
            high.BombardmentDefense = 1000;
            game.AttachNode(low, lowBombardmentDefense);
            game.AttachNode(high, highBombardmentDefense);

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(
                assessment.GetDefendingRegimentDefenseStrength(lowBombardmentDefense),
                assessment.GetDefendingRegimentDefenseStrength(highBombardmentDefense)
            );
        }

        [Test]
        public void GetRequiredAttackRegimentCount_WithDefendersAndLowSupport_RequiresOccupier()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 2;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            enemy.SetPopularSupport(empire.InstanceID, 0);

            for (int index = 0; index < 3; index++)
            {
                Regiment defender = AITestSceneBuilder.CreateRegiment(
                    $"defender-{index}",
                    rebels.InstanceID
                );
                game.AttachNode(defender, enemy);
            }

            Fleet attackerFleet = EntityFactory.CreateFleet("attacker", empire.InstanceID);
            CapitalShip attackerShip = AITestSceneBuilder.CreateCapitalShip(
                "attacker-ship",
                empire.InstanceID
            );
            game.AttachNode(attackerFleet, enemy);
            game.AttachNode(attackerShip, attackerFleet);
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment("attacker-regiment", empire.InstanceID),
                attackerShip
            );

            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Assert.AreEqual(4, assessment.GetRequiredAttackRegimentCount(enemy));
        }

        [Test]
        public void GetRequiredAttackCampaignPackage_AggregatesEveryEnemyPlanetInSystem()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 100;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet = 100;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 0;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfDefense = 100;
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
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
            firstEnemy.SetPopularSupport(
                empire.InstanceID,
                game.Config.AI.Garrison.SupportThreshold
            );
            secondEnemy.SetPopularSupport(
                empire.InstanceID,
                game.Config.AI.Garrison.SupportThreshold
            );
            Fleet firstDefenseFleet = EntityFactory.CreateFleet("defense-1", rebels.InstanceID);
            Fleet secondDefenseFleet = EntityFactory.CreateFleet("defense-2", rebels.InstanceID);
            game.AttachNode(firstDefenseFleet, firstEnemy);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "defense-ship-1",
                    rebels.InstanceID,
                    combatStrength: 200
                ),
                firstDefenseFleet
            );
            game.AttachNode(secondDefenseFleet, secondEnemy);
            game.AttachNode(
                AITestSceneBuilder.CreateCapitalShip(
                    "defense-ship-2",
                    rebels.InstanceID,
                    combatStrength: 300
                ),
                secondDefenseFleet
            );
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment(
                    "defender-1",
                    rebels.InstanceID,
                    defenseRating: 10
                ),
                firstEnemy
            );
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment(
                    "defender-2",
                    rebels.InstanceID,
                    defenseRating: 10
                ),
                firstEnemy
            );
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment(
                    "defender-3",
                    rebels.InstanceID,
                    defenseRating: 20
                ),
                secondEnemy
            );
            AddShield(game, firstEnemy, "shield-1", rebels.InstanceID, 5);
            AddShield(game, firstEnemy, "shield-2", rebels.InstanceID, 5);
            AITestSceneBuilder.RevealPlanet(game, empire, firstEnemy);
            AITestSceneBuilder.RevealPlanet(game, empire, secondEnemy);
            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;
            Planet target = assessment.GetKnownPlanet(firstEnemy.InstanceID);

            Assert.AreEqual(500, assessment.GetRequiredAttackCampaignCombatStrength(target));
            Assert.AreEqual(5, assessment.GetRequiredAttackCampaignRegimentCount(target));
            Assert.AreEqual(40, assessment.GetRequiredAttackCampaignRegimentStrength(target));
            Assert.AreEqual(11, assessment.GetRequiredAttackCampaignBombardmentStrength(target));
        }

        [Test]
        public void GetSabotageTargetPriorityBonus_MixedTargets_UsesTacticalPriorityOrder()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            Building shield = AddShield(game, target, "shield", rebels.InstanceID, 5);
            Building battery = AITestSceneBuilder.CreateBuildingTemplate(
                "battery",
                BuildingType.Weapon
            );
            battery.OwnerInstanceID = rebels.InstanceID;
            game.AttachNode(battery, target);
            Regiment regiment = AITestSceneBuilder.CreateRegiment("regiment", rebels.InstanceID);
            game.AttachNode(regiment, target);
            Starfighter starfighter = AITestSceneBuilder.CreateStarfighter(
                "starfighter",
                rebels.InstanceID
            );
            game.AttachNode(starfighter, target);
            Building shipyard = AITestSceneBuilder.AddProductionFacility(
                game,
                target,
                "shipyard",
                BuildingType.Shipyard,
                ManufacturingType.Ship
            );
            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            int shieldPriority = assessment.GetSabotageTargetPriorityBonus(target, shield);
            int batteryPriority = assessment.GetSabotageTargetPriorityBonus(target, battery);
            int regimentPriority = assessment.GetSabotageTargetPriorityBonus(target, regiment);
            int starfighterPriority = assessment.GetSabotageTargetPriorityBonus(
                target,
                starfighter
            );
            int infrastructurePriority = assessment.GetSabotageTargetPriorityBonus(
                target,
                shipyard
            );

            Assert.Greater(shieldPriority, batteryPriority);
            Assert.Greater(batteryPriority, regimentPriority);
            Assert.Greater(regimentPriority, starfighterPriority);
            Assert.Greater(starfighterPriority, infrastructurePriority);
        }

        private static Fleet CreateAssaultFleet(
            GameRoot game,
            Planet planet,
            string instanceId,
            string ownerInstanceId,
            int bombardment
        )
        {
            Fleet fleet = EntityFactory.CreateFleet(instanceId, ownerInstanceId);
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip(
                $"{instanceId}-ship",
                ownerInstanceId
            );
            ship.Bombardment = bombardment;
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            game.AttachNode(
                AITestSceneBuilder.CreateRegiment($"{instanceId}-regiment", ownerInstanceId),
                ship
            );
            return fleet;
        }

        private static Building AddShield(
            GameRoot game,
            Planet planet,
            string instanceId,
            string ownerInstanceId,
            int shieldStrength
        )
        {
            Building shield = AITestSceneBuilder.CreateBuildingTemplate(
                instanceId,
                BuildingType.Defense
            );
            shield.OwnerInstanceID = ownerInstanceId;
            shield.DefenseFacilityClass = DefenseFacilityClass.Shield;
            shield.ShieldStrength = shieldStrength;
            game.AttachNode(shield, planet);
            return shield;
        }

        [Test]
        public void GetAttackTargetPlanet_EnemyAttackOrder_ReturnsTarget()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                TargetPlanetId = enemy.InstanceID,
            };
            game.AttachNode(fleet, owned);
            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            Planet target = assessment.GetAttackTargetPlanet(fleet);

            Assert.AreSame(enemy, target);
        }

        [Test]
        public void GetAttackTargetPlanet_NonEnemyAttackOrder_ReturnsNull()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            Planet neutral = AITestSceneBuilder.AddPlanet(game, system, "neutral", null);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            game.AttachNode(fleet, owned);
            AIAssessment assessment = AITestSceneBuilder.CreateContext(game, empire).Assessment;

            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Defend,
                TargetPlanetId = enemy.InstanceID,
            };
            Assert.IsNull(assessment.GetAttackTargetPlanet(fleet));

            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                TargetPlanetId = owned.InstanceID,
            };
            Assert.IsNull(assessment.GetAttackTargetPlanet(fleet));

            fleet.Order.TargetPlanetId = neutral.InstanceID;
            Assert.IsNull(assessment.GetAttackTargetPlanet(fleet));

            fleet.Order.TargetPlanetId = "missing";
            Assert.IsNull(assessment.GetAttackTargetPlanet(fleet));
        }
    }
}

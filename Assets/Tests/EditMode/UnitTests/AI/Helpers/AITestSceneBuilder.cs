using System.Collections.Generic;
using Rebellion.AI;
using Rebellion.AI.Demands;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scorers;
using Rebellion.AI.Selectors;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Simulation;
using Rebellion.Util.Random;

namespace Rebellion.Tests.AI.Helpers
{
    public static class AITestSceneBuilder
    {
        /// <summary>
        /// Creates a bombardment fleet with a donor hull available for reinforcement.
        /// </summary>
        /// <param name="assaultBlocked">Whether an additional shield blocks the assault.</param>
        /// <param name="donorBombardment">The donor hull's bombardment rating.</param>
        /// <param name="receiver">Receives the fleet awaiting reinforcement.</param>
        /// <param name="donor">Receives the available donor hull.</param>
        /// <returns>The faction turn containing the fleets and their known targets.</returns>
        public static AITurnContext CreateBombardmentDonorContext(
            bool assaultBlocked,
            int donorBombardment,
            out Fleet receiver,
            out CapitalShip donor
        )
        {
            AITurnContext context = CreateBombardmentReinforcementContext(
                true,
                true,
                out receiver,
                out Fleet other,
                out _
            );
            context.Game.DeleteNode(other);
            Planet otherTarget = context.Game.GetSceneNodeByInstanceID<Planet>("other-target");
            otherTarget.OwnerInstanceID = context.Faction.InstanceID;
            RevealPlanet(context.Game, context.Faction, otherTarget);
            context = AddReinforcementDonor(context, false, out donor);
            donor.GetParentOfType<Fleet>().RoleType = FleetRoleType.Battle;
            donor.RegimentCapacity = 0;
            donor.StarfighterCapacity = 0;
            donor.Bombardment = donorBombardment;
            Planet target = context.Game.GetSceneNodeByInstanceID<Planet>(
                receiver.Order.TargetPlanetId
            );
            if (assaultBlocked)
            {
                Building shield = CreateBuildingTemplate("extra-shield", BuildingType.Defense);
                shield.OwnerInstanceID = target.GetOwnerInstanceID();
                shield.ShieldStrength = 2;
                context.Game.AttachNode(shield, target);
            }
            RevealPlanet(context.Game, context.Faction, target);
            return CreateContext(context.Game, context.Faction);
        }

        /// <summary>
        /// Adds an independently stationed hull to an existing reinforcement scenario.
        /// </summary>
        /// <param name="context">The faction turn whose demands are preserved.</param>
        /// <param name="carriesRegiment">Whether the donor carries a completed regiment.</param>
        /// <param name="ship">Receives the donor hull.</param>
        /// <returns>A refreshed turn context retaining the existing attack demands.</returns>
        public static AITurnContext AddReinforcementDonor(
            AITurnContext context,
            bool carriesRegiment,
            out CapitalShip ship
        )
        {
            var demands = new List<AIAttackDemand>(context.AttackDemands);
            Planet source = context.Assessment.OwnedPlanets[0];
            Fleet donor = EntityFactory.CreateFleet("donor", context.Faction.InstanceID);
            context.Game.AttachNode(donor, source);
            CapitalShip anchor = CreateCapitalShip("donor-anchor", context.Faction.InstanceID, 100);
            anchor.RegimentCapacity = 0;
            context.Game.AttachNode(anchor, donor);
            ship = CreateCapitalShip("donor-ship", context.Faction.InstanceID, 100);
            ship.RegimentCapacity = carriesRegiment ? 1 : 6;
            context.Game.AttachNode(ship, donor);
            if (carriesRegiment)
                context.Game.AttachNode(
                    CreateRegiment("carried", context.Faction.InstanceID),
                    ship
                );
            AITurnContext updated = CreateContext(context.Game, context.Faction);
            updated.SetAttackDemands(demands);
            return updated;
        }

        /// <summary>
        /// Creates competing fleet requirements around a shared regiment source.
        /// </summary>
        /// <param name="shielded">Whether the bombardment target has a planetary shield.</param>
        /// <param name="occupied">Whether the bombardment target has a defending regiment.</param>
        /// <param name="bombardFleet">Receives the fleet assigned to the bombardment target.</param>
        /// <param name="otherFleet">Receives the fleet assigned to the competing target.</param>
        /// <param name="source">Receives the planet supplying spare regiments.</param>
        /// <returns>The assessed faction turn for both targets.</returns>
        public static AITurnContext CreateBombardmentReinforcementContext(
            bool shielded,
            bool occupied,
            out Fleet bombardFleet,
            out Fleet otherFleet,
            out Planet source
        )
        {
            GameRoot game = CreateGame(out Faction faction, out Faction enemy);
            game.Config.AI.FleetDeployment.MinimumAttackStrength = 1;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount = 1;
            game.Config.AI.FleetDeployment.AttackStrengthPercentOfDefense = 100;
            game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultSuccessPercent = 0;
            game.Config.Combat.PlanetaryAssault.CaptureGarrisonCount = 1;
            game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit = 2;
            game.Config.AI.Infrastructure.FleetAllocationUtility =
                new GameConfig.AIFleetProductionAllocationUtilityConfig
                {
                    AttackRequirements = new GameConfig.AIConsiderationConfig
                    {
                        Weight = 1,
                        SaturationValue = 6,
                    },
                };
            PlanetSector sector = AddSector(game, "sector");
            source = AddPlanet(game, sector, "source", faction.InstanceID);
            source.IsColonized = false;
            source.SetPopularSupport(faction.InstanceID, 100);
            for (int index = 0; index < 3; index++)
                game.AttachNode(CreateRegiment("spare-" + index, faction.InstanceID), source);
            Planet bombardTarget = AddPlanet(
                game,
                sector,
                "bombard-target",
                enemy.InstanceID,
                positionX: 100
            );
            Planet otherTarget = AddPlanet(
                game,
                sector,
                "other-target",
                enemy.InstanceID,
                positionX: 100
            );
            foreach (Planet target in new[] { bombardTarget, otherTarget })
            {
                Regiment defender = CreateRegiment(
                    target.InstanceID + "-defender",
                    enemy.InstanceID
                );
                defender.DefenseRating = 100;
                game.AttachNode(defender, target);
            }
            if (shielded)
            {
                Building shield = CreateBuildingTemplate("shield", BuildingType.Defense);
                shield.OwnerInstanceID = enemy.InstanceID;
                shield.ShieldStrength = 2;
                game.AttachNode(shield, bombardTarget);
            }
            RevealPlanet(game, faction, bombardTarget);
            RevealPlanet(game, faction, otherTarget);
            bombardFleet = EntityFactory.CreateFleet("bombard-fleet", faction.InstanceID);
            otherFleet = EntityFactory.CreateFleet("other-fleet", faction.InstanceID);
            foreach (Fleet fleet in new[] { bombardFleet, otherFleet })
            {
                fleet.RoleType = FleetRoleType.Battle;
                game.AttachNode(fleet, source);
                CapitalShip ship = CreateCapitalShip(
                    fleet.InstanceID + "-ship",
                    faction.InstanceID,
                    100
                );
                ship.Bombardment = fleet == bombardFleet ? 1 : 0;
                ship.RegimentCapacity = fleet == bombardFleet ? 1 : 3;
                game.AttachNode(ship, fleet);
                if (occupied && fleet == bombardFleet)
                    game.AttachNode(CreateRegiment("occupation", faction.InstanceID), ship);
                fleet.Order = new FleetOrder
                {
                    OrderType = FleetOrderType.Attack,
                    Status = FleetOrderStatus.Building,
                    TargetPlanetId =
                        fleet == bombardFleet ? bombardTarget.InstanceID : otherTarget.InstanceID,
                };
            }
            return CreateContext(game, faction);
        }

        /// <summary>
        /// Creates a known empty destination and a separately observed defended reference world.
        /// </summary>
        /// <param name="age">The age of the recorded intelligence.</param>
        /// <returns>The faction turn used to evaluate mission risk.</returns>
        public static AITurnContext CreateMissionDefenseContext(int age)
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.MissionPlanning.MissionDefenseIntelFullWeightTicks = 100;
            game.Config.AI.MissionPlanning.MissionDefenseFallbackGroundDetectorCount = 0;
            game.Config.AI.MissionPlanning.MissionDefenseFallbackOrbitalDetectorCount = 0;
            game.Config.AI.MissionPlanning.MissionDefenseFallbackDetectionRating = 50;
            game.Config.AI.MissionPlanning.MissionDefenseFallbackCommanderEspionage = 50;
            game.Config.AI.MissionPlanning.MissionDefenseFallbackCommanderCombat = 50;
            game.Config.ProbabilityTables.Mission.Foil = new Dictionary<int, int> { { -1000, 50 } };
            game.Config.ProbabilityTables.Mission.PlanetaryDecoy = new Dictionary<int, int>
            {
                { -1000, 50 },
            };
            game.Config.ProbabilityTables.Mission.FleetDecoy = new Dictionary<int, int>
            {
                { -1000, 50 },
            };
            game.Config.ProbabilityTables.Mission.Evasion = new Dictionary<int, int>
            {
                { -1000, 50 },
            };
            game.Config.DuelResolution.CaptureEvasionInjuryBaseChance = 0;
            game.Config.DuelResolution.MinimumInjuryChance = 0;
            game.Config.Assassination.KillProbability = 0;
            PlanetSector sector = AITestSceneBuilder.AddSector(game, "sector");
            Planet origin = AITestSceneBuilder.AddPlanet(game, sector, "origin", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, sector, "target", rebels.InstanceID);
            Planet reference = AITestSceneBuilder.AddPlanet(
                game,
                sector,
                "reference",
                rebels.InstanceID
            );
            Officer spy = EntityFactory.CreateOfficer("spy", empire.InstanceID);
            spy.SetBaseRating(SkillRating.Espionage, 50);
            spy.SetBaseRating(SkillRating.Combat, 50);
            game.AttachNode(spy, origin);
            for (int index = 0; index < 2; index++)
                game.AttachNode(
                    new Regiment
                    {
                        InstanceID = "reference-" + index,
                        OwnerInstanceID = rebels.InstanceID,
                        DetectionRating = 100,
                        ManufacturingStatus = ManufacturingStatus.Complete,
                    },
                    reference
                );
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            AITestSceneBuilder.RevealPlanet(game, empire, reference);
            game.CurrentTick = age;
            return AITestSceneBuilder.CreateContext(game, empire);
        }

        /// <summary>
        /// Creates game.
        /// </summary>
        /// <param name="empire">Receives the empire.</param>
        /// <param name="rebels">Receives the rebels.</param>
        /// <returns>The created game.</returns>
        public static GameRoot CreateGame(out Faction empire, out Faction rebels)
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            empire = new Faction { InstanceID = "empire" };
            rebels = new Faction { InstanceID = "rebels" };
            empire.Settings.ResourceProcessingPointsPerFacility = 50;
            rebels.Settings.ResourceProcessingPointsPerFacility = 50;
            game.GetFactions().Add(empire);
            game.GetFactions().Add(rebels);
            return game;
        }

        /// <summary>
        /// Adds sector.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="positionX">The position x.</param>
        /// <param name="positionY">The position y.</param>
        /// <returns>The result of add sector.</returns>
        public static PlanetSector AddSector(
            GameRoot game,
            string instanceId,
            int positionX = 0,
            int positionY = 0
        )
        {
            PlanetSector planetSector = new PlanetSector
            {
                InstanceID = instanceId,
                PositionX = positionX,
                PositionY = positionY,
            };
            game.AttachNode(planetSector, game.Galaxy);
            return planetSector;
        }

        /// <summary>
        /// Adds planet.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planetSector">The planet sector.</param>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="ownerInstanceId">The owner instance id.</param>
        /// <param name="positionX">The position x.</param>
        /// <param name="positionY">The position y.</param>
        /// <param name="energyCapacity">The energy capacity.</param>
        /// <param name="rawResourceNodes">The raw resource nodes.</param>
        /// <returns>The result of add planet.</returns>
        public static Planet AddPlanet(
            GameRoot game,
            PlanetSector planetSector,
            string instanceId,
            string ownerInstanceId,
            int positionX = 0,
            int positionY = 0,
            int energyCapacity = 10,
            int rawResourceNodes = 0
        )
        {
            Planet planet = new Planet
            {
                InstanceID = instanceId,
                DisplayName = instanceId,
                OwnerInstanceID = ownerInstanceId,
                PositionX = positionX,
                PositionY = positionY,
                IsColonized = true,
                EnergyCapacity = energyCapacity,
                NumRawResourceNodes = rawResourceNodes,
            };
            game.AttachNode(planet, planetSector);
            return planet;
        }

        /// <summary>
        /// Adds production facility.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="buildingType">The building type.</param>
        /// <param name="productionType">The production type.</param>
        /// <param name="processRate">The process rate.</param>
        /// <returns>The result of add production facility.</returns>
        public static Building AddProductionFacility(
            GameRoot game,
            Planet planet,
            string instanceId,
            BuildingType buildingType,
            ManufacturingType productionType,
            int processRate = 1
        )
        {
            Building building = CreateBuildingTemplate(instanceId, buildingType, productionType);
            building.OwnerInstanceID = planet.OwnerInstanceID;
            building.ManufacturingStatus = ManufacturingStatus.Complete;
            building.ProcessRate = processRate;
            game.AttachNode(building, planet);
            return building;
        }

        /// <summary>
        /// Creates building template.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="buildingType">The building type.</param>
        /// <param name="productionType">The production type.</param>
        /// <returns>The created building template.</returns>
        public static Building CreateBuildingTemplate(
            string instanceId,
            BuildingType buildingType,
            ManufacturingType productionType = ManufacturingType.Building
        )
        {
            return new Building
            {
                InstanceID = instanceId,
                TypeID = instanceId,
                DisplayName = instanceId,
                BuildingType = buildingType,
                ProductionType = productionType,
                ConstructionCost = 10,
                BaseBuildSpeed = 1,
                ProcessRate = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }

        /// <summary>
        /// Creates capital ship.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="ownerInstanceId">The owner instance id.</param>
        /// <param name="combatStrength">The combat strength.</param>
        /// <param name="regimentCapacity">The regiment capacity.</param>
        /// <param name="starfighterCapacity">The starfighter capacity.</param>
        /// <returns>The created capital ship.</returns>
        public static CapitalShip CreateCapitalShip(
            string instanceId,
            string ownerInstanceId,
            int combatStrength = 100,
            int regimentCapacity = 1,
            int starfighterCapacity = 1
        )
        {
            int durabilityStrength = System.Math.Max(1, combatStrength);
            CapitalShip ship = new CapitalShip
            {
                InstanceID = instanceId,
                TypeID = instanceId,
                DisplayName = instanceId,
                ManufacturingFactionInstanceIDs = new List<string> { ownerInstanceId },
                OwnerInstanceID = ownerInstanceId,
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxHullStrength = durabilityStrength,
                CurrentHullStrength = durabilityStrength,
                RegimentCapacity = regimentCapacity,
                StarfighterCapacity = starfighterCapacity,
                Maneuverability = 1,
                WeaponRecharge = 1,
            };
            ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser][0] = combatStrength;
            return ship;
        }

        /// <summary>
        /// Creates regiment.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="ownerInstanceId">The owner instance id.</param>
        /// <param name="attackRating">The attack rating.</param>
        /// <param name="defenseRating">The defense rating.</param>
        /// <returns>The created regiment.</returns>
        public static Regiment CreateRegiment(
            string instanceId,
            string ownerInstanceId,
            int attackRating = 10,
            int defenseRating = 10
        )
        {
            return new Regiment
            {
                InstanceID = instanceId,
                TypeID = instanceId,
                DisplayName = instanceId,
                OwnerInstanceID = ownerInstanceId,
                ManufacturingStatus = ManufacturingStatus.Complete,
                AttackRating = attackRating,
                DefenseRating = defenseRating,
            };
        }

        /// <summary>
        /// Creates starfighter.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="ownerInstanceId">The owner instance id.</param>
        /// <param name="laserCannon">The laser cannon.</param>
        /// <param name="ionCannon">The ion cannon.</param>
        /// <param name="torpedoes">The torpedoes.</param>
        /// <param name="maintenanceCost">The maintenance cost.</param>
        /// <param name="constructionCost">The construction cost.</param>
        /// <returns>The created starfighter.</returns>
        public static Starfighter CreateStarfighter(
            string instanceId,
            string ownerInstanceId,
            int laserCannon = 1,
            int ionCannon = 0,
            int torpedoes = 0,
            int maintenanceCost = 0,
            int constructionCost = 1
        )
        {
            return new Starfighter
            {
                InstanceID = instanceId,
                TypeID = instanceId,
                DisplayName = instanceId,
                ManufacturingFactionInstanceIDs = new List<string> { ownerInstanceId },
                OwnerInstanceID = ownerInstanceId,
                ConstructionCost = constructionCost,
                MaintenanceCost = maintenanceCost,
                BaseBuildSpeed = 1,
                MaxSquadronSize = 12,
                CurrentSquadronSize = 12,
                Agility = 1,
                LaserCannon = laserCannon,
                IonCannon = ionCannon,
                Torpedoes = torpedoes,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }

        /// <summary>
        /// Creates special forces.
        /// </summary>
        /// <param name="typeId">The type id.</param>
        /// <param name="ownerInstanceId">The owner instance id.</param>
        /// <param name="allowedMissionTypeIds">The allowed mission type ids.</param>
        /// <returns>The created special forces.</returns>
        public static SpecialForces CreateSpecialForces(
            string typeId,
            string ownerInstanceId,
            params string[] allowedMissionTypeIds
        )
        {
            SpecialForces specialForces = new SpecialForces
            {
                TypeID = typeId,
                DisplayName = typeId,
                ManufacturingFactionInstanceIDs = new List<string> { ownerInstanceId },
                OwnerInstanceID = ownerInstanceId,
                ConstructionCost = 1,
                MaintenanceCost = 0,
                BaseBuildSpeed = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
                AllowedMissionTypeIDs = new List<string>(allowedMissionTypeIds),
            };
            return specialForces;
        }

        /// <summary>
        /// Creates context.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="faction">The faction.</param>
        /// <param name="missions">The missions.</param>
        /// <param name="movement">The movement.</param>
        /// <param name="manufacturing">The manufacturing.</param>
        /// <param name="bombardment">The bombardment.</param>
        /// <param name="planetaryAssault">The planetary assault.</param>
        /// <param name="random">The random.</param>
        /// <param name="maintenance">The maintenance.</param>
        /// <param name="headquarters">The headquarters system.</param>
        /// <returns>The created context.</returns>
        public static AITurnContext CreateContext(
            GameRoot game,
            Faction faction,
            MissionCommands missions = null,
            MovementCommands movement = null,
            ManufacturingCommands manufacturing = null,
            BombardmentCommands bombardment = null,
            PlanetaryAssaultCommands planetaryAssault = null,
            IRandomNumberProvider random = null,
            MaintenanceCommands maintenance = null,
            HeadquartersCommands headquarters = null
        )
        {
            IRandomNumberProvider provider = random ?? new StubRNG();
            FogOfWarCommands fog = new FogOfWarCommands(game);
            FogOfWarQueries fogQueries = new FogOfWarQueries(game);
            FleetCommands fleetSystem = new FleetCommands(game);
            HeadquartersQueries headquartersQueries = new HeadquartersQueries(game);
            MovementQueries movementQueries = new MovementQueries(game);
            movementQueries.SetCompletedBuildingMovementPolicy(headquartersQueries.CanMove);
            MovementCommands movementSystem =
                movement
                ?? new MovementCommands(game, fog, fleetSystem, fogQueries, movementQueries);
            MissionCommands missionSystem =
                missions ?? TestSystems.CreateMissionCommands(game, provider, movementSystem);
            ManufacturingCommands manufacturingSystem =
                manufacturing
                ?? new ManufacturingCommands(
                    game,
                    fleetSystem,
                    new ManufacturingQueries(game),
                    movementSystem
                );
            PlanetaryControlCommands planetaryControl = new PlanetaryControlCommands(
                game,
                new PlanetaryControlQueries(game),
                fogQueries
            );
            BombardmentCommands bombardmentSystem =
                bombardment
                ?? new BombardmentCommands(game, provider, new BombardmentQueries(game));
            PlanetaryAssaultCommands planetaryAssaultSystem =
                planetaryAssault
                ?? new PlanetaryAssaultCommands(
                    game,
                    provider,
                    planetaryControl,
                    new PlanetaryAssaultQueries(game)
                );
            MaintenanceCommands maintenanceSystem =
                maintenance ?? new MaintenanceCommands(game, provider);
            HeadquartersCommands headquartersSystem =
                headquarters ?? new HeadquartersCommands(game, movementSystem, headquartersQueries);
            GalaxyMap factionView = fogQueries.BuildFactionView(faction);
            AIAssessment assessment = new AIAssessment(game, faction, factionView);
            AIStrategicPlan strategicPlan = new AIStrategicPlan(game, assessment);

            AITurnContext context = new AITurnContext(
                game,
                faction,
                missionSystem,
                movementSystem,
                manufacturingSystem,
                bombardmentSystem,
                planetaryAssaultSystem,
                provider,
                assessment,
                strategicPlan,
                factionView,
                maintenanceSystem,
                headquartersSystem
            );
            new AIAttackDemandGenerator().Generate(context);
            return context;
        }

        /// <summary>
        /// Executes reveal planet.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="faction">The faction.</param>
        /// <param name="planet">The planet.</param>
        public static void RevealPlanet(GameRoot game, Faction faction, Planet planet)
        {
            PlanetSector system = planet.GetParentOfType<PlanetSector>();
            new FogOfWarRecorder().RecordEspionageSnapshot(
                faction,
                planet,
                system,
                game.CurrentTick
            );
        }
    }
}

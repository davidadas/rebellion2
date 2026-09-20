using System.Collections.Generic;
using Rebellion.AI.Director;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Simulation;
using Rebellion.Util.Random;

namespace Rebellion.Tests.AI.Helpers
{
    internal static class AITestSceneBuilder
    {
        /// <summary>
        /// Creates game.
        /// </summary>
        /// <param name="empire">Receives the empire.</param>
        /// <param name="rebels">Receives the rebels.</param>
        /// <returns>The created game.</returns>
        public static GameRoot CreateGame(out Faction empire, out Faction rebels)
        {
            GameRoot game = new GameRoot(TestConfig.Create());
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
                DisplayName = instanceId,
                ManufacturingFactionInstanceIDs = new List<string> { ownerInstanceId },
                OwnerInstanceID = ownerInstanceId,
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxHullStrength = durabilityStrength,
                CurrentHullStrength = durabilityStrength,
                RegimentCapacity = regimentCapacity,
                StarfighterCapacity = starfighterCapacity,
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
        /// <param name="random">The random.</param>
        /// <returns>The created context.</returns>
        public static AITurnContext CreateContext(
            GameRoot game,
            Faction faction,
            IRandomNumberProvider random = null
        )
        {
            IRandomNumberProvider provider = random ?? new StubRNG();
            GameSession session = GameSessionFactory.Compose(game, TestContent.Data);
            FogOfWar fog = session.Features.FogOfWar;

            return new AITurnContext(
                game,
                faction,
                session.Features.Commands,
                session.Queries,
                provider,
                fog.BuildFactionView(faction)
            );
        }

        /// <summary>
        /// Creates a minimal valid context for isolated AI phase tests.
        /// </summary>
        /// <returns>The minimal AI turn context.</returns>
        public static AITurnContext CreateContext()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction faction = new Faction { InstanceID = "faction" };
            game.GetFactions().Add(faction);
            return CreateContext(game, faction);
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

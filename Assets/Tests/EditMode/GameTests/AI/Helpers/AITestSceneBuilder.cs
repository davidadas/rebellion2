using System.Collections.Generic;
using Rebellion.AI.Director;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Systems;
using Rebellion.Util.Common;

namespace Rebellion.Tests.AI.Helpers
{
    public static class AITestSceneBuilder
    {
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

        public static AITurnContext CreateContext(
            GameRoot game,
            Faction faction,
            MissionSystem missions = null,
            MovementSystem movement = null,
            ManufacturingSystem manufacturing = null,
            BombardmentSystem bombardment = null,
            PlanetaryAssaultSystem planetaryAssault = null,
            IRandomNumberProvider random = null
        )
        {
            IRandomNumberProvider provider = random ?? new StubRNG();
            FogOfWarSystem fog = new FogOfWarSystem(game);
            FleetSystem fleetSystem = new FleetSystem(game);
            MovementSystem movementSystem = movement ?? new MovementSystem(game, fog, fleetSystem);
            MissionSystem missionSystem =
                missions ?? TestSystems.CreateMissionSystem(game, provider, movementSystem);
            ManufacturingSystem manufacturingSystem =
                manufacturing ?? new ManufacturingSystem(game, fleetSystem, movementSystem);
            PlanetaryControlSystem planetaryControl = new PlanetaryControlSystem(
                game,
                movementSystem,
                manufacturingSystem,
                fog
            );
            BombardmentSystem bombardmentSystem =
                bombardment
                ?? new BombardmentSystem(game, provider, movementSystem, planetaryControl);
            PlanetaryAssaultSystem planetaryAssaultSystem =
                planetaryAssault ?? new PlanetaryAssaultSystem(game, provider, planetaryControl);

            return new AITurnContext(
                game,
                faction,
                missionSystem,
                movementSystem,
                manufacturingSystem,
                bombardmentSystem,
                planetaryAssaultSystem,
                provider,
                fog.BuildFactionView(faction)
            );
        }

        public static void RevealPlanet(GameRoot game, Faction faction, Planet planet)
        {
            PlanetSector system = planet.GetParentOfType<PlanetSector>();
            new FogOfWarSystem(game).CaptureSnapshot(faction, planet, system, game.CurrentTick);
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Core.Configuration;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Systems;

namespace Rebellion.Tests.Managers
{
    [TestFixture]
    public class AIManagerTests
    {
        private class FixedRNG : IRandomNumberProvider
        {
            public double NextDouble() => 0.5;

            public int NextInt(int min, int max) => min;
        }

        /// <summary>Always returns the last valid index (max - 1) from NextInt.</summary>
        private class LastIndexRNG : IRandomNumberProvider
        {
            public double NextDouble() => 0.99;

            public int NextInt(int min, int max) => max - 1;
        }

        /// <summary>
        /// Builds a minimal scene: one AI faction, one colonized planet at max popular support,
        /// one available officer with high diplomacy skill.
        /// </summary>
        private (GameRoot game, Planet planet, Officer officer, AIManager ai) BuildScene(
            int popularSupport
        )
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);

            // AI faction — no PlayerID means IsAIControlled() returns true
            Faction faction = new Faction { InstanceID = "empire", PlayerID = null };
            game.Factions.Add(faction);

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(system, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int> { { "empire", popularSupport } },
            };
            game.AttachNode(planet, system);

            Officer officer = new Officer
            {
                InstanceID = "o1",
                OwnerInstanceID = "empire",
                Movement = null,
            };
            // High diplomacy and leadership so the table lookup would pass without the guard
            officer.SetSkillValue(MissionParticipantSkill.Diplomacy, 80);
            officer.SetSkillValue(MissionParticipantSkill.Leadership, 20);
            game.AttachNode(officer, planet);

            FogOfWarSystem fog = new FogOfWarSystem(game);
            MovementSystem movement = new MovementSystem(game, fog);
            OwnershipSystem ownership = new OwnershipSystem(
                game,
                movement,
                new ManufacturingSystem(game)
            );
            MissionSystem missionSystem = new MissionSystem(game, movement, ownership);
            ManufacturingSystem manufacturing = new ManufacturingSystem(game);

            AIManager ai = new AIManager(game, missionSystem, movement, manufacturing);
            return (game, planet, officer, ai);
        }

        [Test]
        public void Update_PlanetAtMaxSupport_DoesNotDispatchDiplomacyMission()
        {
            (GameRoot game, Planet planet, Officer officer, AIManager ai) = BuildScene(
                popularSupport: 100
            );

            Assert.DoesNotThrow(
                () => ai.Update(new FixedRNG()),
                "AI Update should not throw when all planets are at max popular support"
            );

            List<DiplomacyMission> missions = game.GetSceneNodesByType<DiplomacyMission>();
            Assert.AreEqual(
                0,
                missions.Count,
                "No DiplomacyMission should be dispatched to a planet at max popular support"
            );
        }

        [Test]
        public void FindMissionTarget_LastCandidate_IsReachable()
        {
            // Two colonized planets owned by the faction.
            // LastIndexRNG always returns max-1, so the second (last) planet must be selected.
            // Before the off-by-one fix, NextInt(0, count-1) with max-1 returned count-2,
            // making the last candidate unreachable.
            GameConfig config = new GameConfig();
            // Seed the diplomacy dispatch table so score=50 passes the lookup.
            config.AI.MissionTables.Diplomacy[0] = 20;
            GameRoot game = new GameRoot(config);

            Faction faction = new Faction { InstanceID = "empire", PlayerID = null };
            game.Factions.Add(faction);

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(system, game.Galaxy);

            Planet planet1 = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int> { { "empire", 50 } },
            };
            game.AttachNode(planet1, system);

            Planet planet2 = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 10,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int> { { "empire", 50 } },
            };
            game.AttachNode(planet2, system);

            Officer officer = new Officer
            {
                InstanceID = "o1",
                OwnerInstanceID = "empire",
                Movement = null,
            };
            officer.SetSkillValue(MissionParticipantSkill.Diplomacy, 80);
            officer.SetSkillValue(MissionParticipantSkill.Leadership, 20);
            game.AttachNode(officer, planet1);

            FogOfWarSystem fog = new FogOfWarSystem(game);
            MovementSystem movement = new MovementSystem(game, fog);
            OwnershipSystem ownership = new OwnershipSystem(
                game,
                movement,
                new ManufacturingSystem(game)
            );
            MissionSystem missionSystem = new MissionSystem(game, movement, ownership);
            ManufacturingSystem manufacturing = new ManufacturingSystem(game);
            AIManager ai = new AIManager(game, missionSystem, movement, manufacturing);

            ai.Update(new LastIndexRNG());

            // With two candidates and LastIndexRNG returning index 1, planet2 must be targeted.
            List<DiplomacyMission> missions = game.GetSceneNodesByType<DiplomacyMission>();
            Assert.IsTrue(
                missions.Any(m => m.TargetInstanceID == "p2"),
                "Last candidate (p2) must be reachable when RNG returns the final index"
            );
        }

        [Test]
        public void Update_PlanetBelowMaxSupport_CanDispatchDiplomacyMission()
        {
            // Support below max — diplomacy should be a valid candidate for a skilled officer
            (GameRoot game, Planet planet, Officer officer, AIManager ai) = BuildScene(
                popularSupport: 50
            );

            Assert.DoesNotThrow(
                () => ai.Update(new FixedRNG()),
                "AI Update should not throw when planet support is below max"
            );
        }

        /// <summary>
        /// Builds a two-faction scene for testing the enemy mission dispatch path.
        /// Empire (AI, no PlayerID) has an officer on an owned planet at max support.
        /// Rebels (player-controlled) own a separate colonized planet.
        /// The empire planet's max support blocks all friendly mission dispatches,
        /// forcing SelectMissionType to return null so the enemy path is reached.
        /// </summary>
        private (
            GameRoot game,
            Planet empPlanet,
            Planet enemyPlanet,
            Officer officer,
            AIManager ai
        ) BuildEnemyScene()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);

            Faction empire = new Faction { InstanceID = "empire", PlayerID = null };
            Faction rebels = new Faction { InstanceID = "rebels", PlayerID = "player1" };
            game.Factions.Add(empire);
            game.Factions.Add(rebels);

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(system, game.Galaxy);

            Planet empPlanet = new Planet
            {
                InstanceID = "emp_planet",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int> { { "empire", 100 } },
            };
            game.AttachNode(empPlanet, system);

            Planet enemyPlanet = new Planet
            {
                InstanceID = "enemy_planet",
                OwnerInstanceID = "rebels",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
                GroundSlots = 5,
                OrbitSlots = 5,
                PopularSupport = new Dictionary<string, int> { { "rebels", 20 } },
            };
            game.AttachNode(enemyPlanet, system);

            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            game.AttachNode(officer, empPlanet);

            FogOfWarSystem fog = new FogOfWarSystem(game);
            MovementSystem movement = new MovementSystem(game, fog);
            OwnershipSystem ownership = new OwnershipSystem(
                game,
                movement,
                new ManufacturingSystem(game)
            );
            MissionSystem missionSystem = new MissionSystem(game, movement, ownership);
            ManufacturingSystem manufacturing = new ManufacturingSystem(game);
            AIManager ai = new AIManager(game, missionSystem, movement, manufacturing);

            return (game, empPlanet, enemyPlanet, officer, ai);
        }

        [Test]
        public void Update_EnemyPlanetWithoutUprising_DispatchesInciteUprising()
        {
            (GameRoot game, Planet empPlanet, Planet enemyPlanet, Officer officer, AIManager ai) =
                BuildEnemyScene();

            // Score = espionage(50) - enemySupport(20) - enemyStrength(0) = 30
            game.Config.AI.MissionTables.InciteUprising[0] = 5;

            ai.Update(new StubRNG());

            List<InciteUprisingMission> missions =
                game.GetSceneNodesByType<InciteUprisingMission>();
            Assert.IsTrue(
                missions.Any(),
                "AI should dispatch InciteUprising to an enemy planet when the table score passes"
            );
        }

        [Test]
        public void Update_EnemyPlanet_InciteUprisingTableEmpty_DispatchesEspionage()
        {
            (GameRoot game, Planet empPlanet, Planet enemyPlanet, Officer officer, AIManager ai) =
                BuildEnemyScene();

            // InciteUprising table empty → score 0 → skipped
            // Espionage: score = espionage(50), table passes
            game.Config.AI.MissionTables.Espionage[0] = 5;

            ai.Update(new StubRNG());

            List<EspionageMission> missions = game.GetSceneNodesByType<EspionageMission>();
            Assert.IsTrue(
                missions.Any(),
                "AI should dispatch Espionage when InciteUprising table is empty"
            );
        }

        [Test]
        public void Update_EnemyPlanetWithBuilding_DispatchesSabotage()
        {
            (GameRoot game, Planet empPlanet, Planet enemyPlanet, Officer officer, AIManager ai) =
                BuildEnemyScene();

            Building building = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "rebels",
                BuildingType = BuildingType.Mine,
                BuildingSlot = BuildingSlot.Ground,
            };
            game.AttachNode(building, enemyPlanet);

            // InciteUprising and Espionage tables empty → skipped
            // Sabotage: no defender officer present, so score = (espionage(50) + 0) / 2 = 25
            game.Config.AI.MissionTables.Sabotage[0] = 5;

            ai.Update(new StubRNG());

            List<SabotageMission> missions = game.GetSceneNodesByType<SabotageMission>();
            Assert.IsTrue(
                missions.Any(),
                "AI should dispatch Sabotage to an enemy planet with buildings"
            );
        }

        [Test]
        public void Update_EnemyPlanetWithBuilding_SabotageScoreUsesDefenderCombat()
        {
            (GameRoot game, Planet empPlanet, Planet enemyPlanet, Officer officer, AIManager ai) =
                BuildEnemyScene();

            Building building = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "rebels",
                BuildingType = BuildingType.Mine,
                BuildingSlot = BuildingSlot.Ground,
            };
            game.AttachNode(building, enemyPlanet);

            // Defender officer with combat 100 → score = (espionage(50) + combat(100)) / 2 = 75
            Officer defenderOfficer = EntityFactory.CreateOfficer("defender", "rebels");
            defenderOfficer.SetSkillValue(MissionParticipantSkill.Combat, 100);
            game.AttachNode(defenderOfficer, enemyPlanet);

            // InciteUprising and Espionage tables empty → skipped
            // Sabotage table only passes at score >= 80 — 75 should NOT pass
            game.Config.AI.MissionTables.Sabotage[80] = 5;

            ai.Update(new StubRNG());

            List<SabotageMission> sabotageMissions = game.GetSceneNodesByType<SabotageMission>();
            Assert.IsFalse(
                sabotageMissions.Any(),
                "Sabotage should not dispatch when (attacker.espionage + defender.combat) / 2 is below threshold"
            );
        }

        [Test]
        public void Update_EnemyPlanetWithEnemyOfficer_DispatchesAbduction()
        {
            (GameRoot game, Planet empPlanet, Planet enemyPlanet, Officer officer, AIManager ai) =
                BuildEnemyScene();

            Officer enemyOfficer = EntityFactory.CreateOfficer("enemy_officer", "rebels");
            game.AttachNode(enemyOfficer, enemyPlanet);

            // InciteUprising, Espionage tables empty; no buildings so Sabotage skipped
            // Abduction: score = combat(50) - targetCombat(50) = 0, table passes
            game.Config.AI.MissionTables.Abduction[0] = 5;

            ai.Update(new StubRNG());

            List<AbductionMission> missions = game.GetSceneNodesByType<AbductionMission>();
            Assert.IsTrue(
                missions.Any(),
                "AI should dispatch Abduction when an uncaptured enemy officer is present"
            );
        }

        [Test]
        public void Update_EnemyPlanetWithEnemyOfficer_DispatchesAssassination()
        {
            (GameRoot game, Planet empPlanet, Planet enemyPlanet, Officer officer, AIManager ai) =
                BuildEnemyScene();

            Officer enemyOfficer = EntityFactory.CreateOfficer("enemy_officer", "rebels");
            game.AttachNode(enemyOfficer, enemyPlanet);

            // InciteUprising, Espionage tables empty; no buildings so Sabotage skipped;
            // Abduction table empty → skipped
            // Assassination: score = combat(50) - targetCombat(50) = 0, table passes
            game.Config.AI.MissionTables.Assassination[0] = 5;

            ai.Update(new StubRNG());

            List<AssassinationMission> missions = game.GetSceneNodesByType<AssassinationMission>();
            Assert.IsTrue(
                missions.Any(),
                "AI should dispatch Assassination when an uncaptured enemy officer is present"
            );
        }

        [Test]
        public void Update_EnemyPlanetWithCapturedFriendlyOfficer_DispatchesRescue()
        {
            (GameRoot game, Planet empPlanet, Planet enemyPlanet, Officer officer, AIManager ai) =
                BuildEnemyScene();

            Officer captive = EntityFactory.CreateOfficer("captive", "empire");
            captive.IsCaptured = true;
            game.AttachNode(captive, enemyPlanet);

            // InciteUprising, Espionage tables empty; no buildings so Sabotage skipped;
            // no uncaptured enemy officer so Abduction/Assassination skipped
            // Rescue: score = captive.combat(50), table passes
            game.Config.AI.MissionTables.Rescue[0] = 5;

            ai.Update(new StubRNG());

            List<RescueMission> missions = game.GetSceneNodesByType<RescueMission>();
            Assert.IsTrue(
                missions.Any(),
                "AI should dispatch Rescue when a captured friendly officer is held at an enemy planet"
            );
        }

        [Test]
        public void GetCombatValue_MultipleShipsAndFighters_SumsAllAttackRatings()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);

            Faction faction = new Faction { InstanceID = "empire", PlayerID = null };
            game.Factions.Add(faction);

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(system, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(planet, system);

            Fleet fleet = new Fleet
            {
                InstanceID = "f1",
                OwnerInstanceID = "empire",
                RoleType = FleetRoleType.Battle,
            };
            game.AttachNode(fleet, planet);

            CapitalShip ship1 = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                StarfighterCapacity = 2,
            };
            ship1.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { 100 };
            CapitalShip ship2 = new CapitalShip
            {
                InstanceID = "cs2",
                OwnerInstanceID = "empire",
            };
            ship2.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { 150 };
            game.AttachNode(ship1, fleet);
            game.AttachNode(ship2, fleet);

            Starfighter fighter1 = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                LaserCannon = 20,
            };
            Starfighter fighter2 = new Starfighter
            {
                InstanceID = "sf2",
                OwnerInstanceID = "empire",
                LaserCannon = 30,
            };
            game.AttachNode(fighter1, fleet);
            game.AttachNode(fighter2, fleet);

            int combatValue = fleet.GetCombatValue();

            // Expected: 100 + 150 + 20 + 30 = 300
            Assert.AreEqual(
                300,
                combatValue,
                "Fleet combat value should sum capital ship and starfighter attack ratings"
            );
        }

        [Test]
        public void CalculateFleetAssaultStrength_AppliesPersonnelModifier()
        {
            // Formula: (personnel / divisor + 1) * fleet_combat_value
            GameConfig config = new GameConfig
            {
                Combat = new GameConfig.CombatConfig { AssaultPersonnelDivisor = 10 },
            };
            GameRoot game = new GameRoot(config);

            Faction faction = new Faction { InstanceID = "empire", PlayerID = null };
            game.Factions.Add(faction);

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(system, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(planet, system);

            Fleet fleet = new Fleet
            {
                InstanceID = "f1",
                OwnerInstanceID = "empire",
                RoleType = FleetRoleType.Battle,
            };
            game.AttachNode(fleet, planet);

            CapitalShip ship = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
            };
            ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { 100 };
            game.AttachNode(ship, fleet);

            Officer commander = new Officer { InstanceID = "o1", OwnerInstanceID = "empire" };
            commander.SetSkillValue(MissionParticipantSkill.Leadership, 50);
            game.AttachNode(commander, fleet);

            FogOfWarSystem fog = new FogOfWarSystem(game);
            MovementSystem movement = new MovementSystem(game, fog);
            ManufacturingSystem manufacturing = new ManufacturingSystem(game);
            OwnershipSystem ownership = new OwnershipSystem(game, movement, manufacturing);
            MissionSystem missionSystem = new MissionSystem(game, movement, ownership);
            AIManager ai = new AIManager(game, missionSystem, movement, manufacturing);

            var method = typeof(AIManager).GetMethod(
                "CalculateFleetAssaultStrength",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );
            int assaultStrength = (int)method.Invoke(ai, new object[] { fleet });

            // Expected: (50 / 10 + 1) * 100 = 6 * 100 = 600
            Assert.AreEqual(
                600,
                assaultStrength,
                "Assault strength should apply personnel modifier: (personnel/divisor + 1) * combat_value"
            );
        }

        [Test]
        public void CalculateFleetAssaultStrength_NoCommander_UsesBaseMultiplier()
        {
            // With no commander, personnel = 0, so formula becomes (0/10 + 1) * combat = 1 * combat
            GameConfig config = new GameConfig
            {
                Combat = new GameConfig.CombatConfig { AssaultPersonnelDivisor = 10 },
            };
            GameRoot game = new GameRoot(config);

            Faction faction = new Faction { InstanceID = "empire", PlayerID = null };
            game.Factions.Add(faction);

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(system, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(planet, system);

            Fleet fleet = new Fleet
            {
                InstanceID = "f1",
                OwnerInstanceID = "empire",
                RoleType = FleetRoleType.Battle,
            };
            game.AttachNode(fleet, planet);

            CapitalShip ship = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
            };
            ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { 100 };
            game.AttachNode(ship, fleet);

            FogOfWarSystem fog = new FogOfWarSystem(game);
            MovementSystem movement = new MovementSystem(game, fog);
            ManufacturingSystem manufacturing = new ManufacturingSystem(game);
            OwnershipSystem ownership = new OwnershipSystem(game, movement, manufacturing);
            MissionSystem missionSystem = new MissionSystem(game, movement, ownership);
            AIManager ai = new AIManager(game, missionSystem, movement, manufacturing);

            var method = typeof(AIManager).GetMethod(
                "CalculateFleetAssaultStrength",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
            );
            int assaultStrength = (int)method.Invoke(ai, new object[] { fleet });

            // Expected: (0 / 10 + 1) * 100 = 1 * 100 = 100
            Assert.AreEqual(
                100,
                assaultStrength,
                "Without commander, assault strength should equal base combat value (multiplier = 1)"
            );
        }

        [Test]
        public void CalculatePlanetDefenseStrength_SumsDefensiveBuildings()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);

            Faction faction = new Faction { InstanceID = "empire", PlayerID = null };
            game.Factions.Add(faction);

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(system, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                GroundSlots = 5,
                OrbitSlots = 5,
            };
            game.AttachNode(planet, system);

            Building defense1 = new Building
            {
                InstanceID = "d1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Defense,
                BuildingSlot = BuildingSlot.Ground,
                WeaponStrength = 50,
            };
            Building defense2 = new Building
            {
                InstanceID = "d2",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Defense,
                BuildingSlot = BuildingSlot.Ground,
                WeaponStrength = 75,
            };
            Building mine = new Building
            {
                InstanceID = "m1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Mine,
                BuildingSlot = BuildingSlot.Ground,
                WeaponStrength = 10,
            };
            game.AttachNode(defense1, planet);
            game.AttachNode(defense2, planet);
            game.AttachNode(mine, planet);

            int defenseStrength = planet.GetDefenseStrength();

            // Expected: 50 + 75 = 125 (mine doesn't count, only Defense type buildings)
            Assert.AreEqual(
                125,
                defenseStrength,
                "Defense strength should sum only defensive building ratings"
            );
        }

        [Test]
        public void BuildOneOf_PlanetAtCapacity_DoesNotThrow()
        {
            GameConfig config = ConfigLoader.LoadGameConfig();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire", PlayerID = null };
            game.Factions.Add(empire);

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(system, game.Galaxy);

            // Planet with 1 ground slot, already full
            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                GroundSlots = 1,
                OrbitSlots = 1,
                NumRawResourceNodes = 10,
                PopularSupport = new Dictionary<string, int> { { "empire", 100 } },
            };
            game.AttachNode(planet, system);

            // Fill the ground slot with a construction yard
            Building yard = new Building
            {
                InstanceID = "cy1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.ConstructionFacility,
                BuildingSlot = BuildingSlot.Ground,
                ProductionType = ManufacturingType.Building,
                ProcessRate = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(yard, planet);

            // Add a mine tech so the AI has something to try to build
            empire.AddTechnologyNode(
                0,
                new Technology(
                    new Building
                    {
                        InstanceID = "mine_template",
                        OwnerInstanceID = "empire",
                        BuildingType = BuildingType.Mine,
                        BuildingSlot = BuildingSlot.Ground,
                        ConstructionCost = 1,
                    }
                )
            );

            FogOfWarSystem fog = new FogOfWarSystem(game);
            MovementSystem movement = new MovementSystem(game, fog);
            ManufacturingSystem manufacturing = new ManufacturingSystem(game);
            OwnershipSystem ownership = new OwnershipSystem(game, movement, manufacturing);
            MissionSystem missionSystem = new MissionSystem(game, movement, ownership);
            AIManager ai = new AIManager(game, missionSystem, movement, manufacturing);

            // Should not throw — planet is full, AI should skip gracefully
            Assert.DoesNotThrow(
                () => ai.Update(new FixedRNG()),
                "AI should not crash when all planets are at building capacity"
            );
        }

        [Test]
        public void FactionModifiers_GarrisonEfficiency_ClampsToMinOne()
        {
            FactionModifiers mods = new FactionModifiers { GarrisonEfficiency = 0 };
            Assert.AreEqual(1, mods.GarrisonEfficiency, "GarrisonEfficiency should clamp to 1");

            mods.GarrisonEfficiency = -5;
            Assert.AreEqual(1, mods.GarrisonEfficiency, "Negative values should clamp to 1");
        }

        [Test]
        public void FactionModifiers_UprisingResistance_ClampsToMinOne()
        {
            FactionModifiers mods = new FactionModifiers { UprisingResistance = 0 };
            Assert.AreEqual(1, mods.UprisingResistance, "UprisingResistance should clamp to 1");
        }

        [Test]
        public void FactionModifiers_TroopEffectiveness_ClampsToMinOne()
        {
            FactionModifiers mods = new FactionModifiers { TroopEffectiveness = 0 };
            Assert.AreEqual(1, mods.TroopEffectiveness, "TroopEffectiveness should clamp to 1");
        }

        [Test]
        public void UpdateOffensiveFleetMovement_SendsFleetToEnemyPlanet()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire", PlayerID = null };
            Faction rebels = new Faction { InstanceID = "rebels", PlayerID = null };
            game.Factions.Add(empire);
            game.Factions.Add(rebels);

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(system, game.Galaxy);

            Planet empirePlanet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int> { { "empire", 100 } },
            };
            game.AttachNode(empirePlanet, system);

            Planet rebelPlanet = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "rebels",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int> { { "rebels", 50 } },
            };
            game.AttachNode(rebelPlanet, system);

            // Give empire a strong fleet
            Fleet fleet = new Fleet
            {
                InstanceID = "f1",
                OwnerInstanceID = "empire",
                RoleType = FleetRoleType.Battle,
            };
            game.AttachNode(fleet, empirePlanet);

            CapitalShip ship = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
            };
            ship.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { 100, 100, 100, 100 };
            game.AttachNode(ship, fleet);

            FogOfWarSystem fog = new FogOfWarSystem(game);
            MovementSystem movement = new MovementSystem(game, fog);
            ManufacturingSystem manufacturing = new ManufacturingSystem(game);
            OwnershipSystem ownership = new OwnershipSystem(game, movement, manufacturing);
            MissionSystem missionSystem = new MissionSystem(game, movement, ownership);
            AIManager ai = new AIManager(game, missionSystem, movement, manufacturing);

            ai.Update(new FixedRNG());

            // Fleet should have been sent toward the enemy planet
            Assert.IsNotNull(
                fleet.Movement,
                "AI should send idle battle fleet to attack enemy planet"
            );
            Assert.AreEqual(
                "p2",
                fleet.GetParentOfType<Planet>()?.InstanceID,
                "Fleet destination should be the enemy planet"
            );
        }

        [Test]
        public void UpdateOffensiveFleetMovement_DoesNotStackFleets()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire", PlayerID = null };
            Faction rebels = new Faction { InstanceID = "rebels", PlayerID = null };
            game.Factions.Add(empire);
            game.Factions.Add(rebels);

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(system, game.Galaxy);

            Planet empirePlanet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int> { { "empire", 100 } },
            };
            game.AttachNode(empirePlanet, system);

            Planet rebelPlanet = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "rebels",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int> { { "rebels", 50 } },
            };
            game.AttachNode(rebelPlanet, system);

            // First fleet already at rebel planet
            Fleet existingFleet = new Fleet
            {
                InstanceID = "f1",
                OwnerInstanceID = "empire",
                RoleType = FleetRoleType.Battle,
            };
            game.AttachNode(existingFleet, rebelPlanet);
            CapitalShip ship1 = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
            };
            ship1.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { 100 };
            game.AttachNode(ship1, existingFleet);

            // Second fleet idle at empire planet
            Fleet idleFleet = new Fleet
            {
                InstanceID = "f2",
                OwnerInstanceID = "empire",
                RoleType = FleetRoleType.Battle,
            };
            game.AttachNode(idleFleet, empirePlanet);
            CapitalShip ship2 = new CapitalShip
            {
                InstanceID = "cs2",
                OwnerInstanceID = "empire",
            };
            ship2.PrimaryWeapons[PrimaryWeaponType.Turbolaser] = new[] { 100 };
            game.AttachNode(ship2, idleFleet);

            FogOfWarSystem fog = new FogOfWarSystem(game);
            MovementSystem movement = new MovementSystem(game, fog);
            ManufacturingSystem manufacturing = new ManufacturingSystem(game);
            OwnershipSystem ownership = new OwnershipSystem(game, movement, manufacturing);
            MissionSystem missionSystem = new MissionSystem(game, movement, ownership);
            AIManager ai = new AIManager(game, missionSystem, movement, manufacturing);

            ai.Update(new FixedRNG());

            // Second fleet should NOT be sent since first is already at target
            Assert.IsNull(
                idleFleet.Movement,
                "AI should not stack fleets — skip targets with existing presence"
            );
        }

        [Test]
        public void UpdateCapitalShipProduction_StationaryFleetInSystem_UsesExistingFleet()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire", PlayerID = null };
            game.Factions.Add(empire);

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(system, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                OrbitSlots = 10,
                GroundSlots = 10,
                NumRawResourceNodes = 1000,
                PopularSupport = new Dictionary<string, int> { { "empire", 100 } },
            };
            game.AttachNode(planet, system);

            Building shipyard = new Building
            {
                InstanceID = "sy1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Shipyard,
                BuildingSlot = BuildingSlot.Orbit,
                ProductionType = ManufacturingType.Ship,
                ProcessRate = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(shipyard, planet);

            Building mine = new Building
            {
                InstanceID = "mine1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Mine,
                BuildingSlot = BuildingSlot.Ground,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(mine, planet);

            Building refinery = new Building
            {
                InstanceID = "ref1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Refinery,
                BuildingSlot = BuildingSlot.Ground,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(refinery, planet);

            Fleet stationaryFleet = new Fleet
            {
                InstanceID = "f1",
                OwnerInstanceID = "empire",
                RoleType = FleetRoleType.Battle,
                Movement = null,
            };
            game.AttachNode(stationaryFleet, planet);

            empire.AddTechnologyNode(
                0,
                new Technology(
                    new CapitalShip
                    {
                        InstanceID = "ship_template",
                        ConstructionCost = 1,
                        BaseBuildSpeed = 1,
                    }
                )
            );

            FogOfWarSystem fog = new FogOfWarSystem(game);
            MovementSystem movement = new MovementSystem(game, fog);
            ManufacturingSystem manufacturing = new ManufacturingSystem(game);
            OwnershipSystem ownership = new OwnershipSystem(game, movement, manufacturing);
            MissionSystem missionSystem = new MissionSystem(game, movement, ownership);
            AIManager ai = new AIManager(game, missionSystem, movement, manufacturing);

            ai.Update(new FixedRNG());

            Assert.AreEqual(
                1,
                stationaryFleet.CapitalShips.Count,
                "AI should place new capital ship into the existing stationary fleet"
            );
            Assert.AreEqual(
                1,
                empire.GetOwnedUnitsByType<Fleet>().Count,
                "AI should not create a new fleet when a stationary one already exists in the system"
            );
        }

        [Test]
        public void UpdateCapitalShipProduction_NoFleetInSystem_CreatesNewFleetAtShipyard()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire", PlayerID = null };
            game.Factions.Add(empire);

            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(system, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                OrbitSlots = 10,
                GroundSlots = 10,
                NumRawResourceNodes = 1000,
                PopularSupport = new Dictionary<string, int> { { "empire", 100 } },
            };
            game.AttachNode(planet, system);

            Building shipyard = new Building
            {
                InstanceID = "sy1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Shipyard,
                BuildingSlot = BuildingSlot.Orbit,
                ProductionType = ManufacturingType.Ship,
                ProcessRate = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(shipyard, planet);

            Building mine = new Building
            {
                InstanceID = "mine1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Mine,
                BuildingSlot = BuildingSlot.Ground,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(mine, planet);

            Building refinery = new Building
            {
                InstanceID = "ref1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Refinery,
                BuildingSlot = BuildingSlot.Ground,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(refinery, planet);

            empire.AddTechnologyNode(
                0,
                new Technology(
                    new CapitalShip
                    {
                        InstanceID = "ship_template",
                        ConstructionCost = 1,
                        BaseBuildSpeed = 1,
                    }
                )
            );

            FogOfWarSystem fog = new FogOfWarSystem(game);
            MovementSystem movement = new MovementSystem(game, fog);
            ManufacturingSystem manufacturing = new ManufacturingSystem(game);
            OwnershipSystem ownership = new OwnershipSystem(game, movement, manufacturing);
            MissionSystem missionSystem = new MissionSystem(game, movement, ownership);
            AIManager ai = new AIManager(game, missionSystem, movement, manufacturing);

            ai.Update(new FixedRNG());

            List<Fleet> fleets = empire.GetOwnedUnitsByType<Fleet>();
            Assert.AreEqual(
                1,
                fleets.Count,
                "AI should create one new fleet at the shipyard planet"
            );
            Assert.AreEqual(
                1,
                fleets[0].CapitalShips.Count,
                "The new fleet should contain the queued capital ship"
            );
            Assert.AreEqual(
                planet,
                ((ISceneNode)fleets[0]).GetParent(),
                "The new fleet should be parented to the shipyard planet"
            );
        }
    }
}

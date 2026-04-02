using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Core.Configuration;
using Rebellion.Core.Simulation;
using Rebellion.Game;
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
    }
}

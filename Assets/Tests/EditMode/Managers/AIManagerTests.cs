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
    }
}

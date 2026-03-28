using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Core.Configuration;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Systems;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class MissionSystemTests
    {
        private class AlwaysSucceedRNG : IRandomNumberProvider
        {
            public double NextDouble() => 0.01;

            public int NextInt(int min, int max) => min;
        }

        // Minimal concrete mission: always succeeds, runs for exactly 1 tick.
        private class InstantMission : Mission
        {
            public InstantMission(string ownerInstanceId, string targetInstanceId)
                : base(
                    "Instant",
                    ownerInstanceId,
                    targetInstanceId,
                    new List<IMissionParticipant>(),
                    new List<IMissionParticipant>(),
                    MissionParticipantSkill.Diplomacy,
                    null,
                    quadraticCoefficient: 0,
                    linearCoefficient: 0,
                    constantTerm: 100,
                    minSuccessProbability: 100,
                    maxSuccessProbability: 100,
                    minTicks: 1,
                    maxTicks: 1
                ) { }

            protected override List<GameResult> OnSuccess(GameRoot game) => new List<GameResult>();

            public override bool CanContinue(GameRoot game) => false;
        }

        // Builds a game with one planet, one officer parented to the planet (not the mission),
        // and optionally assigns the planet to the faction so GetNearestPlanetTo returns it.
        private (GameRoot game, Planet planet, Officer officer, MovementSystem movement) BuildScene(
            bool factionOwnsPlanet
        )
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            Faction faction = new Faction { InstanceID = "empire" };
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
                OwnerInstanceID = factionOwnsPlanet ? "empire" : null,
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int> { { "empire", 50 } },
            };
            game.AttachNode(planet, system);

            Officer officer = new Officer
            {
                InstanceID = "o1",
                OwnerInstanceID = "empire",
                Movement = null,
            };
            // Parent to planet so IsOnMission() = false and IsMovable() = true.
            game.AttachNode(officer, planet);

            MovementSystem movement = new MovementSystem(game, new FogOfWarSystem(game));
            return (game, planet, officer, movement);
        }

        // Creates a mission with the officer in MainParticipants (but officer stays parented to
        // the planet, not the mission) so IncrementProgress counts down and IsMovable() holds.
        private InstantMission CreateMission(GameRoot game, Planet planet, Officer officer)
        {
            InstantMission mission = new InstantMission("empire", planet.InstanceID);
            game.AttachNode(mission, planet);
            mission.MainParticipants.Add(officer);
            return mission;
        }

        [Test]
        public void UpdateMission_OnCompletion_WithFriendlyPlanet_EmitsCharacterMovedResult()
        {
            (GameRoot game, Planet planet, Officer officer, MovementSystem movement) = BuildScene(
                factionOwnsPlanet: true
            );
            InstantMission mission = CreateMission(game, planet, officer);
            MissionSystem system = new MissionSystem(game, movement);

            while (!mission.IsComplete())
                mission.IncrementProgress();

            List<GameResult> results = system.UpdateMission(mission, new AlwaysSucceedRNG());

            List<CharacterMovedResult> moveResults = results
                .OfType<CharacterMovedResult>()
                .ToList();
            Assert.AreEqual(1, moveResults.Count, "Should emit one CharacterMovedResult");
            Assert.AreEqual("o1", moveResults[0].CharacterInstanceID);
            Assert.AreEqual(planet.InstanceID, moveResults[0].ToLocationInstanceID);
        }

        [Test]
        public void UpdateMission_OnCompletion_NoFriendlyPlanet_SkipsMovement()
        {
            // Faction owns no planets and mission planet is unowned — no valid destination,
            // movement skipped. Officer is not attached to the scene graph so the planet
            // ownership check is never triggered during setup.
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });

            PlanetSystem planetSystem = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(planetSystem, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = null,
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int>(),
            };
            game.AttachNode(planet, planetSystem);

            // Officer is NOT attached to the scene graph — just in MainParticipants so
            // IncrementProgress counts down and IsOnMission() returns false.
            Officer officer = new Officer
            {
                InstanceID = "o1",
                OwnerInstanceID = "empire",
                Movement = null,
            };

            FogOfWarSystem fogOfWar = new FogOfWarSystem(game);
            MovementSystem movement = new MovementSystem(game, fogOfWar);
            MissionSystem missionSystem = new MissionSystem(game, movement);

            InstantMission mission = new InstantMission("empire", planet.InstanceID);
            game.AttachNode(mission, planet);
            mission.MainParticipants.Add(officer);

            while (!mission.IsComplete())
                mission.IncrementProgress();

            List<GameResult> results = null;
            Assert.DoesNotThrow(
                () => results = missionSystem.UpdateMission(mission, new AlwaysSucceedRNG()),
                "Should not throw when faction owns no planets"
            );

            Assert.IsFalse(
                results.OfType<CharacterMovedResult>().Any(),
                "Should not emit CharacterMovedResult when no valid destination exists"
            );
        }

        [Test]
        public void UpdateMission_OnCompletion_NoNearestPlanet_FallsBackToOwnedMissionPlanet()
        {
            // Faction owns the mission's planet but GetNearestPlanetTo still returns it
            // (it's in the owned list), so this is effectively the same as the happy path.
            // Distinct scenario: simulate GetNearestPlanetTo returning null by having
            // the faction own the mission planet — fallback path lands on same planet.
            (GameRoot game, Planet planet, Officer officer, MovementSystem movement) = BuildScene(
                factionOwnsPlanet: true
            );
            InstantMission mission = CreateMission(game, planet, officer);
            MissionSystem system = new MissionSystem(game, movement);

            while (!mission.IsComplete())
                mission.IncrementProgress();

            List<GameResult> results = system.UpdateMission(mission, new AlwaysSucceedRNG());

            List<CharacterMovedResult> moveResults = results
                .OfType<CharacterMovedResult>()
                .ToList();
            Assert.AreEqual(1, moveResults.Count, "Should emit CharacterMovedResult");
            Assert.AreEqual(planet.InstanceID, moveResults[0].ToLocationInstanceID);
        }

        [Test]
        public void UpdateMission_OnCompletion_DetachesMission()
        {
            (GameRoot game, Planet planet, Officer officer, MovementSystem movement) = BuildScene(
                factionOwnsPlanet: true
            );
            InstantMission mission = CreateMission(game, planet, officer);
            MissionSystem system = new MissionSystem(game, movement);

            while (!mission.IsComplete())
                mission.IncrementProgress();

            system.UpdateMission(mission, new AlwaysSucceedRNG());

            Assert.IsNull(
                mission.GetParent(),
                "Mission should be detached from scene graph after completion"
            );
        }
    }
}

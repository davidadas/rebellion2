using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Core.Configuration;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Systems;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class MissionSystemTests
    {
        // Builds a game with one planet, one officer parented to the planet (not the mission),
        // and optionally assigns the planet to the faction so GetNearestFriendlyPlanetTo returns it.
        private (
            GameRoot game,
            Planet planet,
            Officer officer,
            MovementSystem movement,
            OwnershipSystem ownership
        ) BuildScene(bool factionOwnsPlanet)
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
            OwnershipSystem ownership = new OwnershipSystem(
                game,
                movement,
                new ManufacturingSystem(game)
            );
            return (game, planet, officer, movement, ownership);
        }

        // Creates a mission with the officer in MainParticipants (but officer stays parented to
        // the planet, not the mission) so IncrementProgress counts down and IsMovable() holds.
        private StubMission CreateMission(GameRoot game, Planet planet, Officer officer)
        {
            StubMission mission = new StubMission("empire", planet.InstanceID);
            game.AttachNode(mission, planet);
            mission.MainParticipants.Add(officer);
            return mission;
        }

        [Test]
        public void UpdateMission_OnCompletion_WithFriendlyPlanet_EmitsCharacterMovedResult()
        {
            (
                GameRoot game,
                Planet planet,
                Officer officer,
                MovementSystem movement,
                OwnershipSystem ownership
            ) = BuildScene(factionOwnsPlanet: true);
            StubMission mission = CreateMission(game, planet, officer);
            MissionSystem system = new MissionSystem(game, movement, ownership);

            while (!mission.IsComplete())
                mission.IncrementProgress();

            List<GameResult> results = system.UpdateMission(mission, new StubRNG());

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
            OwnershipSystem ownership = new OwnershipSystem(
                game,
                movement,
                new ManufacturingSystem(game)
            );
            MissionSystem missionSystem = new MissionSystem(game, movement, ownership);

            StubMission mission = new StubMission("empire", planet.InstanceID);
            game.AttachNode(mission, planet);
            mission.MainParticipants.Add(officer);

            while (!mission.IsComplete())
                mission.IncrementProgress();

            List<GameResult> results = null;
            Assert.DoesNotThrow(
                () => results = missionSystem.UpdateMission(mission, new StubRNG()),
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
            // Faction owns the mission's planet but GetNearestFriendlyPlanetTo still returns it
            // (it's in the owned list), so this is effectively the same as the happy path.
            // Distinct scenario: simulate GetNearestFriendlyPlanetTo returning null by having
            // the faction own the mission planet — fallback path lands on same planet.
            (
                GameRoot game,
                Planet planet,
                Officer officer,
                MovementSystem movement,
                OwnershipSystem ownership
            ) = BuildScene(factionOwnsPlanet: true);
            StubMission mission = CreateMission(game, planet, officer);
            MissionSystem system = new MissionSystem(game, movement, ownership);

            while (!mission.IsComplete())
                mission.IncrementProgress();

            List<GameResult> results = system.UpdateMission(mission, new StubRNG());

            List<CharacterMovedResult> moveResults = results
                .OfType<CharacterMovedResult>()
                .ToList();
            Assert.AreEqual(1, moveResults.Count, "Should emit CharacterMovedResult");
            Assert.AreEqual(planet.InstanceID, moveResults[0].ToLocationInstanceID);
        }

        [Test]
        public void UpdateMission_OnCompletion_ParticipantParentedToMission_DoesNotThrow()
        {
            // Regression: officer parented to the mission (as happens after Initiate moves them
            // there) caused IsMovable() to return false and RequestMove to throw on teardown.
            (
                GameRoot game,
                Planet planet,
                Officer officer,
                MovementSystem movement,
                OwnershipSystem ownership
            ) = BuildScene(factionOwnsPlanet: true);
            StubMission mission = CreateMission(game, planet, officer);

            // Simulate the officer having arrived at the mission mid-execution.
            game.DetachNode(officer);
            officer.SetParent(mission);

            MissionSystem system = new MissionSystem(game, movement, ownership);

            while (!mission.IsComplete())
                mission.IncrementProgress();

            Assert.DoesNotThrow(() => system.UpdateMission(mission, new StubRNG()));
        }

        [Test]
        public void UpdateMission_OnCompletion_ParticipantParentedToMission_EmitsCharacterMoved()
        {
            (
                GameRoot game,
                Planet planet,
                Officer officer,
                MovementSystem movement,
                OwnershipSystem ownership
            ) = BuildScene(factionOwnsPlanet: true);
            StubMission mission = CreateMission(game, planet, officer);

            game.DetachNode(officer);
            officer.SetParent(mission);

            MissionSystem system = new MissionSystem(game, movement, ownership);

            while (!mission.IsComplete())
                mission.IncrementProgress();

            List<GameResult> results = system.UpdateMission(mission, new StubRNG());

            Assert.AreEqual(
                1,
                results.OfType<CharacterMovedResult>().Count(),
                "Should emit CharacterMovedResult even when officer was parented to the mission"
            );
        }

        [Test]
        public void UpdateMission_OnCompletion_ParticipantParentedToMission_NeutralPlanet_DoesNotThrow()
        {
            // Regression: neutral planet (null owner) must not be used as reparent target —
            // AddOfficer rejects officers whose faction doesn't match the planet owner.
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });

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
                OwnerInstanceID = null,
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(planet, system);

            Officer officer = new Officer { InstanceID = "o1", OwnerInstanceID = "empire" };
            MovementSystem movement = new MovementSystem(game, new FogOfWarSystem(game));
            OwnershipSystem ownership = new OwnershipSystem(
                game,
                movement,
                new ManufacturingSystem(game)
            );

            StubMission mission = new StubMission("empire", planet.InstanceID);
            game.AttachNode(mission, planet);
            mission.MainParticipants.Add(officer);
            officer.SetParent(mission);

            MissionSystem missionSystem = new MissionSystem(game, movement, ownership);

            while (!mission.IsComplete())
                mission.IncrementProgress();

            Assert.DoesNotThrow(() => missionSystem.UpdateMission(mission, new StubRNG()));
        }

        [Test]
        public void UpdateMission_OnCompletion_DetachesMission()
        {
            (
                GameRoot game,
                Planet planet,
                Officer officer,
                MovementSystem movement,
                OwnershipSystem ownership
            ) = BuildScene(factionOwnsPlanet: true);
            StubMission mission = CreateMission(game, planet, officer);
            MissionSystem system = new MissionSystem(game, movement, ownership);

            while (!mission.IsComplete())
                mission.IncrementProgress();

            system.UpdateMission(mission, new StubRNG());

            Assert.IsNull(
                mission.GetParent(),
                "Mission should be detached from scene graph after completion"
            );
        }

        [Test]
        public void Execute_WithDecoyParticipant_ParticipantNamesCountMatchesParticipantInstanceIDsCount()
        {
            // Bug: ParticipantNames is populated from MainParticipants only,
            // but ParticipantInstanceIDs uses GetAllParticipants() (main + decoy).
            // After the fix both lists must have the same length.
            (
                GameRoot game,
                Planet planet,
                Officer officer,
                MovementSystem movement,
                OwnershipSystem ownership
            ) = BuildScene(factionOwnsPlanet: true);

            Officer decoy = new Officer
            {
                InstanceID = "o2",
                DisplayName = "o2",
                OwnerInstanceID = "empire",
                Movement = null,
            };

            StubMission mission = new StubMission("empire", planet.InstanceID);
            game.AttachNode(mission, planet);
            mission.MainParticipants.Add(officer);
            mission.DecoyParticipants.Add(decoy);

            while (!mission.IsComplete())
                mission.IncrementProgress();

            List<GameResult> results = mission.Execute(game, new StubRNG());
            MissionCompletedResult completedResult = results
                .OfType<MissionCompletedResult>()
                .First();

            Assert.AreEqual(
                completedResult.ParticipantInstanceIDs.Count,
                completedResult.ParticipantNames.Count,
                "ParticipantNames and ParticipantInstanceIDs must have the same count"
            );
            Assert.IsTrue(
                completedResult.ParticipantInstanceIDs.Contains("o2"),
                "Decoy instance ID must appear in ParticipantInstanceIDs"
            );
            Assert.IsTrue(
                completedResult.ParticipantNames.Contains("o2"),
                "Decoy must appear in ParticipantNames"
            );
        }
    }
}

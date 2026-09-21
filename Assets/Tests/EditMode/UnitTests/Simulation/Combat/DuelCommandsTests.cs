using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public class DuelCommandsTests
    {
        /// <summary>Verifies that already captured officers do not consume duel rolls.</summary>
        [Test]
        public void Resolve_CapturedOfficer_ReturnsNoResultsWithoutRolling()
        {
            (GameRoot game, Officer encountered, Officer opposing) = BuildEncounter();
            encountered.IsCaptured = true;
            DuelCommands system = new DuelCommands(game, new ThrowingRNG());

            Assert.IsEmpty(system.Resolve(encountered, opposing));
        }

        /// <summary>Verifies that a completed capture invalidates the next duel for the same pair.</summary>
        [Test]
        public void Resolve_RepeatedPairAfterCapture_ResolvesOnlyFirstDuel()
        {
            (GameRoot game, Officer encountered, Officer opposing) = BuildEncounter();
            DuelCommands system = new DuelCommands(
                game,
                new FixedRandomProvider(new[] { 0.99, 0.99, 0.99 })
            );

            List<GameResult> results = system.Resolve(
                encountered,
                opposing,
                sourceEventInstanceID: "first"
            );
            results.AddRange(
                system.Resolve(encountered, opposing, sourceEventInstanceID: "second")
            );

            Assert.AreEqual(1, results.OfType<DuelResult>().Count());
            Assert.IsTrue(results.All(result => result.SourceEventInstanceID == "first"));
        }

        /// <summary>Verifies that capture precedes the duel report and uses the resolution tick.</summary>
        [Test]
        public void Resolve_Capture_EmitsOrderedResultsAtCurrentTick()
        {
            (GameRoot game, Officer encountered, Officer opposing) = BuildEncounter();
            game.CurrentTick = 42;
            DuelCommands system = new DuelCommands(
                game,
                new FixedRandomProvider(new[] { 0.99, 0.99, 0.99 })
            );

            List<GameResult> results = system.Resolve(
                encountered,
                opposing,
                "image",
                "audio",
                "event"
            );

            CollectionAssert.AreEqual(
                new[] { typeof(OfficerCaptureStateResult), typeof(DuelResult) },
                results.Select(result => result.GetType())
            );
            Assert.IsTrue(
                results.All(result => result.Tick == 42 && result.SourceEventInstanceID == "event")
            );
            DuelResult outcome = (DuelResult)results[1];
            Assert.AreEqual("image", outcome.ImagePath);
            Assert.AreEqual("audio", outcome.AudioPath);
        }

        /// <summary>Verifies that failed avoidance captures the encountered officer.</summary>
        [Test]
        public void Resolve_FailedAvoidance_CapturesEncounteredOfficer()
        {
            (GameRoot game, Officer encountered, Officer opposing) = BuildEncounter();
            DuelCommands system = new DuelCommands(
                game,
                new FixedRandomProvider(new[] { 0.99, 0.99, 0.99 })
            );

            List<GameResult> results = system.Resolve(
                encountered,
                opposing,
                sourceEventInstanceID: "event"
            );

            Assert.IsTrue(encountered.IsCaptured);
            Assert.AreEqual("empire", encountered.CaptorInstanceID);
            Assert.IsTrue(encountered.CanEscape);
            OfficerCaptureStateResult capture = results
                .OfType<OfficerCaptureStateResult>()
                .Single();
            Assert.AreSame(encountered, capture.TargetOfficer);
            Assert.AreSame(opposing, capture.CapturingUnit);
            Assert.AreSame(opposing, capture.LinkedOfficer);
            Assert.AreEqual("event", capture.SourceEventInstanceID);
            Assert.IsTrue(results.OfType<DuelResult>().Single().EncounteredOfficerCaptured);
        }

        /// <summary>Verifies that injuries reward the other officer's combat rating.</summary>
        [Test]
        public void Resolve_Injuries_RewardTheOtherOfficersCombat()
        {
            (GameRoot game, Officer encountered, Officer opposing) = BuildEncounter();
            DuelCommands system = new DuelCommands(
                game,
                new FixedRandomProvider(new[] { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 })
            );

            List<GameResult> results = system.Resolve(encountered, opposing);

            Assert.IsFalse(encountered.IsCaptured);
            Assert.AreEqual(1, encountered.InjuryPoints);
            Assert.AreEqual(1, opposing.InjuryPoints);
            Assert.AreEqual(51, encountered.GetBaseRating(SkillRating.Combat));
            Assert.AreEqual(51, opposing.GetBaseRating(SkillRating.Combat));
            Assert.AreEqual(2, results.OfType<OfficerInjuredResult>().Count());
        }

        /// <summary>Verifies that officers on different planets cannot duel.</summary>
        [Test]
        public void Resolve_OfficersOnDifferentPlanets_RejectsDuel()
        {
            (GameRoot game, Officer encountered, Officer opposing) = BuildEncounter();
            Planet other = new Planet
            {
                InstanceID = "other",
                OwnerInstanceID = "empire",
                IsColonized = true,
            };
            game.AttachNode(other, game.Galaxy.GetChildren<PlanetSector>()[0]);
            game.MoveNode(opposing, other);
            DuelCommands system = new DuelCommands(game, new FixedRandomProvider(new[] { 0.0 }));

            List<GameResult> results = system.Resolve(encountered, opposing);

            Assert.IsEmpty(results);
        }

        /// <summary>Verifies that either missing participant rejects the encounter before drawing randomness.</summary>
        /// <param name="missingEncountered">Whether to omit the encountered officer rather than the opponent.</param>
        [TestCase(true)]
        [TestCase(false)]
        public void Resolve_MissingOfficer_ReturnsNoResultsWithoutRolling(bool missingEncountered)
        {
            (GameRoot game, Officer encountered, Officer opposing) = BuildEncounter();
            DuelCommands commands = new DuelCommands(game, new ThrowingRNG());

            List<GameResult> results = commands.Resolve(
                missingEncountered ? null : encountered,
                missingEncountered ? opposing : null
            );

            Assert.IsEmpty(results);
        }

        /// <summary>
        /// Builds encounter.
        /// </summary>
        /// <returns>The constructed encounter.</returns>
        private static (GameRoot game, Officer encountered, Officer opposing) BuildEncounter()
        {
            GameConfig config = new GameConfig();
            config.DuelResolution = new GameConfig.DuelResolutionConfig
            {
                CombatCaptureAvoidance = new Dictionary<int, int> { { 0, 50 } },
                CaptureEvasionInjuryBaseChance = 100,
                MinimumInjuryChance = 1,
                InjuryBase = 1,
                InjurySecondaryRollMaximum = 29,
                CombatReward = 1,
            };
            config.Recovery.MaxInjuryPoints = 100;
            GameRoot game = new GameRoot(config);
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            PlanetSector planetSector = new PlanetSector { InstanceID = "sector" };
            game.AttachNode(planetSector, game.Galaxy);
            Planet planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = "rebels",
                IsColonized = true,
            };
            game.AttachNode(planet, planetSector);
            Officer encountered = EntityFactory.CreateOfficer("luke", "rebels");
            Officer opposing = EntityFactory.CreateOfficer("vader", "empire");
            encountered.SetBaseRating(SkillRating.Combat, 50);
            opposing.SetBaseRating(SkillRating.Combat, 50);
            game.AttachNode(encountered, planet);
            opposing.IsCaptured = true;
            game.AttachNode(opposing, planet);
            opposing.IsCaptured = false;
            return (game, encountered, opposing);
        }
    }
}

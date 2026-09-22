using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Requests;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Systems;

namespace Rebellion.Tests.Sectors
{
    [TestFixture]
    public class DuelSystemTests
    {
        [Test]
        public void HandleResults_FailedAvoidance_CapturesEncounteredOfficer()
        {
            (GameRoot game, Officer encountered, Officer opposing) = BuildEncounter();
            DuelSystem system = new DuelSystem(
                game,
                new FixedRandomProvider(new[] { 0.99, 0.99, 0.99 })
            );

            List<GameResult> results = system.HandleRequests(
                new[] { Request(encountered, opposing, "event") }
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

        [Test]
        public void HandleResults_Injuries_RewardTheOtherOfficersCombat()
        {
            (GameRoot game, Officer encountered, Officer opposing) = BuildEncounter();
            DuelSystem system = new DuelSystem(
                game,
                new FixedRandomProvider(new[] { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 })
            );

            List<GameResult> results = system.HandleRequests(
                new[] { Request(encountered, opposing) }
            );

            Assert.IsFalse(encountered.IsCaptured);
            Assert.AreEqual(1, encountered.InjuryPoints);
            Assert.AreEqual(1, opposing.InjuryPoints);
            Assert.AreEqual(51, encountered.GetBaseRating(SkillRating.Combat));
            Assert.AreEqual(51, opposing.GetBaseRating(SkillRating.Combat));
            Assert.AreEqual(2, results.OfType<OfficerInjuredResult>().Count());
        }

        [Test]
        public void HandleResults_OfficersOnDifferentPlanets_RejectsDuel()
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
            DuelSystem system = new DuelSystem(game, new FixedRandomProvider(new[] { 0.0 }));

            List<GameResult> results = system.HandleRequests(
                new[] { Request(encountered, opposing) }
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

        /// <summary>
        /// Executes request.
        /// </summary>
        /// <param name="encountered">The encountered.</param>
        /// <param name="opposing">The opposing.</param>
        /// <param name="sourceEventInstanceId">The source event instance id.</param>
        /// <returns>The result of request.</returns>
        private static DuelRequest Request(
            Officer encountered,
            Officer opposing,
            string sourceEventInstanceId = null
        )
        {
            return new DuelRequest
            {
                EncounteredOfficer = encountered,
                OpposingOfficer = opposing,
                SourceEventInstanceID = sourceEventInstanceId,
            };
        }
    }
}

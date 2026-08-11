using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Game.Missions
{
    [TestFixture]
    public class CustomMissionTests
    {
        [Test]
        public void RollDuration_AuthoredBaseAndSpread_UsesDefinition()
        {
            (GameRoot game, Planet _, Planet location, Officer officer, _) =
                MissionSceneBuilder.Build();
            CustomMissionDefinition definition = OpposedDefinition();
            definition.Duration = new MissionDuration
            {
                Random = new RandomMissionDuration { MinimumTicks = 5, MaximumTicks = 15 },
            };
            CustomMission mission = CreateMission(game, location, definition, officer);

            int duration = mission.RollDuration(new QueueRNG(0.5));

            Assert.AreEqual(10, duration);
        }

        [Test]
        public void Execute_OpposedSuccessRule_ReturnsSuccessfulCompletionWithoutDomainMutation()
        {
            (GameRoot game, Planet _, Planet location, Officer _, _) = MissionSceneBuilder.Build();
            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            game.AttachNode(target, location);
            CustomMission mission = CreateMission(game, location, OpposedDefinition(), target);

            MissionCompletedResult result = mission
                .Execute(game, new FixedRNG(0))
                .OfType<MissionCompletedResult>()
                .Single();

            Assert.AreEqual(MissionOutcome.Success, result.Outcome);
            Assert.IsFalse(target.IsCaptured);
        }

        [Test]
        public void Execute_ChanceSuccessRule_UsesAuthoredParticipantRatings()
        {
            (GameRoot game, Planet _, Planet location, Officer participant, _) =
                MissionSceneBuilder.Build();
            participant.SetBaseRating(OfficerRating.Combat, 90);
            participant.SetBaseRating(OfficerRating.Espionage, 90);
            CustomMissionDefinition definition = new CustomMissionDefinition
            {
                InstanceID = "chance",
                DisplayName = "Chance",
                Duration = new MissionDuration { Fixed = new FixedMissionDuration { Ticks = 0 } },
                Success = new MissionSuccessRule
                {
                    Chance = new ChanceMissionSuccess
                    {
                        Ratings = new List<MissionRatingContribution>
                        {
                            new MissionRatingContribution
                            {
                                Rating = OfficerRating.Combat,
                                Divisor = 3,
                            },
                            new MissionRatingContribution
                            {
                                Rating = OfficerRating.Espionage,
                                Divisor = 3,
                            },
                        },
                    },
                },
            };
            CustomMission mission = CreateMission(
                game,
                location,
                definition,
                participant,
                participant
            );

            MissionCompletedResult result = mission
                .Execute(game, new FixedRNG(0.5))
                .OfType<MissionCompletedResult>()
                .Single();

            Assert.AreEqual(MissionOutcome.Success, result.Outcome);
        }

        [Test]
        public void Execute_AutomaticSuccessRule_ReturnsSuccessfulCompletion()
        {
            (GameRoot game, Planet _, Planet location, Officer participant, _) =
                MissionSceneBuilder.Build();
            CustomMissionDefinition definition = new CustomMissionDefinition
            {
                InstanceID = "automatic",
                DisplayName = "Automatic",
                Duration = new MissionDuration { Fixed = new FixedMissionDuration { Ticks = 0 } },
                Success = new MissionSuccessRule { Automatic = new AutomaticMissionSuccess() },
            };
            CustomMission mission = CreateMission(
                game,
                location,
                definition,
                participant,
                participant
            );

            MissionCompletedResult result = mission
                .Execute(game, new FixedRNG(0.99))
                .OfType<MissionCompletedResult>()
                .Single();

            Assert.AreEqual(MissionOutcome.Success, result.Outcome);
        }

        [Test]
        public void HandleResults_ValidRequest_CreatesDefinitionBackedMission()
        {
            (GameRoot game, Planet _, Planet location, Officer _, _) = MissionSceneBuilder.Build();
            Officer target = EntityFactory.CreateOfficer("target", "rebels");
            game.AttachNode(target, location);
            CustomMissionDefinition definition = OpposedDefinition();
            MovementSystem movement = new MovementSystem(
                game,
                new FogOfWarSystem(game),
                new FleetSystem(game)
            );
            MissionSystem system = TestSystems.CreateMissionSystem(
                game,
                new FixedRNG(0),
                movement,
                new[] { definition }
            );

            system.HandleResults(
                new[]
                {
                    new CustomMissionRequestedResult
                    {
                        MissionDefinitionID = definition.InstanceID,
                        TargetInstanceID = target.InstanceID,
                    },
                }
            );

            CustomMission mission = game.GetSceneNodesByType<CustomMission>().Single();
            Assert.AreEqual(definition.InstanceID, mission.MissionDefinitionID);
            Assert.AreSame(definition, mission.Definition);
            Assert.AreEqual(definition.DisplayName, mission.DisplayName);
            Assert.IsFalse(mission.CanCancel);
        }

        private static CustomMissionDefinition OpposedDefinition() =>
            new CustomMissionDefinition
            {
                InstanceID = "opposed",
                DisplayName = "Opposed",
                OwnerFactionInstanceID = "empire",
                Duration = new MissionDuration { Fixed = new FixedMissionDuration { Ticks = 0 } },
                Success = new MissionSuccessRule
                {
                    Opposed = new OpposedMissionSuccess
                    {
                        AttackRating = 0,
                        TargetRating = OfficerRating.Combat,
                        ProbabilityTableKey = AbductionMission.MissionTypeID,
                    },
                },
            };

        private static CustomMission CreateMission(
            GameRoot game,
            Planet location,
            CustomMissionDefinition definition,
            IGameEntity target,
            params IMissionParticipant[] participants
        )
        {
            CustomMission mission = new CustomMission(
                definition,
                target.InstanceID,
                null,
                participants.Select(participant => participant.InstanceID),
                new List<string>(),
                "event",
                game
            );
            game.AttachNode(mission, location);
            return mission;
        }
    }
}

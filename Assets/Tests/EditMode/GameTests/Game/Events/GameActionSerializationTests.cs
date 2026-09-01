using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Advisor;
using Rebellion.Game.Events;
using Rebellion.Game.Messages;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public sealed class GameActionSerializationTests
    {
        [Test]
        public void RoundTrip_PlanetActions_RestoresConcreteValues()
        {
            GameEvent gameEvent = new GameEvent
            {
                Actions = new List<GameAction>
                {
                    new ChangeEnergyCapacityAction
                    {
                        PlanetInstanceID = "NABOO",
                        RollInteger = new RollInteger { Minimum = -3, Maximum = -1 },
                    },
                    new ChangePopularSupportAction
                    {
                        FactionInstanceID = "FNALL1",
                        PlanetBinding = "planet",
                        AmountBinding = "supportChange",
                    },
                    new ChangeRawResourceNodesAction
                    {
                        PlanetInstanceID = "NABOO",
                        PercentOfCurrent = 25,
                    },
                    new DamagePlanetResourcesAction
                    {
                        PlanetBinding = "planet",
                        ProbabilityBinding = "damageProbability",
                        MinimumTotalLoss = 2,
                    },
                    new SetPopularSupportAction
                    {
                        FactionInstanceID = "FNEMP1",
                        PlanetInstanceID = "NABOO",
                        Support = 50,
                    },
                },
            };

            string xml = SerializationHelper.Serialize(gameEvent);
            GameEvent restored = SerializationHelper.Deserialize<GameEvent>(xml);

            ChangeEnergyCapacityAction energy = restored.Actions[0] as ChangeEnergyCapacityAction;
            Assert.IsNotNull(energy);
            Assert.AreEqual(-3, energy.RollInteger.Minimum);
            Assert.AreEqual(-1, energy.RollInteger.Maximum);
            ChangePopularSupportAction changeSupport =
                restored.Actions[1] as ChangePopularSupportAction;
            Assert.IsNotNull(changeSupport);
            Assert.AreEqual("FNALL1", changeSupport.FactionInstanceID);
            Assert.AreEqual("supportChange", changeSupport.AmountBinding);
            Assert.AreEqual(
                25,
                ((ChangeRawResourceNodesAction)restored.Actions[2]).PercentOfCurrent
            );
            DamagePlanetResourcesAction damage = restored.Actions[3] as DamagePlanetResourcesAction;
            Assert.IsNotNull(damage);
            Assert.AreEqual("damageProbability", damage.ProbabilityBinding);
            Assert.AreEqual(2, damage.MinimumTotalLoss);
            Assert.AreEqual(50, ((SetPopularSupportAction)restored.Actions[4]).Support);
        }

        [Test]
        public void RoundTrip_PresentationActions_RestoresConcreteValues()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "EVENT_STORY",
                Actions = new List<GameAction>
                {
                    new RollOutcomeAction
                    {
                        Outcomes = new List<RandomOutcome>
                        {
                            new RandomOutcome
                            {
                                Weight = 3,
                                Actions = new List<GameAction>
                                {
                                    new SendMessageAction
                                    {
                                        RecipientFactionInstanceID = "FNALL1",
                                        SubjectInstanceID = "LUKE",
                                        RelatedSubjectInstanceID = "VADER",
                                        ShowSubjectImage = true,
                                        MessageType = MessageType.Advice,
                                        BackgroundAudio = new MessageAudio
                                        {
                                            Path = "Story/dialogue",
                                        },
                                        AdvisorNotification = new AdvisorNotification
                                        {
                                            Preset = AdvisorNotificationPreset.SubjectReport,
                                            Protocol = new AdvisorAnimation
                                            {
                                                AnimationPath = "Story/advisor",
                                                FrameCount = 3,
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                    new SetNodeStateAction
                    {
                        InstanceID = "LUKE_SKYWALKER",
                        State = SceneNodeState.Inactive,
                    },
                    new IncreaseForceRankAction
                    {
                        OfficerInstanceID = "LUKE_SKYWALKER",
                        Amount = 5,
                    },
                },
            };

            string xml = SerializationHelper.Serialize(gameEvent);
            GameEvent restored = SerializationHelper.Deserialize<GameEvent>(xml);

            RollOutcomeAction outcome = restored.Actions[0] as RollOutcomeAction;
            SendMessageAction message =
                outcome?.Outcomes.Single().Actions.Single() as SendMessageAction;
            Assert.IsNotNull(message);
            Assert.AreEqual("LUKE", message.SubjectInstanceID);
            Assert.AreEqual("VADER", message.RelatedSubjectInstanceID);
            Assert.IsTrue(message.ShowSubjectImage);
            Assert.AreEqual("Story/dialogue", message.BackgroundAudio.Path);
            Assert.AreEqual("Story/advisor", message.AdvisorNotification.Protocol.AnimationPath);
            Assert.AreEqual(3, message.AdvisorNotification.Protocol.FrameCount);
            Assert.AreEqual("LUKE_SKYWALKER", ((SetNodeStateAction)restored.Actions[1]).InstanceID);
            Assert.AreEqual(
                SceneNodeState.Inactive,
                ((SetNodeStateAction)restored.Actions[1]).State
            );
            Assert.AreEqual(5, ((IncreaseForceRankAction)restored.Actions[2]).Amount);
        }

        [Test]
        public void RoundTrip_RandomActions_RestoresConcreteValues()
        {
            GameEvent gameEvent = new GameEvent
            {
                Actions = new List<GameAction>
                {
                    new RollChanceAction
                    {
                        ProbabilityBinding = "probability",
                        Actions = new List<GameAction>
                        {
                            new SetEventVariableAction
                            {
                                Key = "result",
                                RollInteger = new RollInteger { Minimum = 1, Maximum = 3 },
                            },
                        },
                    },
                    new RollOutcomeAction
                    {
                        Outcomes = new List<RandomOutcome>
                        {
                            new RandomOutcome { Weight = 5, Actions = new List<GameAction>() },
                        },
                    },
                },
            };

            string xml = SerializationHelper.Serialize(gameEvent);
            GameEvent restored = SerializationHelper.Deserialize<GameEvent>(xml);

            RollChanceAction chance = restored.Actions[0] as RollChanceAction;
            Assert.IsNotNull(chance);
            Assert.AreEqual("probability", chance.ProbabilityBinding);
            SetEventVariableAction setVariable = chance.Actions.Single() as SetEventVariableAction;
            Assert.IsNotNull(setVariable);
            Assert.AreEqual(1, setVariable.RollInteger.Minimum);
            Assert.AreEqual(3, setVariable.RollInteger.Maximum);
            Assert.AreEqual(5, ((RollOutcomeAction)restored.Actions[1]).Outcomes.Single().Weight);
        }
    }
}

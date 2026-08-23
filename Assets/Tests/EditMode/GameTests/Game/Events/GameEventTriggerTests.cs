using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Events;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public class GameEventTriggerTests
    {
        [Test]
        public void Matches_UnitArrivedTrigger_AppliesIdentityAndDestinationFilters()
        {
            UnitArrivedTrigger trigger = new UnitArrivedTrigger
            {
                UnitInstanceID = "officer",
                DestinationInstanceID = "planet",
            };
            UnitArrivedResult result = new UnitArrivedResult
            {
                Unit = new Officer { InstanceID = "officer" },
                Destination = new Planet { InstanceID = "planet" },
            };

            Assert.IsTrue(trigger.Matches(result));
            result.Destination.InstanceID = "elsewhere";
            Assert.IsFalse(trigger.Matches(result));
        }

        [Test]
        public void Matches_DuelCompletedTrigger_AppliesOfficerAndSourceFilters()
        {
            DuelCompletedTrigger trigger = new DuelCompletedTrigger
            {
                FirstOfficerInstanceID = "luke",
                SecondOfficerInstanceID = "vader",
                SourceEventInstanceID = "encounter",
            };
            DuelResult result = new DuelResult
            {
                EncounteredOfficer = new Officer { InstanceID = "luke" },
                OpposingOfficer = new Officer { InstanceID = "vader" },
                SourceEventInstanceID = "encounter",
            };

            Assert.IsTrue(trigger.Matches(result));
            result.SourceEventInstanceID = "other";
            Assert.IsFalse(trigger.Matches(result));
        }

        [Test]
        public void Bind_TriggerWithAlias_ExposesCompleteResult()
        {
            DuelResult result = new DuelResult();
            DuelCompletedTrigger trigger = new DuelCompletedTrigger { As = "duel" };

            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent(),
                new GameEventState(),
                result,
                trigger
            );

            Assert.AreSame(result, context.GetBinding<DuelResult>("duel"));
        }
    }
}

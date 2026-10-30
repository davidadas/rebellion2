using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Game.Scripting;
using Rebellion.Game.Units;
using Rebellion.Gameplay.Scripting;

namespace Rebellion.Tests.Gameplay.Scripting
{
    [TestFixture]
    public class GameEvaluationContextTests
    {
        [Test]
        public void Bind_BlankName_ThrowsArgumentException()
        {
            GameEvaluationContext context = new GameEvaluationContext(
                null,
                triggerResult: null,
                sourceEventInstanceID: null,
                sourceKind: "Event"
            );

            TestDelegate bind = () => context.Bind(" ", new object());

            Assert.Throws<ArgumentException>(bind);
        }

        [Test]
        public void Bind_DuplicateName_ThrowsInvalidOperationException()
        {
            GameEvaluationContext context = new GameEvaluationContext(
                null,
                triggerResult: null,
                sourceEventInstanceID: null,
                sourceKind: "Event"
            );
            context.Bind("binding", 1);

            TestDelegate bind = () => context.Bind("binding", 2);

            Assert.Throws<InvalidOperationException>(bind);
        }

        [Test]
        public void GetBindingReference_ExactOpaqueName_ReturnsValue()
        {
            GameEvaluationContext context = new GameEvaluationContext(
                null,
                triggerResult: null,
                sourceEventInstanceID: null,
                sourceKind: "Event"
            );
            context.Bind("#@$#@binding", 42);

            int value = context.GetBindingReference<int>("#@$#@binding");

            Assert.AreEqual(42, value);
        }

        [Test]
        public void AddResult_NullResult_DoesNotRecordResult()
        {
            GameEvaluationContext context = new GameEvaluationContext(
                null,
                triggerResult: null,
                sourceEventInstanceID: null,
                sourceKind: "Event"
            );

            context.AddResult(null);

            Assert.IsEmpty(context.Results);
        }

        [Test]
        public void Constructor_TriggerArgument_ExposesOnlyAuthoredValue()
        {
            Officer officer = new Officer { InstanceID = "luke" };
            DuelResult result = new DuelResult { EncounteredOfficer = officer };
            DuelCompletedTrigger trigger = new DuelCompletedTrigger
            {
                Bindings = new List<GameBinding>
                {
                    new GameBinding { Argument = "FirstOfficer", As = "officer" },
                },
            };

            GameEvaluationContext context = new GameEvaluationContext(
                null,
                triggerResult: result,
                trigger: trigger,
                sourceEventInstanceID: null,
                sourceKind: "Event"
            );

            Assert.AreSame(officer, context.GetBinding<Officer>("officer"));
        }
    }
}

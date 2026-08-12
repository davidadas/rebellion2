using NUnit.Framework;
using Rebellion.Game.Events;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public class GameEventTriggerTests
    {
        [Test]
        public void Bind_UnitArrivedTrigger_BindsAuthoredValues()
        {
            Officer officer = new Officer { InstanceID = "officer" };
            Planet destination = new Planet { InstanceID = "planet" };
            GameEventTrigger trigger = new GameEventTrigger(
                "core:unit.arrived",
                ("Unit", "unit"),
                ("Destination", "destination")
            );

            GameEventExecutionContext context = CreateContext(
                trigger,
                new UnitArrivedResult { Unit = officer, Destination = destination }
            );

            Assert.AreSame(officer, context.GetBinding<Officer>("unit"));
            Assert.AreSame(destination, context.GetBinding<Planet>("destination"));
        }

        [Test]
        public void Bind_DuelCompletedTrigger_BindsAuthoredValues()
        {
            Officer encountered = new Officer { InstanceID = "encountered" };
            Officer opponent = new Officer { InstanceID = "opponent" };
            GameEventTrigger trigger = new GameEventTrigger(
                "core:duel.completed",
                ("Officer", "officer"),
                ("Opponent", "opponent")
            );

            GameEventExecutionContext context = CreateContext(
                trigger,
                new DuelResult { EncounteredOfficer = encountered, OpposingOfficer = opponent }
            );

            Assert.AreSame(encountered, context.GetBinding<Officer>("officer"));
            Assert.AreSame(opponent, context.GetBinding<Officer>("opponent"));
        }

        [Test]
        public void Bind_OfficerCaptureChangedTrigger_BindsAuthoredValues()
        {
            Officer officer = new Officer { InstanceID = "officer" };
            Officer linkedOfficer = new Officer { InstanceID = "linked" };
            Planet planet = new Planet { InstanceID = "planet" };
            GameEventTrigger trigger = new GameEventTrigger(
                "core:officer.capture-changed",
                ("Officer", "officer"),
                ("LinkedOfficer", "linkedOfficer"),
                ("Context", "context")
            );

            GameEventExecutionContext context = CreateContext(
                trigger,
                new OfficerCaptureStateResult
                {
                    TargetOfficer = officer,
                    LinkedOfficer = linkedOfficer,
                    Context = planet,
                }
            );

            Assert.AreSame(officer, context.GetBinding<Officer>("officer"));
            Assert.AreSame(linkedOfficer, context.GetBinding<Officer>("linkedOfficer"));
            Assert.AreSame(planet, context.GetBinding<Planet>("context"));
        }

        [Test]
        public void Bind_MissionCompletedTrigger_BindsAuthoredValues()
        {
            Mission mission = new StubMission();
            GameEventTrigger trigger = new GameEventTrigger(
                "core:mission.completed",
                ("Mission", "mission")
            );

            GameEventExecutionContext context = CreateContext(
                trigger,
                new MissionCompletedResult { Mission = mission }
            );

            Assert.AreSame(mission, context.GetBinding<Mission>("mission"));
        }

        private static GameEventExecutionContext CreateContext(
            GameEventTrigger trigger,
            GameResult result
        ) =>
            new GameEventExecutionContext(
                new GameEvent(),
                new GameEventState(),
                null,
                result,
                trigger
            );
    }
}

using NUnit.Framework;
using Rebellion.Game.Events;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public class GameEventTriggerBindingsTests
    {
        [Test]
        public void Bind_UnitArrival_BindsUnitDestinationAndPlanet()
        {
            Officer officer = new Officer { InstanceID = "officer" };
            Planet destination = new Planet { InstanceID = "planet" };
            UnitArrivedResult arrival = new UnitArrivedResult
            {
                Unit = officer,
                Destination = destination,
            };

            GameEventExecutionContext context = new GameEventExecutionContext(
                new GameEvent(),
                new GameEventState(),
                null
            );
            GameEventTriggerBindings.Bind(context, arrival);

            Assert.AreSame(officer, context.GetBinding<Officer>("unit"));
            Assert.AreSame(destination, context.GetBinding<Planet>("destination"));
            Assert.AreSame(destination, context.GetBinding<Planet>("planet"));
        }

        [Test]
        public void Bind_OfficerEncounter_BindsBothOfficers()
        {
            Officer encountered = new Officer { InstanceID = "encountered" };
            Officer opponent = new Officer { InstanceID = "opponent" };
            GameEventExecutionContext context = CreateContext();

            GameEventTriggerBindings.Bind(
                context,
                new OfficerEncounterResult
                {
                    EncounteredOfficer = encountered,
                    OpposingOfficer = opponent,
                }
            );

            Assert.AreSame(encountered, context.GetBinding<Officer>("officer"));
            Assert.AreSame(opponent, context.GetBinding<Officer>("opponent"));
        }

        [Test]
        public void Bind_OfficerCapture_BindsOfficerLinkAndContext()
        {
            Officer officer = new Officer { InstanceID = "officer" };
            Officer linkedOfficer = new Officer { InstanceID = "linked" };
            Planet planet = new Planet { InstanceID = "planet" };
            GameEventExecutionContext context = CreateContext();

            GameEventTriggerBindings.Bind(
                context,
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
        public void Bind_MissionCompletion_BindsMission()
        {
            Mission mission = new StubMission();
            GameEventExecutionContext context = CreateContext();

            GameEventTriggerBindings.Bind(
                context,
                new MissionCompletedResult { Mission = mission }
            );

            Assert.AreSame(mission, context.GetBinding<Mission>("mission"));
        }

        private static GameEventExecutionContext CreateContext() =>
            new GameEventExecutionContext(new GameEvent(), new GameEventState(), null);
    }
}
